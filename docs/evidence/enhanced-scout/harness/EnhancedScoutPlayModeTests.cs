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
    public sealed class EnhancedScoutPlayModeTests
    {
        private Component scout, player, motor;
        private object awareness, hearing;
        private NavMeshAgent agent;
        private float capture;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private GameObject cover;
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
        private float Suspicion => (float)Get(awareness, "Suspicion");
        private object Decision => Call(scout, "Decide", false, Vector3.one * 99999f, float.PositiveInfinity);
        private string Label => (string)Value(Decision, "Name");
        private Vector3 Goal => (Vector3)Value(Decision, "Destination");
        private void Tick() { Field(scout, "nextDecision").SetValue(scout, 0f); Call(scout, "Update"); }
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
        private IEnumerator Emit(string category, float distance = 1f)
        {
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                Noise(category, scout.transform.position + direction * distance);
                if (Latest(out var sound) && Value(sound, "Category").ToString() == category &&
                    (float)Value(sound, "EmittedAt") >= Time.time - .001f &&
                    (bool)Call(scout, "ValidDestination", (Vector3)Value(sound, "Position"))) yield break;
                yield return null;
            }
            Assert.Fail("No valid " + category + " fixture near actual Scout at " + scout.transform.position);
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 30f;
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var b in Find("DroneBrain"))
            {
                ((Behaviour)b).enabled = false;
                ((Behaviour)b.GetComponent(T("DroneMotor"))).enabled = false;
                var nav = b.GetComponent<NavMeshAgent>(); nav.ResetPath(); nav.isStopped = true; nav.velocity = Vector3.zero;
            }
            player = Find("PlayerController").Single(); ((Behaviour)player).enabled = false;
            scout = Find("ScoutBrain").Single(); motor = scout.GetComponent(T("DroneMotor")); agent = scout.GetComponent<NavMeshAgent>();
            awareness = Activator.CreateInstance(T("DroneAwareness"), Activator.CreateInstance(T("AwarenessSettings")), Get(scout, "DifficultySettings"));
            Field(scout, "<Awareness>k__BackingField").SetValue(scout, awareness);
            Field(scout, "lastSeenAt").SetValue(scout, float.NegativeInfinity);
            Field(scout, "lastKnown").SetValue(scout, Get(scout, "Home"));
            hearing = Get(scout, "Hearing");
            Call(hearing, "Configure", Get(Find("GameSession").Single(), "Graph"), 1f);
            MovePlayer(new Vector3(-1000f, 0f, -1000f)); Tick();
            Assert.That(State, Is.EqualTo("Patrol"));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (cover != null) Object.Destroy(cover);
            Scene old = SceneManager.GetActiveScene(); var empty = SceneManager.CreateScene("After enhanced Scout");
            SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(old);
            Time.captureDeltaTime = capture; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }
        [UnityTest] public IEnumerator MainLab_Demonstration_GunshotTurnTravelSearchDecayReturn()
        {
            var rows = new List<string> { "time,state,decision,suspicion,x,z,target_speed,actual_speed" };
            Vector3 start = scout.transform.position;
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            yield return Emit("Gunshot", 3f); Assert.That(Latest(out var sound), Is.True);
            Vector3 heard = (Vector3)Value(sound, "Position");
            Tick(); Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Label, Is.EqualTo("Turn toward sound"));
            // Clear the frozen fixture's prior patrol route; production Motor arrives at this hold normally.
            ((IList)Field(motor, "route").GetValue(motor)).Clear();
            float angleBefore = Vector3.Angle(scout.transform.forward, heard - scout.transform.position);
            for (int i = 0; i < 8; i++)
            {
                Quaternion before = scout.transform.rotation; Call(scout, "LateUpdate");
                Assert.That(Quaternion.Angle(before, scout.transform.rotation), Is.LessThanOrEqualTo(agent.angularSpeed * Time.deltaTime + .001f));
                yield return null;
            }
            float angleAfter = Vector3.Angle(scout.transform.forward, heard - scout.transform.position);
            Assert.That(angleAfter, Is.LessThan(angleBefore), "Stationary Scout turns toward uncertain sound");
            yield return new WaitForSeconds(.2f); Tick();
            Assert.That(State, Is.EqualTo("Investigate")); Assert.That(Label, Is.EqualTo("Investigate sound"));
            Assert.That(Goal, Is.EqualTo(heard)); Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(2.2f).Within(.001f));
            ((Behaviour)motor).enabled = true;
            var states = new HashSet<string> { "Suspicious", "Investigate" };
            var decisions = new HashSet<string>(); float maxTravel = 0f; float until = Time.time + 15f;
            while (Time.time < until)
            {
                Tick(); states.Add(State); decisions.Add((string)Get(scout, "CurrentDecision"));
                maxTravel = Mathf.Max(maxTravel, Vector3.Distance(start, scout.transform.position));
                rows.Add(string.Join(",", Time.time.ToString("F3"), State, Get(scout, "CurrentDecision"), Suspicion.ToString("F3"),
                    scout.transform.position.x.ToString("F3"), scout.transform.position.z.ToString("F3"), Get(motor, "TargetSpeed"), Get(motor, "ActualSpeed")));
                if (State == "Patrol") break;
                yield return new WaitForSeconds(.3f);
            }
            string output = Environment.GetEnvironmentVariable("SHIFTFOUR_SCOUT_EVIDENCE") ?? "Logs/EnhancedScout";
            Directory.CreateDirectory(output); File.WriteAllLines(Path.Combine(output, "MainLab-demo.csv"), rows);
            Assert.That(states, Does.Contain("Search")); Assert.That(states, Does.Contain("ReturnToPost"));
            Assert.That(decisions, Does.Contain("Search sound area"));
            Assert.That(State, Is.EqualTo("Patrol")); Assert.That(Suspicion, Is.Zero); Assert.That(maxTravel, Is.GreaterThan(.25f));
            Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
            Assert.That(Field(scout, "lastSeenAt").GetValue(scout), Is.EqualTo(float.NegativeInfinity));
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp));
            Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(1.8f).Within(.001f));
        }
        [UnityTest] public IEnumerator NearbyWalking_BuildsEvidenceThenInvestigatesWithoutVision()
        {
            yield return Emit("Footstep", .5f); Tick();
            Assert.That(Suspicion, Is.GreaterThan(0f)); Assert.That(State, Is.EqualTo("Patrol"), "One weak step does not confirm a target");
            bool turned = false;
            for (int i = 0; i < 12 && State != "Investigate"; i++)
            {
                yield return new WaitForSeconds(.35f); yield return Emit("Footstep", .5f); Tick();
                turned |= Label == "Turn toward sound";
            }
            Assert.That(turned, Is.True); Assert.That(State, Is.EqualTo("Investigate")); Assert.That(Label, Is.EqualTo("Investigate sound"));
            Assert.That(Latest(out var sound), Is.True); Assert.That(Goal, Is.EqualTo(Value(sound, "Position")));
            Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
        }
        [UnityTest] public IEnumerator NearbySprint_UsesTypedHearingWithoutAddingSprintControls()
        {
            for (int i = 0; i < 5 && State != "Investigate"; i++)
            { yield return Emit("Sprint", 1f); Tick(); yield return new WaitForSeconds(.5f); }
            Assert.That(State, Is.EqualTo("Investigate")); Assert.That(Label, Is.EqualTo("Investigate sound"));
            Assert.That(Latest(out var sound), Is.True); Assert.That(Value(sound, "Category").ToString(), Is.EqualTo("Sprint"));
            Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
        }
        private void EstablishActualSight()
        {
            bool found = false;
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                MovePlayer(scout.transform.position + direction * 4.5f); scout.transform.rotation = Quaternion.LookRotation(direction);
                if ((bool)Call(scout, "CanSeePlayer")) { found = true; break; }
            }
            Assert.That(found, Is.True, "MainLab fixture must establish real visibility");
            Tick(); Assert.That(State, Is.EqualTo("Combat")); Assert.That(Get(awareness, "HasVisualConfirmation"), Is.True);
        }
        [UnityTest] public IEnumerator FreshVisualConfirmation_OutranksSoundAndEngagesThroughNormalCombatChecks()
        {
            yield return Emit("Gunshot"); Tick(); Assert.That(State, Is.EqualTo("Suspicious"));
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            Field(scout, "nextShot").SetValue(scout, 0f); EstablishActualSight();
            object option = Call(scout, "Decide", true, player.transform.position, 4.5f);
            Assert.That(Value(option, "Name"), Is.EqualTo("Chase visible player"));
            Assert.That(Value(option, "Destination"), Is.EqualTo(player.transform.position));
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.LessThan(hp));
        }
        [UnityTest] public IEnumerator RealSight_CoverBlocksFireAndHiddenMotionDoesNotUpdateMemory()
        {
            EstablishActualSight(); Vector3 seen = player.transform.position;
            Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(2.6f).Within(.001f));
            cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Runtime Scout cover test";
            cover.transform.position = (scout.transform.position + seen) * .5f + Vector3.up * 1.5f;
            cover.transform.localScale = new Vector3(.3f, 3f, .3f); Physics.SyncTransforms();
            Assert.That(Call(scout, "CanSeePlayer"), Is.False);
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            Field(scout, "nextShot").SetValue(scout, 0f); Call(scout, "TryShoot");
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp));
            MovePlayer(new Vector3(-1000f, 0f, -1000f)); Tick(); yield return new WaitForSeconds(.7f); Tick();
            Assert.That(State, Is.EqualTo("Search")); Assert.That(Label, Is.EqualTo("Search last sighting"));
            Assert.That(Goal, Is.EqualTo(seen));
            for (int i = 0; i < 3; i++)
            {
                MovePlayer(player.transform.position + Vector3.right * 20f); Tick();
                Assert.That(Field(scout, "lastKnown").GetValue(scout), Is.EqualTo(seen)); Assert.That(Goal, Is.EqualTo(seen));
                yield return null;
            }
            yield return new WaitForSeconds(6f); Tick();
            Assert.That(Label, Is.EqualTo("Return to patrol"));
        }
        [UnityTest] public IEnumerator LocalSearch_IsBoundedAndDoesNotRefreshEvidenceOrRepeatForever()
        {
            yield return Emit("Gunshot", .5f); Tick();
            yield return new WaitForSeconds(.5f); Tick();
            yield return new WaitForSeconds(2.2f); Tick(); Assert.That(State, Is.EqualTo("Search"));
            Assert.That(Latest(out var sound), Is.True); Vector3 center = (Vector3)Value(sound, "Position");
            float emitted = (float)Get(awareness, "LastHeardAt");
            var goals = new HashSet<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                Vector3 goal = Goal; goals.Add(goal);
                Assert.That(Vector3.Distance(goal, center), Is.LessThanOrEqualTo(1.51f));
                agent.Warp(goal); Physics.SyncTransforms();
            }
            Assert.That(goals.Count, Is.LessThanOrEqualTo(3)); Assert.That(Get(awareness, "LastHeardAt"), Is.EqualTo(emitted));
            yield return new WaitForSeconds(3f); Tick(); Assert.That(Label, Is.EqualTo("Return to patrol"));
        }
        [UnityTest] public IEnumerator ClosedCheckpointGate_RejectsHiddenSourceAndUnreachableGoal()
        {
            // The real closed Checkpoint gate separates its adjacent room from the Scout.
            var gate = Find("LabDoor").Single(g => (int)Get(g, "GateId") == 0);
            Assert.That(Get(gate, "IsOpen"), Is.False);
            Vector3 otherRoom = new Vector3(-10f, 0f, -3f);
            Noise("Gunshot", otherRoom); Tick();
            Assert.That(Latest(out _), Is.False); Assert.That(Label, Is.EqualTo("Patrol"));
            Assert.That(Call(scout, "ValidDestination", new Vector3(10000f, 0f, 10000f)), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator RuntimeProfileAndActualDeath_PreserveHealthSpeedAndShutdown()
        {
            var health = scout.GetComponent(T("Health"));
            Assert.That(Get(health, "Maximum"), Is.EqualTo(100f)); Assert.That(Get(health, "Current"), Is.EqualTo(100f));
            Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(1.8f).Within(.001f));
            yield return Emit("Gunshot"); Tick();
            Call(health, "TakeDamage", 10000f);
            Assert.That(Get(awareness, "IsEnabled"), Is.False); Assert.That(((Behaviour)hearing).enabled, Is.False);
            Assert.That(((Behaviour)motor).enabled, Is.False); Assert.That(Get(motor, "ActualSpeed"), Is.Zero);
            Quaternion rotation = scout.transform.rotation; Call(scout, "LateUpdate");
            Assert.That(scout.transform.rotation, Is.EqualTo(rotation));
            Call(motor, "SetMovementMode", Enum.Parse(T("DroneMovementMode"), "Chase"));
            Assert.That(Get(motor, "CruiseSpeed"), Is.Zero);
        }
    }
}
#endif
