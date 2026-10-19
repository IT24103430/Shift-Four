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
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class TacticalFlankerPlayModeTests
    {
        private Component brain, player, motor, graph, body;
        private NavMeshAgent agent;
        private object awareness, hearing;
        private float capture;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private readonly List<GameObject> temporary = new List<GameObject>();
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name)).Cast<Component>().ToArray();
        private static object Get(object o,string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o,string n) => o.GetType().GetField(n).GetValue(o);
        private static FieldInfo Field(object o,string name)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType)
            { var f=t.GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(f!=null)return f; }
            throw new MissingFieldException(name);
        }
        private static object Call(object o,string name,params object[] args)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType)
            {var m=t.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}
            throw new MissingMethodException(name);
        }
        private Vector3 Home => (Vector3)Get(brain,"Home");
        private object Policy => Get(brain,"Policy");
        private object Routes => Get(brain,"Routes");
        private int Side => (int)Get(Policy,"SelectedSide");
        private void Tick() { Field(brain,"nextDecision").SetValue(brain,0f);Call(brain,"Update"); }
        private void MovePlayer(Vector3 point)
        {
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=point;cc.enabled=true;
            Call(player,"ApplyCameraPose");Physics.SyncTransforms();
        }
        private void RouteBegin() => Call(Routes,"Begin",Home);
        private Vector3[] Route(Vector3 from,Vector3 to)=>(Vector3[])Call(Routes,"Route",from,to);
        private bool Occupy(Vector3 point)=>(bool)Call(Routes,"CanOccupy",point);
        private bool Visible => (bool)Call(brain,"CanSeePlayer");
        [UnitySetUp] public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;Time.captureDeltaTime=1f/30f;cursor=Cursor.lockState;cursorVisible=Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            foreach(var b in Find("DroneBrain"))
            {
                ((Behaviour)b).enabled=false;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=false;
                var nav=b.GetComponent<NavMeshAgent>();nav.ResetPath();nav.isStopped=true;nav.velocity=Vector3.zero;
            }
            brain=Find("FlankerBrain").Single();player=Find("PlayerController").Single();((Behaviour)player).enabled=false;
            motor=brain.GetComponent(T("DroneMotor"));body=brain.GetComponent(T("DroneBody"));agent=brain.GetComponent<NavMeshAgent>();graph=Find("WaypointGraph").Single();
            awareness=Activator.CreateInstance(T("DroneAwareness"),Activator.CreateInstance(T("AwarenessSettings")),Get(brain,"DifficultySettings"));
            Field(brain,"<Awareness>k__BackingField").SetValue(brain,awareness);Field(brain,"policy").SetValue(brain,null);
            Field(brain,"lastSeenAt").SetValue(brain,float.NegativeInfinity);Field(brain,"nextShot").SetValue(brain,float.PositiveInfinity);
            hearing=Get(brain,"Hearing");Call(hearing,"Configure",graph,1f);MovePlayer(Vector3.one*-1000);Tick();
            Assert.That(Get(Policy,"VisualObservations"),Is.Zero);Assert.That(Occupy(brain.transform.position),Is.True,"Authored clear Flanker spawn");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var go in temporary)if(go!=null)Object.Destroy(go);temporary.Clear();
            var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("After tactical Flanker"));yield return SceneManager.UnloadSceneAsync(scene);
            Time.captureDeltaTime=capture;Cursor.lockState=cursor;Cursor.visible=cursorVisible;
        }
        private void VisiblePair(bool both=true,bool isolatedBlock=false)
        {
            Vector3[] starts={new Vector3(0,0,4.4f),new Vector3(-5,0,5),new Vector3(5,0,5),new Vector3(-5,0,-5),new Vector3(5,0,-5),new Vector3(0,0,-5),new Vector3(-7,0,-6),new Vector3(7,0,-6),new Vector3(-7,0,6),new Vector3(7,0,6),new Vector3(-7,0,0),new Vector3(7,0,0)};
            foreach(var offset in starts)
            {
                MovePlayer(Vector3.one*-1000);if(!NavMesh.SamplePosition(Home+offset,out var root,.1f,agent.areaMask)||!Occupy(root.position))continue;
                Assert.That(agent.Warp(root.position),Is.True);Physics.SyncTransforms();
                foreach(float x in new[]{0f,-2f,2f,-4f,4f})foreach(float z in new[]{-6f,-3f,0f,3f,6f})
                {
                    var point=Home+new Vector3(x,0,z);float distance=Vector3.Distance(root.position,point);if(distance<5f||distance>12f)continue;
                    if(!NavMesh.SamplePosition(point,out var hit,.1f,agent.areaMask))continue;
                    MovePlayer(hit.position);brain.transform.rotation=Quaternion.LookRotation((hit.position-root.position).normalized,Vector3.up);Physics.SyncTransforms();
                    if(!Visible)continue;
                    Field(brain,"policy").SetValue(brain,null);Tick();
                    var candidates=((IEnumerable)Get(Policy,"Candidates")).Cast<object>().ToArray();
                    if(Side==0||both&&candidates.Length!=2)continue;
                    if(candidates.Any(c=>Vector3.Distance((Vector3)F(c,"Position"),root.position)<3.6f))continue;
                    if(isolatedBlock)
                    {
                        object selected=candidates.First(c=>(int)F(c,"Side")==Side);
                        object other=candidates.First(c=>(int)F(c,"Side")!=Side);
                        BlockCandidate(selected);var obstacle=temporary.Last();RouteBegin();
                        if(Route(brain.transform.position,(Vector3)F(other,"Position"))==null)
                        {obstacle.SetActive(false);Physics.SyncTransforms();continue;}
                    }
                    Assert.That(Get(Policy,"LastVisual"),Is.EqualTo(player.transform.position));return;
                }
            }
            Assert.Fail("MainLab requires a real visible player and body-safe side approaches");
        }
        private object SelectedCandidate => ((IEnumerable)Get(Policy,"Candidates")).Cast<object>().First(c=>(int)F(c,"Side")==Side);
        private void BlockCandidate(object c,float size=.4f)
        {
            var go=new GameObject("Temporary solid failed flank");temporary.Add(go);go.transform.position=(Vector3)F(c,"Position")+Vector3.up*1.6f;
            go.AddComponent<BoxCollider>().size=new Vector3(size,3f,size);Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator MainLab_LeftAndRightAreReachable_UseNativeAStarAndCombatStandoff()
        {
            VisiblePair();Assert.That(((IList)Get(Policy,"Candidates")).Count,Is.EqualTo(2));
            foreach(object c in (IEnumerable)Get(Policy,"Candidates"))
            {
                var point=(Vector3)F(c,"Position");Assert.That(Occupy(point),Is.True);Assert.That(Route(brain.transform.position,point),Is.Not.Null);
                Assert.That(Vector3.Distance(point,(Vector3)Get(Policy,"LastVisual")),Is.GreaterThanOrEqualTo(2.5f));
                var route=(Vector3[])F(c,"Route");float length=0;for(int i=1;i<route.Length;i++)length+=Vector3.Distance(route[i-1],route[i]);
                Assert.That(F(c,"Cost"),Is.EqualTo(length).Within(.001f));
            }
            yield return TacticalFrame("Visible-two-flanks");
        }
        [UnityTest] public IEnumerator MainLab_OneSideSolid_OverridesCommitAndSelectsOtherSide()
        {
            VisiblePair(isolatedBlock:true);int old=Side;Tick();
            Assert.That(Side,Is.EqualTo(-old));Assert.That((int)Get(Policy,"FailedAttempts"),Is.GreaterThan(0));yield return TacticalFrame("One-flank-blocked");
        }
        [UnityTest] public IEnumerator MainLab_BothSidesSolid_RejectsFlanksAndUsesSafeFallback()
        {
            VisiblePair();foreach(object c in (IEnumerable)Get(Policy,"Candidates"))BlockCandidate(c,4f);Tick();
            Assert.That(Side,Is.Zero);Assert.That(Get(brain,"CurrentDecision").ToString(),Does.Not.Contain("Flank"));
            var route=(Vector3[])Get(Policy,"PlannedRoute");if(route.Length>0)Assert.That(Occupy(route[route.Length-1]),Is.True);yield return TacticalFrame("Both-flanks-blocked");
        }
        [UnityTest] public IEnumerator MainLab_ActualVisibleReversal_ReplansWithoutHiddenRefresh()
        {
            VisiblePair();Vector3 start=player.transform.position;
            Vector3 direction=(brain.transform.position-start).normalized;
            yield return new WaitForSeconds(.3f);MovePlayer(start+direction*.6f);Assert.That(Visible,Is.True);Tick();
            int old=(int)Get(Policy,"Replans");yield return new WaitForSeconds(.3f);MovePlayer(start);Assert.That(Visible,Is.True);Tick();
            Assert.That((int)Get(Policy,"Replans"),Is.GreaterThan(old));var visual=Get(Policy,"LastVisual");var velocity=Get(Policy,"VisualVelocity");var count=Get(Policy,"VisualObservations");
            MovePlayer(new Vector3(-999,0,999));Tick();Assert.That(Get(Policy,"LastVisual"),Is.EqualTo(visual));Assert.That(Get(Policy,"VisualVelocity"),Is.EqualTo(velocity));Assert.That(Get(Policy,"VisualObservations"),Is.EqualTo(count));
        }
        [UnityTest] public IEnumerator MainLab_CoverBlocksVisionAndFire_WithoutRefreshingLastSight()
        {
            VisiblePair();var last=Get(Policy,"LastVisual");float seen=(float)Get(Policy,"LastVisualAt");float hp=(float)Get(player.GetComponent(T("Health")),"Current");
            var go=new GameObject("Temporary real occluding cover");temporary.Add(go);go.transform.position=(player.transform.position+brain.transform.position)*.5f+Vector3.up*1.3f;
            go.AddComponent<BoxCollider>().size=new Vector3(1f,3f,5f);Physics.SyncTransforms();Assert.That(Visible,Is.False);Field(brain,"nextShot").SetValue(brain,0f);
            for(int i=0;i<6;i++){Tick();yield return new WaitForSeconds(.3f);}
            Assert.That(Get(player.GetComponent(T("Health")),"Current"),Is.EqualTo(hp));Assert.That(Get(Policy,"LastVisual"),Is.EqualTo(last));Assert.That(Get(Policy,"LastVisualAt"),Is.EqualTo(seen));
        }
        [UnityTest] public IEnumerator MainLab_LockedControlAndExitGates_HaveNoFlankRoute()
        {
            Call(graph,"SetGateOpen",2,false);Call(graph,"SetGateOpen",3,false);RouteBegin();
            Assert.That(Route(brain.transform.position,new Vector3(10,0,10)),Is.Null);Assert.That(Route(brain.transform.position,new Vector3(22,0,-15)),Is.Null);yield return null;
        }
        [UnityTest] public IEnumerator MainLab_RepeatedFailedEndpoint_IsNotImmediatelyReselected()
        {
            VisiblePair();int old=Side;BlockCandidate(SelectedCandidate);Tick();int failed=(int)Get(Policy,"FailedAttempts");
            foreach(var go in temporary)go.SetActive(false);Physics.SyncTransforms();
            for(int i=0;i<4;i++){yield return new WaitForSeconds(.3f);Tick();Assert.That(Side,Is.EqualTo(-old));}
            Assert.That(Get(Policy,"FailedAttempts"),Is.EqualTo(failed));Assert.That((int)Get(Policy,"Switches"),Is.LessThanOrEqualTo(1));
        }
        [UnityTest] public IEnumerator MainLab_ChaseCapAndAcceptedMovement_AreSmoothAndBodyClear()
        {
            VisiblePair(false);Assert.That(Get(brain.GetComponent(T("Health")),"Current"),Is.EqualTo(150f));Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(4.2f));
            ((Behaviour)motor).enabled=true;float peak=0,travel=0;Vector3 previous=brain.transform.position;float cap=agent.speed;float next=Time.time;
            for(int i=0;i<120;i++)
            {
                if(Time.time>=next){Tick();next=Time.time+.3f;}yield return null;
                float step=Vector3.Distance(previous,brain.transform.position);travel+=step;peak=Mathf.Max(peak,step/Time.deltaTime);
                Assert.That(step,Is.LessThanOrEqualTo(agent.speed*Time.deltaTime+.02f));Assert.That(Mathf.Abs(agent.speed-cap),Is.LessThanOrEqualTo(agent.acceleration*Time.deltaTime+.005f));
                Assert.That((bool)Call(body,"CanOccupy",brain.transform.position),Is.True,"Raised body/player clearance during real chase");previous=brain.transform.position;cap=agent.speed;
            }
            Assert.That(agent.speed,Is.EqualTo(4.2f).Within(.01f));Assert.That(peak,Is.GreaterThan(3.3f));Assert.That(travel,Is.GreaterThan(3f));
        }
        [UnityTest] public IEnumerator MainLab_ExpiredVisualMemory_ReturnsToReachableStoragePost()
        {
            VisiblePair();MovePlayer(Vector3.one*-1000);yield return new WaitForSeconds((float)Field(brain,"memorySeconds").GetValue(brain)+.3f);Tick();
            Assert.That(Get(brain,"CurrentDecision").ToString(),Does.Contain("Storage post"));Assert.That(Side,Is.Zero);
            ((Behaviour)motor).enabled=true;float until=Time.time+12f;
            while(Time.time<until){Tick();if(Vector3.Distance(brain.transform.position,((Vector3[])Get(Policy,"PlannedRoute")).Last())<1.2f)break;yield return new WaitForSeconds(.3f);}
            Assert.That(Vector3.Distance(brain.transform.position,Home),Is.LessThanOrEqualTo(4.7f));Assert.That(Occupy(brain.transform.position),Is.True);Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(2.8f));
        }
        private IEnumerator EmitReachableSound()
        {
            foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                T("SoundEvents").GetMethod("ReportShot").Invoke(null,new object[]{brain.transform.position+direction*3f});
                object[] args={null};if((bool)Call(hearing,"TryGetLatest",args))
                { RouteBegin();if(Route(brain.transform.position,(Vector3)F(args[0],"Position"))!=null)yield break; }
                yield return null;
            }
            Assert.Fail("Storage sound fixture needs a reachable approximate clue");
        }
        [UnityTest] public IEnumerator MainLab_SoundOnly_DemonstratesInvestigationSearchAndReturn()
        {
            string dir=Environment.GetEnvironmentVariable("SHIFTFOUR_FLANKER_EVIDENCE")??"Logs/TacticalFlanker";Directory.CreateDirectory(dir);
            var rows=new List<string>{"seconds,awareness,decision,sound,visual_count,x,z,goal_x,goal_z,cap"};var phases=new HashSet<string>();
            float started=Time.time;yield return EmitReachableSound();int soundTicks=0;float travelled=0;Vector3 previous=brain.transform.position;
            float until=Time.time+25f;
            while(Time.time<until)
            {
                Tick();bool sound=(bool)Get(Policy,"UsingSound");if(sound)soundTicks++;
                string state=Get(awareness,"State").ToString();Vector3 goal=(Vector3)Field(motor,"goal").GetValue(motor);
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:F3},{1},\"{2}\",{3},{4},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3}",
                    Time.time-started,state,Get(brain,"CurrentDecision"),sound,Get(Policy,"VisualObservations"),brain.transform.position.x,brain.transform.position.z,goal.x,goal.z,agent.speed));
                if(phases.Add(state))yield return Frame(dir,state,goal);
                travelled+=Vector3.Distance(previous,brain.transform.position);previous=brain.transform.position;((Behaviour)motor).enabled=true;
                if(Time.time-started>6&&!sound&&Get(brain,"CurrentDecision").ToString().Contains("Storage post")&&Vector3.Distance(brain.transform.position,(Vector3)Get(Policy,"PostDestination"))<1.2f)break;
                yield return new WaitForSeconds(.3f);
            }
            File.WriteAllLines(Path.Combine(dir,"MainLab-demo.csv"),rows);
            Assert.That(soundTicks,Is.GreaterThanOrEqualTo(3));Assert.That(travelled,Is.GreaterThan(1f));Assert.That(Get(Policy,"VisualObservations"),Is.Zero);
            Assert.That(Get(Policy,"LastVisualAt"),Is.EqualTo(float.NegativeInfinity));Assert.That(Get(Policy,"VisualVelocity"),Is.EqualTo(Vector3.zero));Assert.That(Get(awareness,"HasVisualConfirmation"),Is.False);
            Assert.That(Get(Policy,"UsingSound"),Is.False);Assert.That(Vector3.Distance(brain.transform.position,(Vector3)Get(Policy,"PostDestination")),Is.LessThan(1.2f));Assert.That(Vector3.Distance(brain.transform.position,Home),Is.LessThanOrEqualTo(4.7f));Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(2.8f));
        }
        private IEnumerator TacticalFrame(string phase)
        {
            string dir=Environment.GetEnvironmentVariable("SHIFTFOUR_FLANKER_EVIDENCE")??"Logs/TacticalFlanker";Directory.CreateDirectory(dir);
            var path=(Vector3[])Get(Policy,"PlannedRoute");var goal=path.Length>0?path.Last():brain.transform.position;
            string csv="phase,decision,side,x,z,target_x,target_z,goal_x,goal_z,failed,switches\n";
            var target=(Vector3)Get(Policy,"LastVisual");
            csv+=string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},\"{1}\",{2},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9},{10}\n",
                phase,Get(brain,"CurrentDecision"),Side,brain.transform.position.x,brain.transform.position.z,target.x,target.z,goal.x,goal.z,Get(Policy,"FailedAttempts"),Get(Policy,"Switches"));
            File.WriteAllText(Path.Combine(dir,phase+".csv"),csv);yield return Frame(dir,phase,goal);
        }

        private IEnumerator Frame(string dir,string phase,Vector3 goal)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)yield break;
            var temp=new List<GameObject>();var path=Route(brain.transform.position,goal);Material material=null;
            if(path!=null&&path.Length>1)
            {
                var line=new GameObject("Temporary validated flank route");temp.Add(line);var renderer=line.AddComponent<LineRenderer>();material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",(bool)Get(Policy,"UsingSound")?new Color(1,.55f,.1f):Color.cyan);
                renderer.sharedMaterial=material;renderer.widthMultiplier=.1f;renderer.positionCount=path.Length;renderer.SetPositions(path.Select(p=>p+Vector3.up*.1f).ToArray());
            }
            var cameraObject=new GameObject("Temporary Storage test camera");temp.Add(cameraObject);var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=10.5f;camera.nearClipPlane=.05f;
            camera.transform.position=Home+Vector3.up*2.8f;camera.transform.rotation=Quaternion.Euler(90,0,0);var render=new RenderTexture(960,960,24);render.Create();camera.targetTexture=render;yield return null;
            var previous=RenderTexture.active;RenderTexture.active=render;var pixels=new Texture2D(960,960,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,960,960),0,0);pixels.Apply();using var writer=new BinaryWriter(File.Create(Path.Combine(dir,phase+".tga")));
                writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0});writer.Write((short)960);writer.Write((short)960);writer.Write((byte)24);writer.Write((byte)0);
                foreach(var c in pixels.GetPixels32()){writer.Write(c.b);writer.Write(c.g);writer.Write(c.r);}
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;render.Release();Object.Destroy(render);Object.Destroy(pixels);if(material!=null)Object.Destroy(material);foreach(var go in temp)Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator MainLab_DeathIsTerminal_PreservesDamageGateAndAwarenessMechanics()
        {
            yield return EmitReachableSound();Tick();Call(brain.GetComponent(T("Health")),"ApplyDamage",150f,null,"Task 11 terminal death probe");
            Assert.That(Get(awareness,"IsEnabled"),Is.False);Assert.That(((Behaviour)hearing).enabled,Is.False);Assert.That(Get(motor,"CruiseSpeed"),Is.EqualTo(0f));
            Assert.That(brain.GetComponentsInChildren<Collider>().All(c=>!c.enabled),Is.True);Tick();Assert.That(Get(brain.GetComponent(T("Health")),"Current"),Is.EqualTo(0f));
        }
    }
}
#endif
