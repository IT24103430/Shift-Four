#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using Stopwatch=System.Diagnostics.Stopwatch;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShiftFour.Tests
{
    // Runtime-only QA fixtures. No authored scene is saved.
    public sealed partial class Student4UnifiedPlayModeTests
    {
        Component graph,player,session,interceptor; Component[] brains;
        float captureDelta; int targetRate; CursorLockMode cursor; bool cursorVisible;
        static Type T(string n,bool required=true)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",required);
        static Component[] Find(string n)=>Object.FindObjectsByType(T(n)).Cast<Component>().ToArray();
        static object Get(object o,string p)=>o.GetType().GetProperty(p).GetValue(o);
        static object Field(object o,string p)
        { for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(p,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(o);}throw new MissingFieldException(p); }
        static void Set(object o,string p,object v)
        { for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(p,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(o,v);return;}}throw new MissingFieldException(p); }
        static MethodInfo Method(object o,string p)
        { for(var t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(p,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(m!=null)return m;}return null; }
        static object Call(object o,string p,params object[] a)=>Method(o,p).Invoke(o,a);
        static string Out=>Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE")??"Logs/Student4QA";
        static void Write(string name,string text){Directory.CreateDirectory(Out);File.WriteAllText(Path.Combine(Out,name),text);}
        static string V(Vector3 p)=>string.Format(CultureInfo.InvariantCulture,"{0:F3}|{1:F3}|{2:F3}",p.x,p.y,p.z);
        List<Vector3> Route(Vector3 a,Vector3 b)=>(List<Vector3>)Call(graph,"FindRoute",a,b);
        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta=Time.captureDeltaTime;targetRate=Application.targetFrameRate;cursor=Cursor.lockState;cursorVisible=Cursor.visible;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            Time.captureDeltaTime=1f/60f;Application.targetFrameRate=60;
            graph=Find("WaypointGraph").Single();player=Find("PlayerController").Single();session=Find("GameSession").Single();brains=Find("DroneBrain");
            interceptor=brains.Single(b=>b.GetType().Name=="InterceptorBrain");
            foreach(var b in brains){((Behaviour)b).enabled=false;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=false;}
            ((Behaviour)player).enabled=false;yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime=captureDelta;Application.targetFrameRate=targetRate;
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("After Student4 QA"));yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState=cursor;Cursor.visible=cursorVisible;
        }
        [UnityTest] public IEnumerator All400GateEndpointQueries_MatchIndependentDijkstra()
        {
            int n=(int)Get(graph,"NodeCount");var nodes=Enumerable.Range(0,n).Select(i=>(Vector3)Call(graph,"NodePosition",i)).ToArray();var edges=(Array)Field(graph,"edges");
            string csv="mask,start,goal,expected_cost,actual_cost,returned_waypoints,result\n";int errors=0;
            for(int mask=0;mask<16;mask++)
            {
                for(int k=0;k<4;k++)Call(graph,"SetGateOpen",k,(mask&(1<<k))!=0);
                for(int start=0;start<n;start++)for(int goal=0;goal<n;goal++)
                {
                    var cost=Enumerable.Repeat(float.PositiveInfinity,n).ToArray();var visited=new bool[n];cost[start]=0;
                    for(int count=0;count<n;count++)
                    {
                        int at=-1;for(int i=0;i<n;i++)if(!visited[i]&&(at<0||cost[i]<cost[at]))at=i;
                        if(at<0||float.IsPositiveInfinity(cost[at]))break;visited[at]=true;
                        foreach(var edge in edges)
                        {
                            int a=(int)edge.GetType().GetField("from").GetValue(edge),b=(int)edge.GetType().GetField("to").GetValue(edge),gate=(int)edge.GetType().GetField("gateId").GetValue(edge);
                            if(gate>=0&&(mask&(1<<gate))==0)continue;int next=a==at?b:b==at?a:-1;
                            if(next>=0)cost[next]=Mathf.Min(cost[next],cost[at]+Vector3.Distance(nodes[at],nodes[next]));
                        }
                    }
                    var path=Route(nodes[start],nodes[goal]);float actual=0;Vector3 previous=nodes[start];
                    foreach(var p in path){actual+=Vector3.Distance(previous,p);previous=p;}
                    if(path.Count==0)actual=float.PositiveInfinity;
                    bool pass=(float.IsPositiveInfinity(cost[goal])&&float.IsPositiveInfinity(actual))||Mathf.Abs(cost[goal]-actual)<.001f;
                    if(!pass)errors++;
                    csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6}\n",mask,start,goal,cost[goal],actual,string.Join(";",path.Select(V)),pass?"PASS":"FAIL");
                    if(!float.IsPositiveInfinity(cost[goal]))Assert.That(Vector3.Distance(nodes[start],nodes[goal]),Is.LessThanOrEqualTo(cost[goal]+.001f),"admissible h");
                }
            }
            Write("graph-400-queries.csv",csv);Assert.That(errors,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator ConsoleAndPhysicalGate_PreserveExitHandoff()
        {
            var console=Find("ConsoleSwitch").Single(c=>(int)Get(c,"GateId")==2);var progress=Get(session,"Progress");int before=(int)Get(graph,"Version");
            Vector3 home=(Vector3)Get(interceptor,"Home"),storage=new Vector3(10,0,-3),approach=new Vector3(16.5f,0,14.5f);
            var native=new NavMeshPath();Assert.That(NavMesh.CalculatePath(new Vector3(3,0,10),approach,NavMesh.AllAreas,native)&&native.status==NavMeshPathStatus.PathComplete,Is.True);
            Vector3 eye=approach+Vector3.up*1.3f,aim=console.GetComponent<Collider>().bounds.center-eye;
            Assert.That(Physics.Raycast(eye,aim.normalized,out var hit,3f,~0,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent(T("ConsoleSwitch"))==console,Is.True);
            string csv="step,gate2,gate3,version,control_storage_path,ended\n";
            Action<string> row=step=>csv+=$"{step},{Call(graph,"IsGateOpen",2)},{Call(graph,"IsGateOpen",3)},{Get(graph,"Version")},{Route(home,storage).Count},{Get(session,"Ended")}\n";
            row("locked");Assert.That(Route(home,storage),Is.Empty);Call(console,"Interact");yield return new WaitForSeconds(2);row("console_only");Assert.That((bool)Call(graph,"IsGateOpen",2),Is.False);
            Call(interceptor.GetComponent(T("Health")),"TakeDamage",10000f);yield return new WaitForSeconds(2);row("drone_and_console");
            Assert.That((bool)Call(graph,"IsGateOpen",2),Is.True);Assert.That(Route(home,storage),Is.Not.Empty);Assert.That((int)Get(graph,"Version"),Is.GreaterThan(before));
            Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.False);
            for(int i=0;i<4;i++){Call(progress,"DefeatDrone",i);Call(progress,"ActivateConsole",i);}yield return new WaitForSeconds(2);
            row("all_objectives");Assert.That((bool)Call(graph,"IsGateOpen",3),Is.False);
            Call(session,"EnterRoom",3);Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.False);
            Call(Find("BombConsole").Single(),"Interact");yield return new WaitForSeconds(7f);
            Assert.That(Route(new Vector3(10,0,-10),new Vector3(22,0,-15)),Is.Not.Empty);
            Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.True);row("exit");Write("door-handoff.csv",csv);
        }
        [UnityTest] public IEnumerator RaisedBeam_ActualMotorCollisionAndRecovery(){yield return Obstacle(false);}
        [UnityTest] public IEnumerator Player_ActualMotorCollisionAndRecovery(){yield return Obstacle(true);}
        IEnumerator Obstacle(bool usePlayer)
        {
            var agent=interceptor.GetComponent<NavMeshAgent>();var motor=interceptor.GetComponent(T("DroneMotor"));
            Assert.That(agent.Warp(new Vector3(10,0,6)),Is.True);yield return null;
            Collider obstacle;GameObject fixture=null;
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=usePlayer?new Vector3(10,0,8):new Vector3(-10,0,-10);cc.enabled=true;
            if(usePlayer)obstacle=cc;
            else{fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="QA temporary raised beam";fixture.transform.position=new Vector3(10,1.3f,8);fixture.transform.localScale=new Vector3(19,.25f,.15f);obstacle=fixture.GetComponent<Collider>();}
            Physics.SyncTransforms();var native=new NavMeshPath();Assert.That(NavMesh.CalculatePath(interceptor.transform.position,new Vector3(10,0,10),NavMesh.AllAreas,native)&&native.status==NavMeshPathStatus.PathComplete,Is.True);
            var body=interceptor.GetComponent<Collider>();((Behaviour)motor).enabled=true;Call(motor,"GoTo",new Vector3(10,0,10));
            bool penetrated=false;float depth=0;string csv="frame,x,z,penetration_depth\n";
            for(int i=0;i<240;i++)
            {
                yield return null;Physics.SyncTransforms();bool overlap=Physics.ComputePenetration(body,body.transform.position,body.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out var direction,out float d);
                if(overlap&&d>.005f){penetrated=true;depth=Mathf.Max(depth,d);}
                csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1:F4},{2:F4},{3:F5}\n",i,interceptor.transform.position.x,interceptor.transform.position.z,overlap?d:0);
                if(i==70)yield return Capture(interceptor,usePlayer?"player-obstacle":"raised-beam");
            }
            if(fixture!=null)Object.Destroy(fixture);else{cc.enabled=false;player.transform.position=new Vector3(-10,0,-10);cc.enabled=true;}
            for(int i=0;i<360&&Vector3.Distance(interceptor.transform.position,new Vector3(10,0,10))>1.1f;i++)yield return null;
            bool recovered=Vector3.Distance(interceptor.transform.position,new Vector3(10,0,10))<=1.1f;
            Write((usePlayer?"player":"beam")+"-collision.csv",csv);
            Write((usePlayer?"player":"beam")+"-result.txt",$"penetrated={penetrated}; max_depth={depth}; recovered={recovered}; DroneBody_present={T("DroneBody",false)!=null}\nTemporary fixture; native ground path complete; 60Hz fixed simulation, 240 observed frames.\n");
            Assert.That(penetrated,Is.False,"Actual root collider penetrated obstacle; see CSV");Assert.That(recovered,Is.True);
        }
        static IEnumerator Capture(Component target,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)yield break;
            var obj=new GameObject("QA evidence camera");var camera=obj.AddComponent<Camera>();var render=new RenderTexture(960,540,24);render.Create();camera.targetTexture=render;
            camera.transform.position=target.transform.position+new Vector3(3,2.8f,-3);camera.transform.LookAt(target.transform.position+Vector3.up);
            yield return null;var previous=RenderTexture.active;RenderTexture.active=render;var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();Directory.CreateDirectory(Out);
            using(var writer=new BinaryWriter(File.Create(Path.Combine(Out,name+".tga"))))
            {writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0});writer.Write((short)960);writer.Write((short)540);writer.Write((byte)24);writer.Write((byte)0);foreach(Color32 p in pixels.GetPixels32()){writer.Write(p.b);writer.Write(p.g);writer.Write(p.r);}}
            RenderTexture.active=previous;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);Object.Destroy(obj);
        }
        [UnityTest] public IEnumerator AllocationMicrobench_LiveComponents()
        {
            for(int k=0;k<4;k++)Call(graph,"SetGateOpen",k,true);
            long calibration=MeasuredAllocation.Calibrate();
            Write("allocation-calibration.txt",$"Known live 1MiB array: GC.Alloc recorded {calibration} allocation events. GC.Alloc sample values are timing, not byte counts. Main-thread recorder; calibrated before measurements.\n");
            string csv="component,workload,calls,total_ms,allocation_events,events_per_call\n";
            Action<string,string,Action,int> measure=(label,workload,action,count)=>
            {
                for(int i=0;i<20;i++)action();
                Action batch=()=>{for(int i=0;i<count;i++)action();};
                long began=Stopwatch.GetTimestamp();
                long allocated=MeasuredAllocation.Events(batch,out var samples);
                long ticks=Stopwatch.GetTimestamp()-began;
                if(label.EndsWith(".BodyQueries",StringComparison.Ordinal))Assert.That(allocated,Is.Zero,"Warmed nonsaturated body query buffers");
                csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:F6},{4},{5:F3}\n",label,workload,count,ticks*1000.0/Stopwatch.Frequency,allocated,(double)allocated/count);
            };
            var search=(Func<Vector3,Vector3,List<Vector3>> )Delegate.CreateDelegate(typeof(Func<Vector3,Vector3,List<Vector3>>),graph,Method(graph,"FindRoute"));
            measure("AStar","all-open graph node0 to node4 (73m)",()=>search(new Vector3(-10,0,-10),new Vector3(22,0,-15)),100);
            foreach(var b in brains)
            {
                var motor=b.GetComponent(T("DroneMotor"));Call(motor,"GoTo",(Vector3)Get(b,"Home"));
                measure(b.GetType().Name+".Motor","same-frame live Update",(Action)Delegate.CreateDelegate(typeof(Action),motor,Method(motor,"Update")),100);
                var bodyType=T("DroneBody",false);var body=bodyType!=null?b.GetComponent(bodyType):null;
                if(body!=null)measure(b.GetType().Name+".Body","same-frame live LateUpdate",(Action)Delegate.CreateDelegate(typeof(Action),body,Method(body,"LateUpdate")),100);
                if(body!=null)
                {
                    var occupy=(Func<Vector3,bool>)Delegate.CreateDelegate(typeof(Func<Vector3,bool>),body,Method(body,"CanOccupy"));
                    var sweep=(Func<Vector3,Vector3,Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3,Vector3,Vector3>),body,Method(body,"Sweep"));
                    Vector3 origin=b.transform.position;
                    measure(b.GetType().Name+".BodyQueries","raised overlap and 0.1m sweep (nonzero displacement)",()=>{occupy(origin);sweep(origin,Vector3.forward*.1f);},100);
                }
                var visual=b.GetComponentInChildren(T("DroneRotorVisuals"));var update=Method(visual,"LateUpdate")??Method(visual,"Update");
                measure(b.GetType().Name+".Animation","same-frame live animation",(Action)Delegate.CreateDelegate(typeof(Action),visual,update),100);
                var visible=Expression.Parameter(typeof(bool));var position=Expression.Parameter(typeof(Vector3));var distance=Expression.Parameter(typeof(float));
                var invoke=Expression.Call(Expression.Constant(b),Method(b,"Decide"),visible,position,distance);
                var decide=Expression.Lambda<Action<bool,Vector3,float>>(Expression.Block(invoke,Expression.Empty()),visible,position,distance).Compile();
                measure(b.GetType().Name+".Decision","same timestamp hidden target; warmed caches",()=>decide(false,Vector3.one*9999,100f),100);
            }
            Write("allocation-microbench.csv",csv);yield return null;
        }
        [UnityTest] public IEnumerator LiveProfile_AllFourAgentsAndDestruction()
        {
            // Inclusive samples. Fixed simulation time; wall frame duration is measured separately.
            bool profilerWasEnabled=UnityEngine.Profiling.Profiler.enabled;
            bool scriptsWasEnabled=UnityEngine.Profiling.Profiler.IsCategoryEnabled(ProfilerCategory.Scripts);
            UnityEngine.Profiling.Profiler.enabled=true;
            UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts,true);
            var names=new[]{"FindRoute","DroneMotor","Decision","DroneAnimation","DroneBody","Effects","Fire","WreckCreate","WreckGeometry","WreckActorExclusion","WreckTick","SyncTransforms","Physics.Simulate"};
            var recorders=new ProfilerRecorder[names.Length];var previousCounts=new int[names.Length];
            var frames=new double[720,names.Length+2];var counts=new long[720,names.Length];
            var progress=Get(session,"Progress");for(int i=0;i<4;i++){Call(progress,"DefeatDrone",i);Call(progress,"ActivateConsole",i);}yield return new WaitForSeconds(2);
            Call(player.GetComponent(T("Health")),"Configure",10000f,false);
            foreach(var b in brains){((Behaviour)b).enabled=true;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=true;}
            for(int i=0;i<names.Length;i++)recorders[i]=ProfilerRecorder.StartNew(i==names.Length-1?ProfilerCategory.Physics:ProfilerCategory.Scripts,i==names.Length-1?names[i]:"ShiftFour."+names[i],4096,ProfilerRecorderOptions.Default|ProfilerRecorderOptions.SumAllSamplesInFrame);
            double before=Time.realtimeSinceStartupAsDouble;
            for(int frame=0;frame<720;frame++)
            {
                if(frame==240)
                {foreach(var b in brains){((Behaviour)b).enabled=false;Call(b.GetComponent(T("DroneMotor")),"GoTo",(Vector3)Get(b,"Home"));}}
                if(frame>=360&&frame<480&&frame%18==0)
                {
                    // Synthetic visible observation stress, explicitly not a perception correctness test.
                    Call(interceptor,"Decide",true,new Vector3(3,0,6+(frame-360)*.04f),10f);
                }
                if(frame==480)
                {
                    foreach(var b in brains)
                    {
                        bool found=false;foreach(float distance in new[]{3.3f,4.5f,5.5f})
                        {for(int yaw=0;yaw<360;yaw+=30)
                        {
                            Vector3 point=b.transform.position+Quaternion.Euler(0,yaw,0)*Vector3.forward*distance;
                            if(!NavMesh.SamplePosition(point,out var hit,.3f,NavMesh.AllAreas))continue;
                            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=hit.position;cc.enabled=true;
                            Method(player,"ApplyCameraPose")?.Invoke(player,null);b.transform.rotation=Quaternion.LookRotation(point-b.transform.position);Physics.SyncTransforms();
                            if(!(bool)Call(b,"CanSeePlayer"))continue;
                            float hp=(float)Get(player.GetComponent(T("Health")),"Current");Set(b,"nextShot",0f);Call(b,"TryShoot");
                            Assert.That((float)Get(player.GetComponent(T("Health")),"Current"),Is.LessThan(hp));found=true;break;
                        }if(found)break;}
                        Assert.That(found,Is.True,"Accepted firing setup "+b.name);
                    }
                }
                if(frame==540)foreach(var b in brains)Call(b.GetComponent(T("Health")),"TakeDamage",10000f);
                yield return null;
                double now=Time.realtimeSinceStartupAsDouble;frames[frame,0]=(now-before)*1000;frames[frame,1]=-1;before=now;
                for(int m=0;m<names.Length;m++)
                {
                    int current=recorders[m].Count;
                    for(int s=previousCounts[m];s<current;s++){var sample=recorders[m].GetSample(s);frames[frame,m+2]+=sample.Value/1000000.0;counts[frame,m]+=sample.Count;}
                    previousCounts[m]=current;
                }
            }
            string availability="marker,valid,samples\n";for(int i=0;i<names.Length;i++){availability+=$"{names[i]},{recorders[i].Valid},{recorders[i].Count}\n";recorders[i].Dispose();}
            string csv="frame,phase,wall_ms,main_thread_gc_bytes,"+string.Join(",",names.SelectMany(n=>new[]{n+"_ms",n+"_calls"}))+"\n";
            for(int f=0;f<720;f++)
            {
                string phase=f<240?"live":f<360?"navigation":f<480?"prediction_stress":f<540?"firing":f<630?"destruction":"settled";
                csv+=f+","+phase+","+frames[f,0].ToString("F6",CultureInfo.InvariantCulture)+","+frames[f,1];
                for(int m=0;m<names.Length;m++)csv+=","+frames[f,m+2].ToString("F6",CultureInfo.InvariantCulture)+","+counts[f,m];csv+="\n";
            }
            UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts,scriptsWasEnabled);
            UnityEngine.Profiling.Profiler.enabled=profilerWasEnabled;
            Write("profile-frames.csv",csv);Write("profile-availability.csv",availability);
            Write("profile-context.txt",$"Unity={Application.unityVersion}; device={SystemInfo.graphicsDeviceName}; CPU={SystemInfo.processorType}; memoryMB={SystemInfo.systemMemorySize}; fixed_simulation_dt={Time.captureDeltaTime}; targetFrameRate={Application.targetFrameRate}\nInclusive main-thread QA markers; do not sum nested scopes. Wall interval includes Editor/render/pacing/test harness. Frame GC is unavailable (-1); see calibrated per-component allocation microbench instead. Gates opened by objective fixture before sampling. 4 accepted shots; 4 fatal damage calls. No player inputs simulated.\n");
            TestContext.Out.WriteLine(availability);
        }
    }
}
#endif
