#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class AwarenessAnimationPlayModeTests
    {
        private Component[] brains,visuals;
        private Component player;
        private float capture;
        private int rate;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o,string n)=>o.GetType().GetField(n).GetValue(o);
        private static FieldInfo Field(object o,string n)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(f!=null)return f;}
            throw new MissingFieldException(n);
        }
        private static object Call(object o,string n,params object[] args)
        {
            for(Type t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}
            throw new MissingMethodException(n);
        }
        private static object Pose(Component v)=>Field(v,"pose").GetValue(v);
        private static float P(Component v,string n)=>(float)Get(Pose(v),n);
        private static string State(Component v)=>Get(v,"State").ToString();
        private static object Awareness(Component b)=>Get(b,"Awareness");
        private static void Cue(Component brain,string state)
        {
            var a=Activator.CreateInstance(T("DroneAwareness"),Activator.CreateInstance(T("AwarenessSettings")),Get(brain,"DifficultySettings"));
            Field(brain,"<Awareness>k__BackingField").SetValue(brain,a);float now=Time.time;
            var sound=Activator.CreateInstance(T("SoundObservation"),brain.transform.position,Enum.Parse(T("NoiseCategory"),"Gunshot"),1f,1f,now,now+20);
            if(state=="Combat")Call(a,"Step",now,true,brain.transform.position,false,Activator.CreateInstance(T("SoundObservation")),false);
            else if(state!="Patrol")
            {
                Call(a,"Step",now,false,Vector3.zero,true,sound,false);
                if(state!="Suspicious")Call(a,"Step",now+.5f,false,Vector3.zero,true,sound,false);
                if(state=="Search"||state=="ReturnToPost")Call(a,"Step",now+3f,false,Vector3.zero,true,sound,false);
                if(state=="ReturnToPost")Call(a,"Step",now+9f,false,Vector3.zero,false,sound,false);
            }
            Assert.That(Get(a,"State").ToString(),Is.EqualTo(state));
        }
        private static string Output=>Environment.GetEnvironmentVariable("SHIFTFOUR_ANIMATION_EVIDENCE")??"Logs/AwarenessAnimation";
        [UnitySetUp]public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime;rate=Application.targetFrameRate;cursor=Cursor.lockState;cursorVisible=Cursor.visible;
            Application.targetFrameRate=60;Time.captureDeltaTime=1f/60;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            brains=Object.FindObjectsByType(T("DroneBrain")).Cast<Component>().OrderBy(b=>b.name).ToArray();Assert.That(brains.Length,Is.EqualTo(4));
            visuals=brains.Select(b=>b.GetComponentInChildren(T("DroneRotorVisuals"))).ToArray();player=(Component)Object.FindFirstObjectByType(T("PlayerController"));((Behaviour)player).enabled=false;
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=Vector3.one*-1000;cc.enabled=true;Call(player,"ApplyCameraPose");
            foreach(var b in brains){((Behaviour)b).enabled=false;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=false;b.GetComponent<NavMeshAgent>().enabled=false;Cue(b,"Patrol");}
            yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("After awareness animation"));yield return SceneManager.UnloadSceneAsync(old);
            Time.captureDeltaTime=capture;Application.targetFrameRate=rate;Cursor.lockState=cursor;Cursor.visible=cursorVisible;
        }
        [UnityTest]public IEnumerator AllFour_AwarenessTransitionsUsePropertyBlocksAndPreserveMaterials()
        {
            var materials=visuals.Select(v=>v.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray()).ToArray();
            var counts=brains.Select(b=>b.GetComponentsInChildren<Transform>(true).Length).ToArray();
            foreach(string state in new[]{"Suspicious","Investigate","Search","Combat","ReturnToPost","Patrol"})
            {
                foreach(var b in brains)Cue(b,state);yield return new WaitForSeconds(1.3f);
                for(int i=0;i<4;i++)
                {
                    var v=visuals[i];Assert.That(Get(v,"AwarenessState").ToString(),Is.EqualTo(state));
                    Assert.That(P(v,"AlertBlend")>0,Is.EqualTo(state!="Patrol"&&state!="ReturnToPost"));
                    var slots=(IList)Field(v,"tints").GetValue(v);Assert.That(slots.Count,Is.GreaterThan(0));
                    foreach(object s in slots)
                    {
                        var block=new MaterialPropertyBlock();((Renderer)F(s,"Renderer")).GetPropertyBlock(block,(int)F(s,"Index"));
                        var color=block.GetColor((int)F(s,"Property"));var original=(Color)F(s,"Color");
                        Assert.That(color.a,Is.EqualTo(original.a));
                        Assert.That(Vector3.Distance(new Vector3(color.r,color.g,color.b),new Vector3(original.r,original.g,original.b)),Is.LessThan(.5f));
                        if(state=="Patrol")Assert.That(Vector4.Distance(color,original),Is.LessThan(.001f));
                    }
                    CollectionAssert.AreEqual(materials[i],v.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).ToArray());
                    Assert.That(brains[i].GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(counts[i]));
                    Assert.That(Get(Get(v,"Effects"),"FlashVisible"),Is.False,"Combat cue cannot fabricate an accepted shot");
                }
            }
        }
        [UnityTest]public IEnumerator AllFour_ActualMotorAccelerationTurningAndBrakingDrivePose()
        {
            string csv="agent,peak_acceleration,peak_rotor,bank,braking,travel\n";
            for(int i=0;i<4;i++)
            {
                var b=brains[i];var v=visuals[i];var motor=b.GetComponent(T("DroneMotor"));var agent=b.GetComponent<NavMeshAgent>();var body=b.GetComponent(T("DroneBody"));
                Vector3 home=b.transform.position;agent.enabled=true;Assert.That(agent.Warp(new Vector3(3,0,4.5f)),Is.True);b.transform.rotation=Quaternion.LookRotation(Vector3.right);yield return null;
                Cue(b,"Combat");Call(motor,"SetMovementMode",Enum.Parse(T("DroneMovementMode"),"Chase"));((Behaviour)motor).enabled=true;Call(motor,"GoTo",new Vector3(3,0,15.5f));
                float acceleration=0,rotor=0,bank=0,travel=0;bool braking=false;Vector3 last=b.transform.position;float until=Time.time+14;
                while(Time.time<until)
                {
                    yield return null;travel+=Vector3.Distance(last,b.transform.position);last=b.transform.position;
                    acceleration=Mathf.Max(acceleration,P(v,"ForwardAcceleration"));rotor=Mathf.Max(rotor,P(v,"RotorSpeed"));bank=Mathf.Max(bank,Mathf.Abs(P(v,"Bank")));braking|=State(v)=="Braking";
                    Assert.That(Get(v,"MovementVelocity"),Is.EqualTo(Get(motor,"ActualVelocity")));
                    Assert.That((bool)Call(body,"CanOccupy",b.transform.position),Is.True);Assert.That(Quaternion.Angle(v.transform.localRotation,(Quaternion)Field(v,"restRotation").GetValue(v)),Is.LessThanOrEqualTo(4.0002f));
                    if(Field(motor,"state").GetValue(motor).ToString()=="Arrived")break;
                }
                Assert.That(acceleration,Is.GreaterThan(.05f),b.name);Assert.That(rotor,Is.GreaterThan(.85f));Assert.That(bank,Is.GreaterThan(.1f));Assert.That(braking,Is.True);Assert.That(travel,Is.InRange(9f,14f));
                yield return new WaitForSeconds(1.5f);Assert.That(Mathf.Abs(P(v,"Pitch")),Is.LessThan(.01f));
                csv+=string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F4},{2:F4},{3:F4},{4},{5:F4}\n",b.name,acceleration,rotor,bank,braking,travel);
                ((Behaviour)motor).enabled=false;agent.isStopped=true;agent.ResetPath();agent.Warp(home);agent.enabled=false;yield return null;
            }
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"actual-movement.csv"),csv);
        }
        [UnityTest]public IEnumerator AllFour_ScanningAt30_60_120HzPreservesRootHeightScaleAndGeometry()
        {
            var verts=new Dictionary<Mesh,Vector3[]>();
            foreach(var v in visuals)foreach(var m in v.GetComponentsInChildren<MeshFilter>())
            {
                if(verts.ContainsKey(m.sharedMesh))continue;using var data=MeshUtility.AcquireReadOnlyMeshData(m.sharedMesh);using var values=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp);data[0].GetVertices(values);verts[m.sharedMesh]=values.ToArray();
            }
            var roots=brains.Select(b=>b.transform.position).ToArray();var rotations=brains.Select(b=>b.transform.rotation).ToArray();var scales=visuals.Select(v=>v.transform.localScale).ToArray();
            float maximum=0;
            foreach(int fps in new[]{30,60,120})
            {
                Time.captureDeltaTime=1f/fps;foreach(var b in brains)Cue(b,"Search");
                for(int frame=0;frame<fps*2;frame++)
                {
                    yield return null;
                    for(int i=0;i<4;i++)
                    {
                        var v=visuals[i];maximum=Mathf.Max(maximum,Mathf.Abs(P(v,"ScanYaw")));
                        Assert.That(brains[i].transform.position,Is.EqualTo(roots[i]));Assert.That(brains[i].transform.rotation,Is.EqualTo(rotations[i]));Assert.That(v.transform.localScale,Is.EqualTo(scales[i]));
                        Vector3 rest=(Vector3)Field(v,"restPosition").GetValue(v);Assert.That(Mathf.Abs(v.transform.localPosition.y-rest.y),Is.LessThanOrEqualTo(.02001f));
                        if(frame%(fps/6)!=0)continue;
                        var body=brains[i].GetComponent(T("DroneBody"));var box=new Bounds(brains[i].transform.position+(Vector3)Get(body,"EnvelopeCenter"),(Vector3)Get(body,"EnvelopeExtents")*2);
                        foreach(var m in v.GetComponentsInChildren<MeshFilter>())foreach(var point in verts[m.sharedMesh])
                        {Vector3 p=m.transform.TransformPoint(point)-box.center;Assert.That(Mathf.Abs(p.x),Is.LessThanOrEqualTo(box.extents.x+.0001f));Assert.That(Mathf.Abs(p.y),Is.LessThanOrEqualTo(box.extents.y+.0001f));Assert.That(Mathf.Abs(p.z),Is.LessThanOrEqualTo(box.extents.z+.0001f));}
                    }
                }
            }
            Assert.That(maximum,Is.InRange(.9f,1.201f));
        }
        [UnityTest]public IEnumerator FatalDestructionCancelsCombatCueFireAndHitWithoutRestartingRotors()
        {
            foreach(var b in brains)Cue(b,"Combat");yield return new WaitForSeconds(.5f);
            // Exercise one genuine accepted shot through unchanged combat checks.
            var b0=brains[0];var v0=visuals[0];var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(1000,0,1000);cc.enabled=true;Call(player,"ApplyCameraPose");
            b0.transform.position=player.transform.position+Vector3.forward*5;b0.transform.rotation=Quaternion.LookRotation(Vector3.back);Physics.SyncTransforms();Field(b0,"nextShot").SetValue(b0,0f);Call(b0,"TryShoot");
            Assert.That(Get(Get(v0,"Effects"),"FlashVisible"),Is.True);Assert.That(Get(Get(v0,"Effects"),"TracerVisible"),Is.True);
            foreach(var b in brains)Call(b.GetComponent(T("Health")),"TakeDamage",5f);yield return null;
            foreach(var v in visuals)Assert.That(State(v),Is.EqualTo("Damaged"));
            foreach(var b in brains)Call(b.GetComponent(T("Health")),"TakeDamage",10000f);
            foreach(var v in visuals){Assert.That(P(v,"AlertBlend"),Is.Zero);Assert.That(P(v,"ScanYaw"),Is.Zero);Assert.That(Get(Get(v,"Effects"),"FlashVisible"),Is.False);Assert.That(Get(Get(v,"Effects"),"TracerVisible"),Is.False);Assert.That(P(v,"RotorSpeed"),Is.GreaterThan(0));}
            yield return new WaitForSeconds(3);
            foreach(var v in visuals)
            {
                Assert.That(P(v,"RotorSpeed"),Is.Zero);Assert.That(P(v,"AlertBlend"),Is.Zero);Assert.That(Get(v,"DestructionCount"),Is.EqualTo(1));
                ((Behaviour)v).enabled=false;((Behaviour)v).enabled=true;Assert.That(P(v,"RotorSpeed"),Is.Zero);
            }
        }
        [UnityTest]public IEnumerator DisableRestoresPropertyBlocksAndRestPose_WithoutDuplicateWriters()
        {
            foreach(var b in brains)Cue(b,"Search");yield return new WaitForSeconds(1);
            foreach(var v in visuals)
            {
                int count=v.GetComponentsInChildren<Transform>().Length;var root=v.GetComponentInParent(T("DroneBrain"));Vector3 position=root.transform.position;
                ((Behaviour)v).enabled=false;Assert.That(v.transform.localPosition,Is.EqualTo(Field(v,"restPosition").GetValue(v)));Assert.That(v.transform.localRotation,Is.EqualTo(Field(v,"restRotation").GetValue(v)));
                foreach(object s in (IList)Field(v,"tints").GetValue(v)){var block=new MaterialPropertyBlock();((Renderer)F(s,"Renderer")).GetPropertyBlock(block,(int)F(s,"Index"));Assert.That(block.isEmpty,Is.EqualTo(((MaterialPropertyBlock)F(s,"Original")).isEmpty));}
                ((Behaviour)v).enabled=true;Assert.That(v.GetComponentsInChildren<Transform>().Length,Is.EqualTo(count));Assert.That(root.transform.position,Is.EqualTo(position));Assert.That(v.GetComponentsInParent(T("DroneRotorVisuals")).Length,Is.EqualTo(1));
            }
        }
        [UnityTest]public IEnumerator TeleportsAndDisabledAwareness_CannotCreateAccelerationOrAlert()
        {
            foreach(var b in brains)Cue(b,"Combat");yield return new WaitForSeconds(.5f);
            foreach(var b in brains){Call(Awareness(b),"Disable");b.transform.position+=Vector3.right*100;}
            yield return null;foreach(var v in visuals)Assert.That(P(v,"ForwardAcceleration"),Is.Zero);
            yield return new WaitForSeconds(1.5f);foreach(var v in visuals){Assert.That(P(v,"AlertBlend"),Is.Zero);Assert.That(Mathf.Abs(P(v,"ScanYaw")),Is.LessThan(.001f));}
        }
        [UnityTest]public IEnumerator AllAwarenessStates_WarmedPresentationHasNoManagedAllocations()
        {
            var ticks=visuals.Select(v=>(Action)Delegate.CreateDelegate(typeof(Action),v,v.GetType().GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance))).ToArray();
            long calibration=MeasuredAllocation.Calibrate();string csv="state,ticks,allocation_events,samples,milliseconds,calibration_events\n";
            foreach(string state in new[]{"Patrol","Suspicious","Investigate","Search","Combat","ReturnToPost"})
            {
                foreach(var b in brains)Cue(b,state);foreach(var tick in ticks)tick();
                Action work=()=>{for(int i=0;i<1000;i++)foreach(var tick in ticks)tick();};var timer=System.Diagnostics.Stopwatch.StartNew();long events=MeasuredAllocation.Events(work,out int samples);timer.Stop();
                Assert.That(events,Is.Zero,state);csv+=string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},4000,{1},{2},{3:F3},{4}\n",state,events,samples,timer.Elapsed.TotalMilliseconds,calibration);
            }
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"allocations.csv"),csv);yield return null;
        }
        [UnityTest]public IEnumerator RenderedAllFour_ShowDistinctAwarenessCuesWithOriginalModelColors()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));Directory.CreateDirectory(Output);
            string csv="agent,phase,alert_strength,scan_yaw,rotor,pitch,bank\n";
            for(int i=0;i<4;i++)foreach(string state in new[]{"Patrol","Suspicious","Search","Combat"})
            {
                Cue(brains[i],state);yield return new WaitForSeconds(1);
                csv+=string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}\n",brains[i].name,state,P(visuals[i],"AlertBlend"),P(visuals[i],"ScanYaw"),P(visuals[i],"RotorSpeed"),P(visuals[i],"Pitch"),P(visuals[i],"Bank"));
                yield return Capture(brains[i],state);
            }
            File.WriteAllText(Path.Combine(Output,"rendered-cues.csv"),csv);
        }
        private IEnumerator Capture(Component brain,string state)
        {
            var go=new GameObject("Temporary awareness evidence camera");var camera=go.AddComponent<Camera>();camera.fieldOfView=40;camera.nearClipPlane=.05f;
            Vector3 target=brain.transform.position+Vector3.up*1.65f;bool clear=false;
            for(int yaw=0;yaw<360;yaw+=45)
            {
                Vector3 point=brain.transform.position+Quaternion.Euler(0,yaw,0)*(brain.transform.forward*3.5f+brain.transform.right*2.5f)+Vector3.up*2.7f;
                Vector3 delta=point-target;
                if(Physics.SphereCastAll(target,.65f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.transform.IsChildOf(brain.transform)))continue;
                camera.transform.position=point;clear=true;break;
            }
            Assert.That(clear,Is.True,brain.name+" needs an unobstructed evidence camera; do not move room geometry");camera.transform.LookAt(target);
            var render=new RenderTexture(960,540,24);render.Create();camera.targetTexture=render;yield return null;
            var old=RenderTexture.active;RenderTexture.active=render;var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();using var writer=new BinaryWriter(File.Create(Path.Combine(Output,brain.name+"-"+state+".tga")));
                writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0});writer.Write((short)960);writer.Write((short)540);writer.Write((byte)24);writer.Write((byte)0);
                foreach(var c in pixels.GetPixels32()){writer.Write(c.b);writer.Write(c.g);writer.Write(c.r);}
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;render.Release();Object.Destroy(render);Object.Destroy(pixels);Object.Destroy(go);}
        }
    }
}
#endif
