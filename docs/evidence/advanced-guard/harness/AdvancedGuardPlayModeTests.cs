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
    public sealed class AdvancedGuardPlayModeTests
    {
        private Component guard, player, motor;
        private object awareness, hearing;
        private NavMeshAgent agent;
        private float capture;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private readonly List<GameObject> temporary = new List<GameObject>();
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
        private static FieldInfo Field(object o, string name)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new MissingFieldException(name);
        }
        private static object Call(object o, string name, params object[] args)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(o, args);
            }
            throw new MissingMethodException(name);
        }
        private static object Value(object o, string name) => o.GetType().GetField(name).GetValue(o);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private string State => Get(awareness, "State").ToString();
        private Vector3 Home => (Vector3)Get(guard, "Home");
        private object Decision => Call(guard, "Decide", false, Vector3.one * 99999f, float.PositiveInfinity);
        private string Label => (string)Value(Decision, "Name");
        private Vector3 Goal => (Vector3)Value(Decision, "Destination");
        private bool Valid(Vector3 point) => (bool)Call(guard, "ValidDestination", point);
        private void Tick() { Field(guard, "nextDecision").SetValue(guard, 0f); Call(guard, "Update"); }
        private void MovePlayer(Vector3 point)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = point; cc.enabled = true;
            Call(player, "ApplyCameraPose"); Physics.SyncTransforms();
        }
        private void Noise(string category, Vector3 point)
        {
            var type = T("NoiseCategory"); object kind = Enum.Parse(type, category);
            float intensity = (float)T("SoundEvents").GetMethod("DefaultIntensity").Invoke(null, new[] { kind });
            T("SoundEvents").GetMethod("ReportNoise", new[] { typeof(Vector3), type, typeof(float), typeof(Object) })
                .Invoke(null, new object[] { point, kind, intensity, player });
        }
        private bool Latest(out object sound)
        {
            object[] args = { null }; bool result = (bool)hearing.GetType().GetMethod("TryGetLatest").Invoke(hearing, args);
            sound = args[0]; return result;
        }
        private IEnumerator Emit(string category, float distance = 2f)
        {
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                Noise(category, guard.transform.position + direction * distance);
                if (Latest(out var sound) && Value(sound, "Category").ToString() == category &&
                    (float)Value(sound, "EmittedAt") >= Time.time - .001f && Valid((Vector3)Value(sound, "Position"))) yield break;
                yield return null;
            }
            Assert.Fail("No valid " + category + " fixture near actual Guard at " + guard.transform.position);
        }
        private GameObject Box(Vector3 point, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Runtime Guard test occluder";
            go.transform.position = point; go.transform.localScale = size; temporary.Add(go); Physics.SyncTransforms(); return go;
        }
        private Vector3[] ClearCover()
        {
            var authored = (Vector3[])Field(guard, "coverPoints").GetValue(guard);
            var points = new List<Vector3>();
            foreach (Vector3 point in authored) if (Valid(point)) points.Add(point);
            foreach (Vector3 offset in new[] { new Vector3(-4,0,2), new Vector3(4,0,2), new Vector3(0,0,4),
                new Vector3(-4,0,-2), new Vector3(4,0,-2), new Vector3(0,0,-4), new Vector3(-5,0,4), new Vector3(5,0,4) })
            {
                Vector3 point = Home + offset;
                if (Valid(point) && !points.Any(p => Vector3.Distance(p, point) < 2f)) points.Add(point);
            }
            Assert.That(points.Count, Is.GreaterThanOrEqualTo(2), "Requires two genuinely reachable and physically clear Server positions");
            return points.Take(2).ToArray();
        }
        private object Score(Vector3 point, Vector3 threat)
        {
            object[] args = { point, threat, 0f, false };
            Assert.That(guard.GetType().GetMethod("TryScoreCover", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(guard, args), Is.True);
            return args;
        }
        private object VisibleDecision(Vector3 threat) => Call(guard, "Decide", true, threat, 12f);
        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var b in Find("DroneBrain"))
            {
                ((Behaviour)b).enabled = false; ((Behaviour)b.GetComponent(T("DroneMotor"))).enabled = false;
                var nav = b.GetComponent<NavMeshAgent>(); nav.ResetPath(); nav.isStopped = true; nav.velocity = Vector3.zero;
            }
            player = Find("PlayerController").Single(); ((Behaviour)player).enabled = false;
            guard = Find("GuardBrain").Single(); motor = guard.GetComponent(T("DroneMotor")); agent = guard.GetComponent<NavMeshAgent>();
            awareness = Activator.CreateInstance(T("DroneAwareness"), Activator.CreateInstance(T("AwarenessSettings")), Get(guard, "DifficultySettings"));
            Field(guard, "<Awareness>k__BackingField").SetValue(guard, awareness);
            Field(guard, "lastSeenAt").SetValue(guard, float.NegativeInfinity); Field(guard, "lastKnown").SetValue(guard, Home);
            hearing = Get(guard, "Hearing"); Call(hearing, "Configure", Get(Find("GameSession").Single(), "Graph"), 1f);
            MovePlayer(new Vector3(-1000,0,-1000)); Tick();
            Assert.That(State, Is.EqualTo("Patrol"));
            Assert.That(Valid(Goal), Is.True, "Post fallback must be valid without scene edits");
            Assert.That(Vector3.Distance(Goal,Home), Is.LessThanOrEqualTo(4.01f));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in temporary) if (go != null) Object.Destroy(go); temporary.Clear();
            Scene old = SceneManager.GetActiveScene(); var empty = SceneManager.CreateScene("After advanced Guard");
            SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(old);
            Time.captureDeltaTime = capture; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }
        private IEnumerator Capture(string phase)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            string folder=Path.Combine(Environment.GetEnvironmentVariable("SHIFTFOUR_GUARD_EVIDENCE")??"Logs/AdvancedGuard","frames");
            Directory.CreateDirectory(folder);
            var go=new GameObject("Temporary Guard evidence camera"); temporary.Add(go);
            var camera=go.AddComponent<Camera>(); camera.fieldOfView=55f; camera.nearClipPlane=.05f;
            var render=new RenderTexture(960,540,24); render.Create(); camera.targetTexture=render;
            Vector3 center=guard.transform.position+Vector3.up*1.3f;
            bool found=false;
            foreach(float distance in new[]{4f,2.8f})
            {
                foreach(float angle in new[]{45f,-45f,90f,-90f,135f,-135f,180f,0f})
                {
                    Vector3 offset=Quaternion.Euler(0,angle,0)*Vector3.forward*distance+Vector3.up*.8f;
                    bool blocked=Physics.RaycastAll(center,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore)
                        .Any(h=>!h.transform.IsChildOf(guard.transform)&&h.collider.GetComponentInParent(T("Health"))==null);
                    if(blocked)continue;
                    camera.transform.position=center+offset; found=true; break;
                }
                if(found)break;
            }
            Assert.That(found,Is.True,"Rendered scenario needs a clear in-room view of Guard");
            camera.transform.LookAt(center);
            yield return null;
            RenderTexture previous=RenderTexture.active; RenderTexture.active=render;
            var pixels=new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,render.width,render.height),0,0); pixels.Apply();
                using var writer=new BinaryWriter(File.Create(Path.Combine(folder,"Guard-"+phase+".tga")));
                writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0}); writer.Write((short)render.width); writer.Write((short)render.height);
                writer.Write((byte)24); writer.Write((byte)0);
                foreach(Color32 pixel in pixels.GetPixels32()){writer.Write(pixel.b);writer.Write(pixel.g);writer.Write(pixel.r);}
                File.WriteAllText(Path.Combine(folder,"Guard-"+phase+".txt"),"state="+State+"\ndecision="+Get(guard,"CurrentDecision")+
                    "\nposition="+guard.transform.position+"\ngoal="+Field(motor,"goal").GetValue(motor)+"\n");
            }
            finally
            {
                RenderTexture.active=previous; camera.targetTexture=null; render.Release();
                Object.Destroy(render); Object.Destroy(pixels); Object.Destroy(go);
            }
        }
        [UnityTest] public IEnumerator MainLab_Demonstration_GunshotInvestigationStaysInServerAndReturnsToPost()
        {
            var rows = new List<string> { "time,state,decision,suspicion,x,z,goal_x,goal_z,target_speed,actual_speed,visible" };
            Vector3 start = guard.transform.position; float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            yield return Emit("Gunshot", 3f); Assert.That(Latest(out var sound), Is.True); Vector3 heard = (Vector3)Value(sound, "Position");
            Tick(); Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Label, Is.EqualTo("Guard server room"));
            yield return Capture("Suspicious");
            yield return new WaitForSeconds(.5f); Tick();
            Assert.That(Label, Is.EqualTo("Investigate server disturbance")); Assert.That(Goal, Is.EqualTo(heard));
            Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(2.5f).Within(.001f)); ((Behaviour)motor).enabled = true;
            var states = new HashSet<string>(); var decisions = new HashSet<string>(); var captured = new HashSet<string>();
            float until = Time.time + 15f, moved = 0f;
            while (Time.time < until)
            {
                Tick(); states.Add(State); decisions.Add((string)Get(guard, "CurrentDecision"));
                Vector3 goal = (Vector3)Field(motor, "goal").GetValue(motor);
                Assert.That(Call(guard, "InProtectedArea", goal), Is.True); Assert.That(Call(guard, "InProtectedArea", guard.transform.position), Is.True);
                moved = Mathf.Max(moved, Vector3.Distance(start, guard.transform.position));
                rows.Add(string.Join(",", Time.time.ToString("F3"), State, Get(guard,"CurrentDecision"), Get(awareness,"Suspicion"),
                    guard.transform.position.x, guard.transform.position.z, goal.x, goal.z, Get(motor,"TargetSpeed"), Get(motor,"ActualSpeed"), Get(awareness,"HasVisualConfirmation")));
                if (captured.Add(State)) yield return Capture(State);
                if (State == "Patrol") break;
                yield return new WaitForSeconds(.3f);
            }
            string output = Environment.GetEnvironmentVariable("SHIFTFOUR_GUARD_EVIDENCE") ?? "Logs/AdvancedGuard";
            Directory.CreateDirectory(output); File.WriteAllLines(Path.Combine(output,"MainLab-demo.csv"),rows);
            Assert.That(states, Does.Contain("Investigate")); Assert.That(states, Does.Contain("Search")); Assert.That(states, Does.Contain("ReturnToPost"));
            Assert.That(decisions, Does.Contain("Search server disturbance")); Assert.That(State, Is.EqualTo("Patrol"));
            Assert.That(Vector3.Distance(guard.transform.position, Goal), Is.LessThanOrEqualTo(1.5f)); Assert.That(moved, Is.GreaterThan(.25f));
            Assert.That(Get(awareness,"Suspicion"), Is.EqualTo(0f)); Assert.That(Get(motor,"TargetSpeed"), Is.EqualTo(2f).Within(.001f));
            Assert.That(Get(awareness,"HasVisualConfirmation"), Is.False); Assert.That(Field(guard,"lastSeenAt").GetValue(guard), Is.EqualTo(float.NegativeInfinity));
            Assert.That(Get(player.GetComponent(T("Health")),"Current"), Is.EqualTo(hp));
        }
        [UnityTest] public IEnumerator SingleCredibleConsoleDisturbance_InvestigatesWithoutVisualConfirmation()
        {
            yield return Emit("ConsoleInteraction", 1f); Tick();
            Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Label, Is.EqualTo("Investigate server disturbance"));
            Assert.That(Latest(out var sound), Is.True); Assert.That(Goal, Is.EqualTo(Value(sound,"Position")));
            Assert.That(Get(motor,"TargetSpeed"), Is.EqualTo(2.5f).Within(.001f)); Assert.That(Get(awareness,"HasVisualConfirmation"), Is.False);
            yield return new WaitForSeconds(5.5f); Tick(); Assert.That(Label, Is.EqualTo("Guard server room"));
        }
        [UnityTest] public IEnumerator ActualServerConsole_UsesProductionEmissionAndSharedHearing()
        {
            var console=Find("ConsoleSwitch").Single(c=>(int)Get(c,"GateId")==1);
            Assert.That(Get(console,"Activated"),Is.False);
            Call(console,"Interact"); Assert.That(Get(console,"Activated"),Is.True);
            Assert.That(Latest(out var sound),Is.True,"Real console emission must reach shared hearing from its authored position "+console.transform.position);
            Assert.That(Value(sound,"Category").ToString(),Is.EqualTo("ConsoleInteraction"));
            Tick(); Assert.That(Label,Is.EqualTo("Investigate server disturbance"));
            Assert.That(Valid(Goal),Is.True);
            Assert.That(Vector3.Distance(Goal,(Vector3)Value(sound,"Position")),Is.LessThanOrEqualTo((float)Value(sound,"UncertaintyRadius")+.001f));
            Assert.That(Get(awareness,"HasVisualConfirmation"),Is.False);
            float heardAt=(float)Get(awareness,"LastHeardAt"); Call(console,"Interact"); Tick();
            Assert.That(Get(awareness,"LastHeardAt"),Is.EqualTo(heardAt),"Duplicate activation must not emit new evidence");
            yield return null;
        }
        [UnityTest] public IEnumerator CoverSelection_PreservesOriginalScoresAndRejectsUnsafeCandidates()
        {
            Vector3[] points=ClearCover(); Call(guard,"SetCover",points[0],points[1]); Vector3 threat=Home+Vector3.forward*6f;
            object[] first=(object[])Score(points[0],threat),second=(object[])Score(points[1],threat);
            float a=(float)first[2],b=(float)second[2];
            foreach (var pair in new[] { (points[0],first), (points[1],second) })
            {
                float expected=40f+((bool)pair.Item2[3]?22f:0f)-Vector3.Distance(guard.transform.position,pair.Item1)*.3f-Vector3.Distance(threat,pair.Item1)*.1f;
                Assert.That(pair.Item2[2],Is.EqualTo(expected).Within(.001f));
            }
            object choice=VisibleDecision(threat); Assert.That(Value(choice,"Destination"),Is.EqualTo(a>=b?points[0]:points[1]));
            Vector3 selected=(Vector3)Value(choice,"Destination"); Assert.That(Valid(selected),Is.True);
            Call(guard,"MoveToDecision",choice,true); ((Behaviour)motor).enabled=true;
            float until=Time.time+12f; Vector3 started=guard.transform.position;
            while(Time.time<until && (Vector3.Distance(guard.transform.position,selected)>1.1f || (float)Get(motor,"ActualSpeed")>.1f))
            {
                Assert.That(Call(guard,"InProtectedArea",guard.transform.position),Is.True);
                Assert.That(agent.isOnNavMesh,Is.True); yield return null;
            }
            Assert.That(Vector3.Distance(guard.transform.position,selected),Is.LessThanOrEqualTo(1.1f),"Selected cover must be physically reachable by the real motor");
            Assert.That(Vector3.Distance(started,guard.transform.position),Is.GreaterThan(.05f));
            Call(guard,"SetCover",Home+Vector3.right*20f,new Vector3(10000,0,10000)); choice=VisibleDecision(threat);
            Assert.That(Value(choice,"Name"),Is.EqualTo("Guard server room")); Assert.That(Value(choice,"Destination"),Is.EqualTo((Vector3)Field(guard,"defensivePost").GetValue(guard)));
            yield return null;
        }
        [UnityTest] public IEnumerator CoverCommitment_ResistsMinorThreatMotionButReleasesBlockedCover()
        {
            Vector3[] points=ClearCover(); Call(guard,"SetCover",points[0],points[1]); Vector3 threat=Home+Vector3.forward*6f;
            object first=VisibleDecision(threat); Vector3 committed=(Vector3)Value(first,"Destination");
            float committedAt=(float)Field(guard,"coverCommittedAt").GetValue(guard);
            for(int i=0;i<10;i++)
            {
                object next=VisibleDecision(threat+Vector3.right*(i%2==0?.05f:-.05f));
                Assert.That(Value(next,"Destination"),Is.EqualTo(committed));
                Assert.That(Field(guard,"coverCommittedAt").GetValue(guard),Is.EqualTo(committedAt)); yield return null;
            }
            Box(committed+Vector3.up*1.7f,Vector3.one);
            object replacement=VisibleDecision(threat); Assert.That(Value(replacement,"Destination"),Is.Not.EqualTo(committed));
            Assert.That(Valid((Vector3)Value(replacement,"Destination")),Is.True);
        }
        [UnityTest] public IEnumerator PlayerRelocation_ReconsidersProtectionAndSelectsUsefulCover()
        {
            Vector3[] points=ClearCover(); Call(guard,"SetCover",points[0],points[1]);
            Vector3 initialThreat=points[0]+Vector3.forward*4f;
            // A small solid between the first candidate and the threat supplies real protection.
            Box((points[0]+initialThreat)*.5f+Vector3.up*1.3f,new Vector3(.4f,2.6f,.4f));
            object first=VisibleDecision(initialThreat); Vector3 committed=(Vector3)Value(first,"Destination");
            Vector3 other=committed==points[0]?points[1]:points[0];
            Vector3 changedThreat=committed+Vector3.back*.5f;
            Box((other+changedThreat)*.5f+Vector3.up*1.3f,new Vector3(.4f,2.6f,.4f));
            yield return new WaitForSeconds(1.6f);
            object[] oldScore=(object[])Score(committed,changedThreat),newScore=(object[])Score(other,changedThreat);
            Assert.That(oldScore[3],Is.False); Assert.That(newScore[3],Is.True);
            object after=VisibleDecision(changedThreat); Assert.That(Value(after,"Destination"),Is.EqualTo(other));
            Assert.That(Value(after,"Name"),Is.EqualTo("Take protected cover"));
        }
        private void EstablishActualSight()
        {
            bool found=false;
            foreach(float distance in new[]{4.5f,3f,6f})
            {
                foreach(Vector3 direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right,
                    new Vector3(1,0,1).normalized,new Vector3(-1,0,1).normalized})
                {
                    MovePlayer(guard.transform.position+direction*distance); guard.transform.rotation=Quaternion.LookRotation(direction);
                    if((bool)Call(guard,"CanSeePlayer")){found=true;break;}
                }
                if(found)break;
            }
            Assert.That(found,Is.True,"Fixture must establish actual LOS in saved MainLab"); Tick();
            Assert.That(Get(awareness,"HasVisualConfirmation"),Is.True);
        }
        [UnityTest] public IEnumerator RealVisionAndCover_PreserveAttackRulesAndHiddenPositionMemory()
        {
            Field(guard,"nextShot").SetValue(guard,0f); float before=(float)Get(player.GetComponent(T("Health")),"Current");
            EstablishActualSight(); Vector3 seen=player.transform.position;
            Assert.That(Get(player.GetComponent(T("Health")),"Current"),Is.EqualTo(before-10f));
            Assert.That(Field(guard,"attackRange").GetValue(guard),Is.EqualTo(11f)); Assert.That(Field(guard,"attackInterval").GetValue(guard),Is.EqualTo(1.1f));
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(3f).Within(.001f));
            Call(guard,"TryShoot"); Assert.That(Get(player.GetComponent(T("Health")),"Current"),Is.EqualTo(before-10f),"Cooldown must still apply");
            Box((guard.transform.position+seen)*.5f+Vector3.up*1.5f,new Vector3(.3f,3f,.3f));
            Assert.That(Call(guard,"CanSeePlayer"),Is.False); Field(guard,"nextShot").SetValue(guard,0f); Call(guard,"TryShoot");
            Assert.That(Get(player.GetComponent(T("Health")),"Current"),Is.EqualTo(before-10f));
            MovePlayer(new Vector3(-1000,0,-1000)); yield return new WaitForSeconds(.7f); Tick();
            object first=Decision; MovePlayer(player.transform.position+Vector3.right*100f); Tick(); object second=Decision;
            Assert.That(Field(guard,"lastKnown").GetValue(guard),Is.EqualTo(seen)); Assert.That(Value(first,"Destination"),Is.EqualTo(Value(second,"Destination")));
            yield return new WaitForSeconds(6f); Tick(); Assert.That(Label,Is.EqualTo("Guard server room")); Assert.That(Goal,Is.EqualTo((Vector3)Field(guard,"defensivePost").GetValue(guard)));
        }
        [UnityTest] public IEnumerator ClosedThenOpenServerGate_NeverAuthorizesOutsideInvestigationOrCover()
        {
            var gate=Find("LabDoor").Single(g=>(int)Get(g,"GateId")==1); Assert.That(Get(gate,"IsOpen"),Is.False);
            Vector3 outside=new Vector3(3,0,10); Noise("Gunshot",outside); Tick(); Assert.That(Latest(out _),Is.False);
            var session=Find("GameSession").Single(); var progress=Get(session,"Progress");
            Call(progress,"DefeatDrone",1); Call(progress,"ActivateConsole",1); yield return new WaitForSeconds(2f);
            Assert.That(Get(gate,"IsOpen"),Is.True); Noise("Gunshot",outside); Assert.That(Latest(out var heard),Is.True);
            Assert.That(Call(guard,"InProtectedArea",Value(heard,"Position")),Is.False);
            Tick(); yield return new WaitForSeconds(.5f); Tick(); Assert.That(Label,Is.EqualTo("Guard server room"));
            Call(guard,"SetCover",outside,new Vector3(-10,0,-3));
            object choice=VisibleDecision(outside); Assert.That(Value(choice,"Destination"),Is.EqualTo((Vector3)Field(guard,"defensivePost").GetValue(guard)));
            Assert.That(Valid(outside),Is.False); Assert.That(Call(guard,"InProtectedArea",Value(choice,"Destination")),Is.True);
        }
        [UnityTest] public IEnumerator MovementHook_PreservesValidatedDestinationInsteadOfCombatOffset()
        {
            Vector3 point=ClearCover()[0]; object choice=Activator.CreateInstance(T("DecisionOption"),"Take protected cover",point,62f);
            MovePlayer(point+Vector3.right*2f);
            Call(guard,"MoveToDecision",choice,true);
            Assert.That(Field(motor,"goal").GetValue(motor),Is.EqualTo(point));
            Assert.That(Field(motor,"hasCombatGoal").GetValue(motor),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator RuntimeHealthSpeedAndDeath_UseExistingConfigurationAndTerminalStop()
        {
            var health=guard.GetComponent(T("Health")); Assert.That(Get(health,"Maximum"),Is.EqualTo(150f)); Assert.That(Get(health,"Current"),Is.EqualTo(150f));
            Assert.That(Get(motor,"TargetSpeed"),Is.EqualTo(2f).Within(.001f));
            Assert.That(agent.acceleration,Is.EqualTo(8f)); Assert.That(agent.angularSpeed,Is.EqualTo(120f));
            Call(health,"TakeDamage",10000f); Assert.That(Get(awareness,"IsEnabled"),Is.False); Assert.That(((Behaviour)hearing).enabled,Is.False);
            Assert.That(((Behaviour)motor).enabled,Is.False); Assert.That(Get(motor,"CruiseSpeed"),Is.Zero);
            Call(motor,"SetMovementMode",Enum.Parse(T("DroneMovementMode"),"Chase")); Assert.That(Get(motor,"CruiseSpeed"),Is.Zero);
            yield return null;
        }
    }
}
#endif
