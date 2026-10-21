#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    public sealed class ControlTacticalNavigationPlayModeTests
    {
        private Component brain,graph,player,session,motor;
        private Component[] brains;
        private object navigation,validator;
        private Vector3 home;
        private NavMeshAgent agent;
        private CursorLockMode cursorLock; private bool cursorVisible;
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string p)=>o.GetType().GetProperty(p).GetValue(o);
        private static object Field(object o,string p)=>o.GetType().GetField(p).GetValue(o);
        private static object Call(object o,string name,params object[] args)
        {
            for(Type type=o.GetType();type!=null;type=type.BaseType)
            {
                var method=type.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(method!=null)return method.Invoke(o,args);
            }
            throw new MissingMethodException(name);
        }
        private static void Set(object o,string name,object data)
        {
            for(Type type=o.GetType();type!=null;type=type.BaseType)
            {
                var field=type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if(field!=null){field.SetValue(o,data);return;}
            }
            throw new MissingFieldException(name);
        }
        private Vector3[] Points=>((IEnumerable)Get(navigation,"Points")).Cast<Vector3>().ToArray();
        private Vector3[] Route(Vector3 a,Vector3 b)=>(Vector3[])Call(validator,"Route",a,b);
        private Vector3[] PlayerRoute(Vector3 a,Vector3 b)=>(Vector3[])Call(validator,"PlayerRoute",a,b);
        private object Build(int direction)=>Call(navigation,"Build",0,3,direction,Points[0],new Func<Vector3,Vector3,Vector3[]>(Route));
        private static Vector3 Destination(object o)=>(Vector3)Field(o,"Destination");
        [UnitySetUp] public IEnumerator SetUp()
        {
            cursorLock=Cursor.lockState;cursorVisible=Cursor.visible;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainLab.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            brains=Object.FindObjectsByType(T("DroneBrain")).Cast<Component>().ToArray();
            foreach(var b in brains){((Behaviour)b).enabled=false;((Behaviour)b.GetComponent(T("DroneMotor"))).enabled=false;}
            player=(Component)Object.FindFirstObjectByType(T("PlayerController"));((Behaviour)player).enabled=false;
            session=(Component)Object.FindFirstObjectByType(T("GameSession"));graph=(Component)Get(session,"Graph");
            brain=brains.Single(b=>b.GetType().Name=="InterceptorBrain");agent=brain.GetComponent<NavMeshAgent>();
            motor=brain.GetComponent(T("DroneMotor"));home=(Vector3)Get(brain,"Home");
            brain.transform.rotation=Quaternion.identity;
            Call(brain,"Decide",false,Vector3.one*9999,float.PositiveInfinity);
            navigation=Get(brain,"TacticalNavigation");validator=Get(brain,"TacticalValidator");
            yield return new WaitForSeconds(.1f);Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var scene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("After Control tactics"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Cursor.lockState=cursorLock;Cursor.visible=cursorVisible;
        }
        [UnityTest] public IEnumerator BothArcs_UseCompleteDistinctCollisionClearRoutes_WithoutGlobalNodes()
        {
            var left=Build(1);var right=Build(-1);
            Assert.That(left,Is.Not.Null);Assert.That(right,Is.Not.Null);
            var leftStops=(int[])Field(left,"Junctions");var rightStops=(int[])Field(right,"Junctions");
            CollectionAssert.AreEqual(new[]{0,1,2,3},leftStops);CollectionAssert.AreEqual(new[]{0,5,4,3},rightStops);
            Assert.That((int)Get(graph,"NodeCount"),Is.EqualTo(5));
            foreach(var p in Points)Assert.That((bool)Call(validator,"CanOccupy",p),Is.True,p.ToString());
            Assert.That(((Vector3[])Field(left,"Route")).Min(p=>p.x),Is.LessThan(4));
            Assert.That(((Vector3[])Field(right,"Route")).Max(p=>p.x),Is.GreaterThan(16));
            Assert.That((float)Field(left,"Distance"),Is.EqualTo(25f).Within(.1f));
            Assert.That((float)Field(right,"Distance"),Is.EqualTo(16f+Mathf.Sqrt(53f)).Within(.1f));
            var polygon=((Vector3[])Field(left,"Route")).Concat(((Vector3[])Field(right,"Route")).Reverse().Skip(1)).ToArray();
            var covers=GameObject.Find("Control").GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("Fixed cover")).ToArray();
            Assert.That(covers.Length,Is.EqualTo(2));
            foreach(var cover in covers)Assert.That(Inside(polygon,cover.bounds.center),Is.True,"The two arcs must enclose actual cover, not be small variants of the same path");
            string directory="Logs/ControlTactical";Directory.CreateDirectory(directory);
            string geometry="Control world geometry (loaded reviewed dev scene)\n";
            foreach(var c in GameObject.Find("Control").GetComponentsInChildren<Collider>())
                if(c.enabled && !c.isTrigger)geometry+=c.name+" center="+c.bounds.center.ToString("F3")+" size="+c.bounds.size.ToString("F3")+"\n";
            File.WriteAllText(Path.Combine(directory,"geometry.txt"),geometry);
            string csv="junction,x,y,z\n";
            for(int i=0;i<Points.Length;i++)csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1:F3},{2:F3},{3:F3}\n",i,Points[i].x,Points[i].y,Points[i].z);
            File.WriteAllText(Path.Combine(directory,"junctions.csv"),csv);
            File.WriteAllText(Path.Combine(directory,"routes.txt"),"left="+Field(left,"Distance")+"m; stops="+string.Join(",",leftStops)+
                "\nright="+Field(right,"Distance")+"m; stops="+string.Join(",",rightStops)+"\nvolume center="+Get(validator,"Center")+" halfExtents="+Get(validator,"Extents"));
            yield return null;
        }
        private static bool Inside(Vector3[] polygon,Vector3 point)
        {
            bool result=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].z>point.z)!=(polygon[j].z>point.z) &&
                    point.x<(polygon[j].x-polygon[i].x)*(point.z-polygon[i].z)/(polygon[j].z-polygon[i].z)+polygon[i].x)result=!result;
            return result;
        }
        [UnityTest] public IEnumerator ActualMotor_FollowsBothArcs_WithoutWallsLoopsOrBodyOverlaps()
        {
            string csv="arc,completed,distance_travelled,seconds,navigation_errors\n";
            foreach(int direction in new[]{1,-1})
            {
                var plan=Build(direction);Assert.That(plan,Is.Not.Null);
                var stops=(int[])Field(plan,"Junctions");Assert.That(stops.Distinct().Count(),Is.EqualTo(stops.Length));
                Assert.That(agent.Warp(Points[0]),Is.True);Physics.SyncTransforms();
                ((Behaviour)motor).enabled=true;float began=Time.time,travelled=0;int errors=0;
                foreach(int stop in stops.Skip(1))
                {
                    Assert.That(Route(brain.transform.position,Points[stop]),Is.Not.Null,"Actual stopping position must also attach safely");
                    Call(motor,"GoTo",Points[stop]);float limit=Time.time+10f;
                    while(Vector3.Distance(brain.transform.position,Points[stop])>1.1f && Time.time<limit)
                    {
                        Vector3 before=brain.transform.position;yield return null;
                        travelled+=Vector3.Distance(before,brain.transform.position);
                        if(!agent.isOnNavMesh || !(bool)Call(validator,"CanOccupy",brain.transform.position) ||
                            (!agent.pathPending && agent.hasPath && agent.pathStatus!=NavMeshPathStatus.PathComplete))errors++;
                        Assert.That(brain.transform.position.x,Is.InRange(1.5f,18.5f));
                        Assert.That(brain.transform.position.z,Is.InRange(2.5f,18f));
                    }
                    Assert.That(Vector3.Distance(brain.transform.position,Points[stop]),Is.LessThanOrEqualTo(1.1f));
                }
                Assert.That(errors,Is.Zero,"Full yaw-safe visual envelope must stay collision clear");
                csv+=string.Format(CultureInfo.InvariantCulture,"{0},True,{1:F3},{2:F3},{3}\n",direction==1?"Left":"Right",travelled,Time.time-began,errors);
                ((Behaviour)motor).enabled=false;agent.isStopped=true;agent.ResetPath();
            }
            Directory.CreateDirectory("Logs/ControlTactical");File.WriteAllText("Logs/ControlTactical/motor-routes.csv",csv);
            TestContext.Out.WriteLine(csv);
        }
        [UnityTest] public IEnumerator ClosedGate_RejectsNativeAndGraphPaths_EvenIfGraphFlagIsPremature()
        {
            Vector3 control=home,storage=new Vector3(10,0,-3);
            Assert.That(PlayerRoute(control,storage),Is.Null);
            Call(graph,"SetGateOpen",2,true);
            Assert.That(PlayerRoute(control,storage),Is.Null,"Native carving must still reject the physically closed gate");
            Call(graph,"SetGateOpen",2,false);
            Assert.That(Route(new Vector3(3,0,10),new Vector3(-3,0,10)),Is.Null,"West wall/Server gate must remain blocked");
            yield return null;
        }
        [UnityTest] public IEnumerator RaisedObstacle_RejectsRouteDespiteCompleteGroundNavMesh()
        {
            Assert.That(Route(Points[1],Points[2]),Is.Not.Null);
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.position=new Vector3(3,1.2f,10);
            box.transform.localScale=new Vector3(.1f,.4f,.1f);Physics.SyncTransforms();
            var native=new NavMeshPath();Assert.That(NavMesh.CalculatePath(Points[1],Points[2],NavMesh.AllAreas,native),Is.True);
            Assert.That(native.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(Route(Points[1],Points[2]),Is.Null);Assert.That(Build(-1),Is.Not.Null);
            Object.Destroy(box);yield return null;
        }
        private object DecideMemory(Vector3 source,Vector3 velocity,Vector3 hidden)
        {
            Set(brain,"lastKnown",source);Set(brain,"lastSeenAt",Time.time);Set(brain,"observedVelocity",velocity);
            return Call(brain,"Decide",false,hidden,float.PositiveInfinity);
        }
        [UnityTest] public IEnumerator Interceptor_UsesBothJunctions_AndIgnoresHiddenInputWithoutOscillation()
        {
            Assert.That(agent.Warp(Points[3]),Is.True);Physics.SyncTransforms();
            foreach(float x in new[]{3f,17f})
            {
                // Fresh policy, actual adapter, timed visible observations (not injected shared memory).
                Set(brain,"policy",null);Call(navigation,"Clear","Controlled independent scenario");
                object first=null;
                for(int i=0;i<=6;i++)
                {
                    first=Call(brain,"Decide",true,new Vector3(x,0,5.5f+i*.6f),10f);
                    yield return new WaitForSeconds(.3f);
                }
                var policy=Get(brain,"Policy");
                Assert.That(Get(policy,"State").ToString(),Is.EqualTo("InterceptAhead"));
                Assert.That(Get(navigation,"Plan"),Is.Not.Null);
                Assert.That(Field(Get(navigation,"Plan"),"Target"),Is.EqualTo(x==3f?2:4));
                var last=(Vector3)Get(policy,"LastObserved");
                for(int i=0;i<20;i++)
                {
                    var repeated=Call(brain,"Decide",false,Vector3.one*(i%2==0?99999:-99999),100f);
                    Assert.That(Destination(repeated),Is.EqualTo(Destination(first)));
                    Assert.That((Vector3)Get(policy,"LastObserved"),Is.EqualTo(last));
                }
                Assert.That(Get(policy,"Switches"),Is.EqualTo(0));
                Call(brain,"Decide",true,last+Vector3.back*.6f,10f);
                Assert.That(Get(policy,"SelectedNode"),Is.EqualTo(-1));
                Assert.That(Get(navigation,"Plan"),Is.Null);
            }
            Assert.That((int)Get(graph,"NodeCount"),Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator OtherThreePolicies_AndSharedGraphRemainUnchangedByLocalPlanning()
        {
            var others=brains.Where(b=>b!=brain).ToArray();
            var baseline=others.Select(b=>Call(b,"Decide",false,player.transform.position,100f)).ToArray();
            string graphBefore=JsonUtility.ToJson(graph);int version=(int)Get(graph,"Version");
            Assert.That(agent.Warp(Points[3]),Is.True);Physics.SyncTransforms();
            for(int i=0;i<7;i++){Call(brain,"Decide",true,new Vector3(3,0,5.5f+i*.6f),10f);yield return new WaitForSeconds(.3f);}
            Assert.That(Get(navigation,"Plan"),Is.Not.Null);
            for(int i=0;i<others.Length;i++)
            {
                var after=Call(others[i],"Decide",false,player.transform.position,100f);
                Assert.That(Destination(after),Is.EqualTo(Destination(baseline[i])));
                Assert.That(Field(after,"Name"),Is.EqualTo(Field(baseline[i],"Name")));
                Assert.That(Field(after,"Score"),Is.EqualTo(Field(baseline[i],"Score")));
            }
            Assert.That(JsonUtility.ToJson(graph),Is.EqualTo(graphBefore));Assert.That(Get(graph,"Version"),Is.EqualTo(version));
            yield return null;
        }
        [UnityTest] public IEnumerator ControlConsole_AccessAndAndGateProgression_PreserveExitHandoff()
        {
            var consoles=Object.FindObjectsByType(T("ConsoleSwitch")).Cast<Component>().ToArray();
            var console=consoles.Single(c=>(int)Get(c,"GateId")==2);
            Vector3 approach=new Vector3(16.5f,0,14.5f);
            Assert.That(PlayerRoute(new Vector3(3,0,10),approach),Is.Not.Null,"Player-sized path to Control console");
            Vector3 eye=approach+Vector3.up*1.3f;
            Vector3 aim=console.GetComponent<Collider>().bounds.center-eye;
            Assert.That(Physics.Raycast(eye,aim.normalized,out var hit,3f,~0,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider.GetComponentInParent(T("ConsoleSwitch")),Is.EqualTo(console));
            var progress=Get(session,"Progress");Call(console,"Interact");
            yield return new WaitForSeconds(2f);
            Assert.That((bool)Call(progress,"ConsoleOnline",2),Is.True);
            Assert.That((bool)Call(graph,"IsGateOpen",2),Is.False,"Console alone must not open the gate");
            Call(brain.GetComponent(T("Health")),"TakeDamage",10000f);
            yield return new WaitForSeconds(2f);
            Assert.That((bool)Call(graph,"IsGateOpen",2),Is.True);
            Assert.That(PlayerRoute(home,new Vector3(10,0,-3)),Is.Not.Null);
            Assert.That((bool)Call(graph,"IsGateOpen",3),Is.False);
            Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.False);
            for(int i=0;i<4;i++)
            {
                Call(progress,"DefeatDrone",i);Call(consoles.Single(c=>(int)Get(c,"GateId")==i),"Interact");
            }
            yield return new WaitForSeconds(2f);
            Assert.That((bool)Call(graph,"IsGateOpen",3),Is.True);
            Assert.That(PlayerRoute(new Vector3(10,0,-10),new Vector3(22,0,-15)),Is.Not.Null);
            Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.True);
            Assert.That((int)Get(graph,"NodeCount"),Is.EqualTo(5));
        }
    }
}
#endif
