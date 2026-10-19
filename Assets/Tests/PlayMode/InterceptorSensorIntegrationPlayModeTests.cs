#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class InterceptorSensorIntegrationPlayModeTests
    {
        private Component brain,player,motor,graph;
        private NavMeshAgent agent;
        private object awareness,hearing;
        private float capture;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private readonly List<GameObject> temporary=new List<GameObject>();
        private static Type T(string name)=>Type.GetType("ShiftFour."+name+", Assembly-CSharp",true);
        private static Component[] Find(string name)=>Object.FindObjectsByType(T(name)).Cast<Component>().ToArray();
        private static object Get(object o,string name)=>o.GetType().GetProperty(name).GetValue(o);
        private static object Value(object o,string name)=>o.GetType().GetField(name).GetValue(o);
        private static FieldInfo Field(object o,string name)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType)
            {
                var f=t.GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(f!=null)return f;
            }
            throw new MissingFieldException(name);
        }
        private static object Call(object o,string name,params object[] args)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType)
            {
                var m=t.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(m!=null)return m.Invoke(o,args);
            }
            throw new MissingMethodException(name);
        }
        private object Policy=>Get(brain,"Policy");
        private string State=>Get(Policy,"State").ToString();
        private Vector3 Home=>(Vector3)Get(brain,"Home");
        private bool SoundAction=>(bool)Get(brain,"IsInvestigatingSound");
        private void Tick(){Field(brain,"nextDecision").SetValue(brain,0f);Call(brain,"Update");}
        private void MovePlayer(Vector3 point)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=point;cc.enabled=true;
            Call(player,"ApplyCameraPose");Physics.SyncTransforms();
        }
        private Vector3[] Route(Vector3 a,Vector3 b)=>(Vector3[])Call(Get(brain,"TacticalValidator"),"Route",a,b);
        private void Noise(Vector3 point)=>T("SoundEvents").GetMethod("ReportShot").Invoke(null,new object[]{point});
        private bool Latest(out object sound)
        {
            object[] args={null};bool result=(bool)hearing.GetType().GetMethod("TryGetLatest").Invoke(hearing,args);sound=args[0];return result;
        }
        private IEnumerator Emit()
        {
            foreach(Vector3 direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                Noise(brain.transform.position+direction*3f);
                if(Latest(out var sound)&&(float)Value(sound,"EmittedAt")>=Time.time-.001f&&Route(brain.transform.position,(Vector3)Value(sound,"Position"))!=null)yield break;
                yield return null;
            }
            Assert.Fail("Actual Control fixture needs a reachable uncertain sound");
        }
        [UnitySetUp]public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/30f;cursor=Cursor.lockState;cursorVisible=Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            foreach(var b in Find("DroneBrain"))
            {
                ((Behaviour)b).enabled=false;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=false;
                var nav=b.GetComponent<NavMeshAgent>();nav.ResetPath();nav.isStopped=true;nav.velocity=Vector3.zero;
            }
            brain=Find("InterceptorBrain").Single();player=Find("PlayerController").Single();((Behaviour)player).enabled=false;
            motor=brain.GetComponent(T("DroneMotor"));agent=brain.GetComponent<NavMeshAgent>();graph=Find("WaypointGraph").Single();
            awareness=Activator.CreateInstance(T("DroneAwareness"),Activator.CreateInstance(T("AwarenessSettings")),Get(brain,"DifficultySettings"));
            Field(brain,"<Awareness>k__BackingField").SetValue(brain,awareness);
            Field(brain,"lastSeenAt").SetValue(brain,float.NegativeInfinity);Field(brain,"lastKnown").SetValue(brain,Home);
            Field(brain,"policy").SetValue(brain,null);hearing=Get(brain,"Hearing");Call(hearing,"Configure",graph,1f);
            MovePlayer(new Vector3(-1000,0,-1000));Tick();
            Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            foreach(var go in temporary)if(go!=null)Object.Destroy(go);temporary.Clear();
            Scene old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("After Interceptor sensors"));
            yield return SceneManager.UnloadSceneAsync(old);Time.captureDeltaTime=capture;Cursor.lockState=cursor;Cursor.visible=cursorVisible;
        }
        [UnityTest]public IEnumerator MainLab_SoundResponse_BeforeAfterEvidence()
        {
            bool before=Environment.GetEnvironmentVariable("SHIFTFOUR_INTERCEPTOR_VARIANT")=="before";
            var property=brain.GetType().GetProperty("IsInvestigatingSound");
            Assert.That(property==null,Is.EqualTo(before),"Baseline evidence must use saved original sources; candidate evidence must use the new adapter");
            string directory=Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE")??"Logs/InterceptorEvidence";
            Directory.CreateDirectory(directory);
            var rows=new List<string>{"variant,seconds,awareness,decision,sound_action,history,x,z,goal_x,goal_z,speed_cap"};
            float started=Time.time;yield return Emit();Tick();
            var captured=new HashSet<string>();float until=Time.time+10f;bool investigated=false;
            while(Time.time<until)
            {
                Tick();bool sound=property!=null&&(bool)property.GetValue(brain);investigated|=sound;
                string state=Get(awareness,"State").ToString();Vector3 goal=(Vector3)Field(motor,"goal").GetValue(motor);
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F3},{2},\"{3}\",{4},{5},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3}",
                    before?"before":"after",Time.time-started,state,Get(brain,"CurrentDecision"),sound,((IList)Get(Policy,"History")).Count,
                    brain.transform.position.x,brain.transform.position.z,goal.x,goal.z,agent.speed));
                if(captured.Add(state))yield return SoundFrame(directory,(before?"before-":"after-")+state,goal);
                ((Behaviour)motor).enabled=true;
                if(Time.time-started>6f&&!sound)break;
                yield return new WaitForSeconds(.3f);
            }
            File.WriteAllLines(Path.Combine(directory,"sound-response.csv"),rows);
            Assert.That(investigated,Is.EqualTo(!before));Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);
            Assert.That(Get(Policy,"LastObservedAt"),Is.EqualTo(float.NegativeInfinity));
            Assert.That(Get(awareness,"HasVisualConfirmation"),Is.False);
        }
        private IEnumerator SoundFrame(string directory,string name,Vector3 goal)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)yield break;
            var objects=new List<GameObject>();var materials=new List<Material>();
            Vector3[] route=Route(brain.transform.position,goal);
            if(route!=null&&route.Length>1)
            {
                var lineObject=new GameObject("Temporary measured sound or home route");objects.Add(lineObject);
                var line=lineObject.AddComponent<LineRenderer>();var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor",new Color(1,.55f,.1f));materials.Add(material);line.sharedMaterial=material;
                line.widthMultiplier=.1f;line.positionCount=route.Length;line.SetPositions(route.Select(v=>v+Vector3.up*.15f).ToArray());
            }
            var go=new GameObject("Temporary Control sensor evidence camera");objects.Add(go);
            var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=10.5f;camera.nearClipPlane=.05f;
            camera.transform.position=Home+Vector3.up*2.8f;camera.transform.rotation=Quaternion.Euler(90,0,0);
            var render=new RenderTexture(960,960,24);render.Create();camera.targetTexture=render;yield return null;
            var previous=RenderTexture.active;RenderTexture.active=render;var pixels=new Texture2D(960,960,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,960,960),0,0);pixels.Apply();
                using var writer=new BinaryWriter(File.Create(Path.Combine(directory,name+".tga")));
                writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0});writer.Write((short)960);writer.Write((short)960);writer.Write((byte)24);writer.Write((byte)0);
                foreach(Color32 pixel in pixels.GetPixels32()){writer.Write(pixel.b);writer.Write(pixel.g);writer.Write(pixel.r);}
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;render.Release();Object.Destroy(render);Object.Destroy(pixels);
                foreach(var goToRemove in objects)Object.Destroy(goToRemove);foreach(var material in materials)Object.Destroy(material);
            }
        }
        [UnityTest]public IEnumerator SoundOnly_InvestigatesWithoutVisualHistoryVelocityOrCombat()
        {
            float hp=(float)Get(player.GetComponent(T("Health")),"Current");yield return Emit();Tick();
            Assert.That(Get(awareness,"State").ToString(),Is.EqualTo("Suspicious"));Assert.That(SoundAction,Is.False);
            yield return new WaitForSeconds(.5f);Tick();Assert.That(SoundAction,Is.True);
            Assert.That(Get(brain,"CurrentDecision"),Is.EqualTo("Investigate heard disturbance"));
            Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);Assert.That(Get(Policy,"LastObservedAt"),Is.EqualTo(float.NegativeInfinity));
            Assert.That(Get(Policy,"SelectedNode"),Is.EqualTo(-1));Assert.That(Get(Policy,"Confidence"),Is.EqualTo(0f));
            Assert.That(Field(Policy,"velocity").GetValue(Policy),Is.EqualTo(Vector3.zero));
            Assert.That(Get(awareness,"HasVisualConfirmation"),Is.False);Assert.That(Get(player.GetComponent(T("Health")),"Current"),Is.EqualTo(hp));
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(3.1f).Within(.001f));
            Vector3 goal=(Vector3)Get(brain,"HeardDestination");Assert.That(Latest(out var heard),Is.True);
            Assert.That(Vector3.Distance(goal,(Vector3)Value(heard,"Position")),Is.LessThanOrEqualTo((float)Value(heard,"UncertaintyRadius")+.001f));
            var first=Call(brain,"Decide",false,Vector3.one*99999,99999f);
            var second=Call(brain,"Decide",false,new Vector3(float.NaN,float.PositiveInfinity,-99999),float.NaN);
            Assert.That(Value(first,"Destination"),Is.EqualTo(Value(second,"Destination")));
        }
        [UnityTest]public IEnumerator SoundExpiry_ReturnsHomeWithoutFollowingMovedSource()
        {
            yield return Emit();Tick();yield return new WaitForSeconds(.5f);Tick();Assert.That(SoundAction,Is.True);
            Vector3 goal=(Vector3)Get(brain,"HeardDestination");MovePlayer(new Vector3(1000,0,1000));Tick();Assert.That(Get(brain,"HeardDestination"),Is.EqualTo(goal));
            yield return new WaitForSeconds(5.5f);Tick();Assert.That(SoundAction,Is.False);
            Assert.That(Get(Policy,"SelectedNode"),Is.EqualTo(-1));Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);
            Assert.That(State,Is.EqualTo("WatchHome").Or.EqualTo("ReturnHome"));
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(2.5f).Within(.001f));
        }
        [UnityTest]public IEnumerator LockedControlGate_RejectsSoundAndTacticalRoute()
        {
            var gate=Find("LabDoor").Single(g=>(int)Get(g,"GateId")==2);Assert.That(Get(gate,"IsOpen"),Is.False);
            Vector3 otherSide=new Vector3(10,0,-3);Noise(otherSide);Tick();
            Assert.That(Latest(out _),Is.False);Assert.That(SoundAction,Is.False);Assert.That(Route(brain.transform.position,otherSide),Is.Null);
            Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);yield return null;
        }
        private IEnumerator VisibleMotion()
        {
            Call(motor,"SetMovementMode",Enum.Parse(T("DroneMovementMode"),"Chase"));((Behaviour)motor).enabled=true;
            yield return new WaitForSeconds(.3f);((Behaviour)motor).enabled=false;agent.ResetPath();agent.isStopped=true;
            Vector3 source=default,heading=default;bool found=false;
            var nodes=((IEnumerable)Get(Get(brain,"TacticalNavigation"),"Points")).Cast<Vector3>().ToArray();
            foreach(Vector3 node in nodes)
            {
                for(int angle=0;angle<360;angle+=45)
                {
                    Vector3 forward=Quaternion.Euler(0,angle,0)*Vector3.forward;
                    Vector3 start=node-forward*6f,staging=node+Vector3.Cross(forward,Vector3.up)*2.5f;
                    if(!NavMesh.SamplePosition(staging,out var hit,.25f,agent.areaMask)||!(bool)Call(Get(brain,"TacticalValidator"),"CanOccupy",hit.position))continue;
                    MovePlayer(new Vector3(-1000,0,-1000));
                    if(!agent.Warp(hit.position))continue;Physics.SyncTransforms();
                    var route=Route(start,node);if(route==null||Route(brain.transform.position,node)==null||route.Length!=2)continue;
                    bool clear=true;
                    for(int i=0;i<=7;i++)
                    {
                        MovePlayer(start+forward*i*.36f);brain.transform.rotation=Quaternion.LookRotation(player.transform.position-brain.transform.position);Physics.SyncTransforms();
                        if(!(bool)Call(brain,"CanSeePlayer")){clear=false;break;}
                    }
                    if(!clear)continue;source=start;heading=forward;found=true;break;
                }
                if(found)break;
            }
            Assert.That(found,Is.True,"Must find a genuinely visible, collision-clear Control staging route without changing MainLab");
            Field(brain,"policy").SetValue(brain,null);
            for(int i=0;i<=7;i++)
            {
                MovePlayer(source+heading*i*.36f);brain.transform.rotation=Quaternion.LookRotation(player.transform.position-brain.transform.position);Physics.SyncTransforms();
                Assert.That(Call(brain,"CanSeePlayer"),Is.True);Tick();if(i<7)yield return new WaitForSeconds(.3f);
            }
            Assert.That(State,Is.EqualTo("InterceptAhead"));Assert.That(Get(Policy,"UsesMotionTiming"),Is.True);
        }
        [UnityTest]public IEnumerator VisiblePrediction_RetainsCommitmentAcrossFreshUncertainSoundAndLostVision()
        {
            yield return VisibleMotion();Assert.That(SoundAction,Is.False);
            int node=(int)Get(Policy,"SelectedNode");float observedAt=(float)Get(Policy,"LastObservedAt");
            object deadline=Field(Policy,"predictionDeadline").GetValue(Policy);
            foreach(var candidate in(IList)Get(Policy,"Candidates"))
            {
                var route=(Vector3[])Value(candidate,"DroneRoute");float distance=0f;
                for(int i=1;i<route.Length;i++)distance+=Vector3.Distance(route[i-1],route[i]);
                Assert.That((float)Value(candidate,"DroneArrival"),Is.GreaterThanOrEqualTo(distance/agent.speed-.001f));
            }
            MovePlayer(new Vector3(-1000,0,-1000));yield return new WaitForSeconds(.05f);yield return Emit();Tick();
            Assert.That(Get(Policy,"LastObservedAt"),Is.EqualTo(observedAt));Assert.That(Get(Policy,"SelectedNode"),Is.EqualTo(node));
            Assert.That(Field(Policy,"predictionDeadline").GetValue(Policy),Is.EqualTo(deadline));Assert.That(SoundAction,Is.False);
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(3.7f).Within(.001f));
        }
        [UnityTest]public IEnumerator SolidCoverAndMovingHiddenPlayer_DoNotRefreshPredictionHistory()
        {
            yield return VisibleMotion();Vector3 last=(Vector3)Get(Policy,"LastObserved");float at=(float)Get(Policy,"LastObservedAt");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);
            Vector3 sight=player.transform.position-brain.transform.position;sight.y=0;
            wall.transform.position=(brain.transform.position+player.transform.position)*.5f+Vector3.up*1.5f;
            wall.transform.rotation=Quaternion.LookRotation(sight);wall.transform.localScale=new Vector3(4,3,.2f);Physics.SyncTransforms();
            Assert.That(Call(brain,"CanSeePlayer"),Is.False);Tick();MovePlayer(new Vector3(-1000,0,-1000));
            yield return new WaitForSeconds(.7f);Tick();Assert.That(Get(Policy,"LastObserved"),Is.EqualTo(last));Assert.That(Get(Policy,"LastObservedAt"),Is.EqualTo(at));
            MovePlayer(new Vector3(1000,0,1000));Tick();Assert.That(Get(Policy,"LastObservedAt"),Is.EqualTo(at));
            yield return new WaitForSeconds(8f);Tick();Assert.That(((IList)Get(Policy,"History")).Count,Is.Zero);Assert.That(Get(Policy,"SelectedNode"),Is.EqualTo(-1));
            Assert.That(State,Is.EqualTo("ReturnHome").Or.EqualTo("WatchHome"));Assert.That(SoundAction,Is.False);
        }
        [UnityTest]public IEnumerator GenuineObservedReversal_ReplansAndClearsTacticalArc()
        {
            yield return VisibleMotion();var history=(IList)Get(Policy,"History");
            Vector3 direction=(Vector3)Value(history[history.Count-1],"Direction");yield return new WaitForSeconds(.3f);
            MovePlayer(player.transform.position-direction*.6f);brain.transform.rotation=Quaternion.LookRotation(player.transform.position-brain.transform.position);Physics.SyncTransforms();
            Assert.That(Call(brain,"CanSeePlayer"),Is.True);Tick();
            Assert.That(Get(Policy,"SelectedNode"),Is.EqualTo(-1));Assert.That(Get(Policy,"Reason").ToString(),Does.Contain("reversed"));
            Assert.That(Get(Get(brain,"TacticalNavigation"),"Plan"),Is.Null);Assert.That(SoundAction,Is.False);
        }
        [UnityTest]public IEnumerator RuntimeProfileAndDeath_PreserveConfiguredCapsAndTerminalShutdown()
        {
            var health=brain.GetComponent(T("Health"));Assert.That(Get(health,"Maximum"),Is.EqualTo(125f));Assert.That(Get(health,"Current"),Is.EqualTo(125f));
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(2.5f).Within(.001f));Assert.That(agent.acceleration,Is.EqualTo(8f));
            yield return Emit();Tick();yield return new WaitForSeconds(.5f);Tick();Assert.That(SoundAction,Is.True);
            Call(health,"TakeDamage",10000f);Assert.That(SoundAction,Is.False);Assert.That(Get(awareness,"IsEnabled"),Is.False);
            Assert.That(((Behaviour)hearing).enabled,Is.False);Assert.That(((Behaviour)motor).enabled,Is.False);Assert.That(Get(motor,"CruiseSpeed"),Is.EqualTo(0f));
        }
    }
}
#endif
