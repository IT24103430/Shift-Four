#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    public sealed class DroneHearingPlayModeTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly List<BoxCollider> geometry = new List<BoxCollider>();
        private NavMeshDataInstance navigation;
        private NavMeshData data;
        private Component sensor, graph;
        private object config;
        private CursorLockMode cursor;
        private bool visible;
        private static readonly Vector3 Origin = new Vector3(1000, 0, 1000);
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
        private static object Field(object o, string name)
        {
            for (Type type = o.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(o);
            }
            throw new MissingFieldException(name);
        }
        private static object Call(object o, string name, params object[] args)
        {
            for (Type type = o.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(o, args);
            }
            throw new MissingMethodException(name);
        }
        private static void ResetBus() => T("SoundEvents").GetMethod("ResetForPlaySession", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        private static object Noise(Vector3 position, string category = "Gunshot", float intensity = 1f, float? timestamp = null, Object source = null)
            => Activator.CreateInstance(T("NoiseEvent"), position, Enum.Parse(T("NoiseCategory"), category), intensity, timestamp ?? Time.time, source);
        private static bool Observe(Component target, object noise, float? now = null)
            => (bool)Call(target, "ObserveAt", noise, now ?? Time.time);
        private static bool Latest(Component target, out object observation, float? now = null)
        {
            object[] args = { now ?? Time.time, null };
            bool result = (bool)target.GetType().GetMethod("TryGetLatestAt").Invoke(target, args); observation = args[1]; return result;
        }
        private static object Value(object o, string name) => o.GetType().GetField(name).GetValue(o);
        private static Delegate[] Subscribers(string name)
        {
            var signal = T("SoundEvents").GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            return ((Array)Get(signal, "Snapshot")).Cast<Delegate>().ToArray();
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; visible = Cursor.visible; ResetBus(); owned.Clear(); geometry.Clear();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Hearing fixture " + Guid.NewGuid()));
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in owned) if (go != null) Object.Destroy(go);
            if (navigation.valid) navigation.Remove(); if (data != null) Object.Destroy(data);
            ResetBus();
            Scene old = SceneManager.GetActiveScene(); Scene empty = SceneManager.GetSceneByName("After hearing tests");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("After hearing tests");
            SceneManager.SetActiveScene(empty); if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }
        private GameObject Own(string name) { var go = new GameObject(name); owned.Add(go); return go; }
        private void Box(string name, Vector3 offset, Vector3 size, bool trigger = false)
        {
            var go = Own(name); go.transform.position = Origin + offset;
            var box = go.AddComponent<BoxCollider>(); box.size = size; box.isTrigger = trigger; geometry.Add(box);
        }
        private void Bake()
        {
            if (navigation.valid) navigation.Remove(); if (data != null) Object.Destroy(data);
            var sources = geometry.Where(c => !c.isTrigger).Select(c => new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box, transform = c.transform.localToWorldMatrix,
                size = c.size, area = 0
            }).ToList();
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                new Bounds(Origin, new Vector3(70, 20, 70)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null); navigation = NavMesh.AddNavMeshData(data); Physics.SyncTransforms();
        }
        private void Fixture()
        {
            Box("Floor", Vector3.down * .1f, new Vector3(60, .2f, 60)); Bake();
            var owner = Own("Drone sensor"); owner.transform.position = Origin; sensor = owner.AddComponent(T("DroneHearingSensor"));
            graph = Own("Waypoint graph").AddComponent(T("WaypointGraph"));
            var a = Own("Node A"); a.transform.position = Origin;
            var b = Own("Node B"); b.transform.position = Origin + Vector3.right * 10f;
            Array edges = Array.CreateInstance(T("GraphEdge"), 1);
            edges.SetValue(Activator.CreateInstance(T("GraphEdge"), 0, 1, -1), 0);
            Call(graph, "Configure", new[] { a.transform, b.transform }, edges);
            config = Field(sensor, "settings"); Call(sensor, "Configure", graph, 1f);
        }
        private void Config(string name, object value) => config.GetType().GetField(name).SetValue(config, value);

        [UnityTest] public IEnumerator NearbyTypedCategories_AreDetectedThroughSharedBus()
        {
            Fixture();
            foreach (var item in new[] { ("Footstep", .2f), ("Sprint", .45f), ("Gunshot", 1f), ("ConsoleInteraction", .4f), ("Impact", .3f) })
            {
                object noise = Noise(Origin + Vector3.right * 2f, item.Item1, item.Item2);
                T("SoundEvents").GetMethod("ReportNoise", new[] { T("NoiseEvent") }).Invoke(null, new[] { noise });
                Assert.That(Latest(sensor, out var heard), Is.True, item.Item1);
                Assert.That(Value(heard, "Category").ToString(), Is.EqualTo(item.Item1)); yield return null;
            }
            Assert.That(Get(sensor, "EvaluationCount"), Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator OutOfRangeAndDisabledSensitivity_UseNoPhysics()
        {
            Fixture(); Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 30f)), Is.False);
            Call(sensor, "Configure", graph, 0f); Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2f)), Is.False);
            Assert.That(Get(sensor, "PhysicsQueryCount"), Is.EqualTo(0)); Assert.That(Latest(sensor, out _), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator DistanceAndSensitivity_ChangeReceivedStrength()
        {
            Fixture();
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2f)), Is.True); Latest(sensor, out var near);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 5f)), Is.True); Latest(sensor, out var far);
            Assert.That((float)Value(far, "Strength"), Is.LessThan((float)Value(near, "Strength")));
            yield return null;
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 18f, "Sprint", .45f)), Is.False);
            Call(sensor, "Configure", graph, 2f);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 18f, "Sprint", .45f)), Is.True);
        }
        [UnityTest] public IEnumerator Wall_OccludesQuietSoundsAndAttenuatesLoudReachableSounds()
        {
            Fixture(); Box("Solid wall", new Vector3(1.25f, 1.5f, 0), new Vector3(.2f, 3, 6)); Bake();
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2.5f, "Footstep", .2f)), Is.False);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2.5f)), Is.True);
            Assert.That(Latest(sensor, out var heard), Is.True);
            Assert.That((float)Value(heard, "Strength"), Is.EqualTo(.25f - 2.5f / 26f).Within(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator SealedOffMeshAndDisconnectedAreas_AreRejected()
        {
            Fixture(); Config("wallTransmission", 1f);
            Box("Cage west", new Vector3(4.5f, 1.5f, 0), new Vector3(.3f, 3, 3.3f));
            Box("Cage east", new Vector3(7.5f, 1.5f, 0), new Vector3(.3f, 3, 3.3f));
            Box("Cage north", new Vector3(6, 1.5f, 1.5f), new Vector3(3.3f, 3, .3f));
            Box("Cage south", new Vector3(6, 1.5f, -1.5f), new Vector3(3.3f, 3, .3f)); Bake();
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 6f)), Is.False);
            Assert.That(Latest(sensor, out _), Is.False);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 35f)), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator Memory_ExpiresAtEmissionDeadlineAndReplayCannotRefreshIt()
        {
            Fixture(); float timestamp = Time.time;
            var noise = Noise(Origin + Vector3.right * 2f, timestamp: timestamp);
            Assert.That(Observe(sensor, noise), Is.True); Assert.That(Latest(sensor, out var first), Is.True);
            Assert.That(Observe(sensor, noise, timestamp + .1f), Is.False);
            for (int i = 0; i < 50; i++) Assert.That(Latest(sensor, out _, timestamp + 4.9f), Is.True);
            Assert.That((float)Value(first, "EmittedAt"), Is.EqualTo(timestamp));
            Assert.That((float)Value(first, "ExpiresAt"), Is.EqualTo(timestamp + 5f));
            Assert.That(Latest(sensor, out _, timestamp + 5f), Is.False);
            Assert.That(Observe(sensor, noise, timestamp + 6f), Is.False);
            Assert.That(Observe(sensor, Noise(Origin, timestamp: timestamp + 100f), timestamp), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator UncertainMemory_DoesNotFollowMovedOrDestroyedSource()
        {
            Fixture(); var source = Own("Hidden player identity"); source.transform.position = Origin + Vector3.right * 3f;
            Vector3 emission = source.transform.position;
            Assert.That(Observe(sensor, Noise(emission, source: source)), Is.True);
            Assert.That(Latest(sensor, out var first), Is.True);
            Vector3 approximate = (Vector3)Value(first, "Position");
            Assert.That(Vector3.Distance(approximate, emission), Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(approximate, emission), Is.LessThanOrEqualTo((float)Value(first, "UncertaintyRadius") + .001f));
            source.transform.position = Vector3.one * -9999f; Object.Destroy(source); yield return null;
            Assert.That(Latest(sensor, out var later), Is.True);
            Assert.That(Value(later, "Position"), Is.EqualTo(approximate)); Assert.That(Value(later, "ExpiresAt"), Is.EqualTo(Value(first, "ExpiresAt")));
        }
        [UnityTest] public IEnumerator Listener_DisableEnableDestroy_CleansTypedSubscriptionAndMemory()
        {
            Fixture();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Subscribers("noises").Count(d => ReferenceEquals(d.Target, sensor)), Is.EqualTo(1));
                ((Behaviour)sensor).enabled = false;
                Assert.That(Subscribers("noises").Count(d => ReferenceEquals(d.Target, sensor)), Is.Zero);
                Assert.That(Latest(sensor, out _), Is.False);
                ((Behaviour)sensor).enabled = true; yield return null;
            }
            Assert.That(Subscribers("shots"), Is.Empty);
            Object.Destroy(sensor.gameObject); yield return null;
            Assert.That(Subscribers("noises"), Is.Empty);
        }
        [UnityTest] public IEnumerator GateVersionChange_InvalidatesOldInvestigationWithoutRefresh()
        {
            Fixture(); Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2f)), Is.True);
            Assert.That(Latest(sensor, out _), Is.True);
            Call(graph, "SetGateOpen", 0, true);
            Assert.That(Latest(sensor, out _), Is.False); yield return null;
        }
        [UnityTest] public IEnumerator LogicalClosedGate_RejectsDespiteCompleteNativeSurface()
        {
            Fixture();
            Array gated = Array.CreateInstance(T("GraphEdge"), 1);
            gated.SetValue(Activator.CreateInstance(T("GraphEdge"), 0, 1, 0), 0);
            Call(graph, "Configure", (Transform[])Field(graph, "nodes"), gated);
            Vector3 source = Origin + Vector3.right * 10f;
            var native = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(Origin, source, NavMesh.AllAreas, native), Is.True);
            Assert.That(native.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(Observe(sensor, Noise(source)), Is.False);
            Call(graph, "SetGateOpen", 0, true); yield return null;
            Assert.That(Observe(sensor, Noise(source)), Is.True);
        }
        [UnityTest] public IEnumerator PerSecondBudget_RemainsBoundedAcrossFramesAndThenRecovers()
        {
            Fixture(); Config("queriesPerSecond", 2);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 2f)), Is.True);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 3f)), Is.True);
            yield return null;
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 4f)), Is.False);
            Assert.That(Get(sensor, "EvaluationCount"), Is.EqualTo(2));
            yield return new WaitForSeconds(1.1f);
            Assert.That(Observe(sensor, Noise(Origin + Vector3.right * 4f)), Is.True);
        }
        [UnityTest] public IEnumerator BurstProcessing_HasFixedPhysicsBudgetAndBoundedMemory()
        {
            Fixture(); Config("minimumUncertainty", 0f); Config("additionalUncertainty", 0f);
            var report = (Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>), T("SoundEvents").GetMethod("ReportShot"));
            report(Origin + Vector3.right * 2f); yield return null;
            int evaluations = (int)Get(sensor, "EvaluationCount"), physics = (int)Get(sensor, "PhysicsQueryCount");
            long calibration = MeasuredAllocation.Calibrate(); var watch = new Stopwatch();
            Action workload = () => { for (int i = 0; i < 1000; i++) report(Origin + new Vector3(2f, 0, .001f * i)); };
            watch.Start(); long allocations = MeasuredAllocation.Events(workload, out int samples); watch.Stop();
            int used = (int)Get(sensor, "EvaluationCount") - evaluations, queries = (int)Get(sensor, "PhysicsQueryCount") - physics;
            Assert.That(used, Is.EqualTo(2)); Assert.That(queries, Is.EqualTo(6));
            Assert.That(Get(sensor, "BudgetRejections"), Is.EqualTo(998)); Assert.That(Get(sensor, "MemoryCapacity"), Is.EqualTo(4));
            Write("hearing-burst.csv", $"reports,evaluations,physics_queries,budget_rejections,memory_capacity,allocation_events,marker_samples,calibration_events,elapsed_ms\n1000,{used},{queries},998,4,{allocations},{samples},{calibration},{watch.Elapsed.TotalMilliseconds}\n");
        }
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static IEnumerator MainLab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            foreach (var brain in Find("DroneBrain")) { ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false; }
            foreach (var player in Find("PlayerController")) ((Behaviour)player).enabled = false;
        }
        [UnityTest] public IEnumerator ActualClosedGate_RejectsEvenPrematureGraphFlag_ThenOpenGateAllowsHearing()
        {
            yield return MainLab();
            var brain = Find("InterceptorBrain").Single(); var hearing = (Component)Get(brain, "Hearing");
            var session = Find("GameSession").Single(); var actualGraph = Get(session, "Graph");
            var settings = Field(hearing, "settings"); settings.GetType().GetField("minimumUncertainty").SetValue(settings, 0f);
            settings.GetType().GetField("additionalUncertainty").SetValue(settings, 0f);
            Vector3 source = new Vector3(10, 0, -3);
            Assert.That(Observe(hearing, Noise(source)), Is.False);
            Call(actualGraph, "SetGateOpen", 2, true); yield return null;
            Assert.That(Observe(hearing, Noise(source)), Is.False, "Physical closed gate wins over a premature graph flag");
            Call(actualGraph, "SetGateOpen", 2, false);
            var progress = Get(session, "Progress"); Call(progress, "DefeatDrone", 2); Call(progress, "ActivateConsole", 2);
            yield return new WaitForSeconds(2f);
            var gate = Find("LabDoor").Single(g => (int)Get(g, "GateId") == 2);
            Assert.That(Get(gate, "IsOpen"), Is.True);
            Assert.That(Observe(hearing, Noise(source)), Is.True); Assert.That(Latest(hearing, out _), Is.True);
        }
        [UnityTest] public IEnumerator AllFourHaveSensors_AndSoundDoesNotAuthorizeVisionOrShooting()
        {
            yield return MainLab(); var brains = Find("DroneBrain"); Assert.That(brains.Length, Is.EqualTo(4));
            var player = Find("PlayerController").Single(); var playerHealth = player.GetComponent(T("Health"));
            float hp = (float)Get(playerHealth, "Current");
            foreach (var brain in brains)
            {
                var hearing = (Component)Get(brain, "Hearing"); Assert.That(hearing, Is.Not.Null);
                object config = Field(hearing, "settings");
                config.GetType().GetField("minimumUncertainty").SetValue(config, 0f);
                config.GetType().GetField("additionalUncertainty").SetValue(config, 0f);
                // Point away from the actual player; a nearby reachable sound still cannot authorize fire.
                Vector3 away = brain.transform.position - player.transform.position; away.y = 0f;
                brain.transform.rotation = Quaternion.LookRotation(away);
                float lastSeen = (float)Field(brain, "lastSeenAt"); Vector3 lastKnown = (Vector3)Field(brain, "lastKnown");
                var localNoise = Noise(brain.transform.position, "Gunshot");
                Assert.That(Observe(hearing, localNoise), Is.True, brain.GetType().Name);
                Assert.That(Call(brain, "CanSeePlayer"), Is.False);
                Call(brain, "TryShoot");
                Assert.That(Field(brain, "lastSeenAt"), Is.EqualTo(lastSeen)); Assert.That(Field(brain, "lastKnown"), Is.EqualTo(lastKnown));
                Assert.That(Get(playerHealth, "Current"), Is.EqualTo(hp));
            }
            var scout = brains.Single(b => b.GetType().Name == "ScoutBrain");
            Assert.That(Latest((Component)Get(scout, "Hearing"), out var heard), Is.True);
            object model = Get(scout, "Awareness");
            Call(model, "Step", Time.time, false, Vector3.zero, true, heard, false);
            object decision = Call(scout, "Decide", false, Vector3.one * 99999f, float.PositiveInfinity);
            Assert.That(Value(decision, "Name"), Is.EqualTo("Turn toward sound"));
            Assert.That(Value(decision, "Destination"), Is.EqualTo(scout.transform.position));
            yield return new WaitForSeconds(.5f);
            Call(model, "Step", Time.time, false, Vector3.zero, true, heard, false);
            decision = Call(scout, "Decide", false, Vector3.one * 99999f, float.PositiveInfinity);
            Assert.That(Value(decision, "Name"), Is.EqualTo("Investigate sound"));
            Assert.That(Value(decision, "Destination"), Is.EqualTo(Value(heard, "Position")));
        }
        private static void Write(string name, string content)
        {
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/HearingEvidence";
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, name), content);
        }
    }
}
#endif
