#if UNITY_EDITOR
using System;
using System.Collections;
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
    public sealed class FourRoomIntegrationPlayModeTests
    {
        private CursorLockMode cursor;
        private bool cursorVisible;
        private float capture;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static FieldInfo Field(object o, string n)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new MissingFieldException(n);
        }
        private static object Call(object o, string n, params object[] args)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(o, args);
            }
            throw new MissingMethodException(n);
        }
        private static Component[] Find(string n) => Object.FindObjectsByType(T(n), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static string Output => Environment.GetEnvironmentVariable("SHIFTFOUR_INTEGRATION_EVIDENCE") ?? "Logs/FourRoomIntegration";
        private static void Write(string name, string value) { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, name), value); }
        private static Delegate[] Subscribers(string name)
        {
            var signal = T("SoundEvents").GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return ((Array)Get(signal, "Snapshot")).Cast<Delegate>().ToArray();
        }
        private static void Freeze()
        {
            foreach (var p in Find("PlayerController")) ((Behaviour)p).enabled = false;
            foreach (var b in Find("DroneBrain"))
            {
                ((Behaviour)b).enabled = false;
                ((Behaviour)b.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = b.GetComponent<NavMeshAgent>();
                agent.isStopped = true; agent.ResetPath();
            }
        }
        private static void HidePlayer()
        {
            var player = Find("PlayerController").Single();
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = new Vector3(1000, 0, 1000); controller.enabled = true;
            Call(player, "ApplyCameraPose"); Physics.SyncTransforms();
        }
        private static void Tick(Component brain)
        {
            Field(brain, "nextDecision").SetValue(brain, 0f); Call(brain, "Update");
            brain.GetComponent<NavMeshAgent>().isStopped = true;
        }
        private static void Ramp(Component brain)
        {
            Call(brain.GetComponent(T("DroneMotor")), "Update");
            brain.GetComponent<NavMeshAgent>().isStopped = true;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible; capture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; Freeze(); HidePlayer();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After four-room integration"));
            yield return SceneManager.UnloadSceneAsync(old);
            Time.captureDeltaTime = capture; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        [UnityTest] public IEnumerator MainLab_RuntimeBindingsAndTwoRestartsHaveNoStaleSensorSubscriptions()
        {
            string csv = "generation,agent,health,profile,hearing_subscribers,model_height,body_center,body_extents\n";
            Component[] previous = Array.Empty<Component>(); object[] previousAwareness = Array.Empty<object>();
            for (int generation = 0; generation < 3; generation++)
            {
                var brains = Find("DroneBrain"); Assert.That(brains.Length, Is.EqualTo(4));
                foreach (var old in previous) Assert.That(old == null, Is.True, "Reload must destroy the previous actors");
                foreach (var old in previousAwareness) Assert.That(Get(old, "IsEnabled"), Is.False, "Old awareness must be terminal after unload");
                foreach (var callback in Subscribers("noises").Concat(Subscribers("shots")))
                    if (callback.Target is Component owner)
                    {
                        Assert.That(owner != null, Is.True, "No destroyed Unity subscriber may remain");
                        Assert.That(owner.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
                    }
                Assert.That(Subscribers("noises").Count(c => c.Target is Component component && component.GetType() == T("DroneHearingSensor")), Is.EqualTo(4));
                foreach (var brain in brains)
                {
                    string name = brain.GetType().Name.Replace("Brain", "");
                    float expectedHP = name == "Scout" ? 100f : name == "Interceptor" ? 125f : 150f;
                    Assert.That(Get(brain.GetComponent(T("Health")), "Current"), Is.EqualTo(expectedHP));
                    Assert.That(Get(brain, "DifficultyProfile"), Is.SameAs(Resources.Load("DroneDifficulty/" + name, T("DroneDifficultyProfile"))));
                    foreach (string type in new[] { "DroneBrain", "DroneHearingSensor", "DroneMotor", "DroneBody", "DroneRotorVisuals" })
                        Assert.That(brain.GetComponentsInChildren(T(type), true).Length, Is.EqualTo(1), name + " " + type);
                    var sensor = (Component)Get(brain, "Hearing");
                    Assert.That(((Behaviour)sensor).enabled, Is.True);
                    int subscribers = Subscribers("noises").Count(c => ReferenceEquals(c.Target, sensor));
                    Assert.That(subscribers, Is.EqualTo(1));
                    Assert.That(Field(sensor, "memoryCount").GetValue(sensor), Is.EqualTo(0), "Restart cannot retain old observations");
                    var visual = brain.GetComponentInChildren(T("DroneRotorVisuals"));
                    Assert.That(((Behaviour)visual).enabled, Is.True);
                    Assert.That(Field(visual, "brain").GetValue(visual), Is.SameAs(brain));
                    var motor = brain.GetComponent(T("DroneMotor"));
                    Assert.That(Field(motor, "visual").GetValue(motor), Is.SameAs(visual.transform));
                    Assert.That(Field(motor, "visualOwner").GetValue(motor), Is.SameAs(visual));
                    Assert.That(Get(motor, "HasMovementProfile"), Is.True);
                    Assert.That(((IList)Field(visual, "pivots").GetValue(visual)).Count, Is.EqualTo(4));
                    var body = brain.GetComponent(T("DroneBody"));
                    Assert.That((bool)Call(body, "CanOccupy", brain.transform.position), Is.True, "Spawn must fit the raised body envelope");
                    csv += $"{generation},{name},{expectedHP},{((Object)Get(brain, "DifficultyProfile")).name},{subscribers},{visual.transform.localPosition.y:F3},\"{Get(body, "EnvelopeCenter")}\",\"{Get(body, "EnvelopeExtents")}\"\n";
                }
                var session = Find("GameSession").Single();
                Assert.That(Get(session, "DronesRemaining"), Is.EqualTo(4)); Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(0));
                foreach (var gate in Find("LabDoor"))
                {
                    Assert.That(Get(gate, "IsOpen"), Is.False); Assert.That(gate.GetComponent<BoxCollider>().enabled, Is.True);
                    Assert.That(gate.GetComponent<NavMeshObstacle>().enabled, Is.True);
                }
                Assert.That(Find("DroneWreckMotion"), Is.Empty);
                if (generation == 2) break;
                previous = brains.SelectMany(b => b.GetComponentsInChildren(T("DroneHearingSensor"))).ToArray();
                previousAwareness = brains.Select(b => Get(b, "Awareness")).ToArray();
                // Real production bus and death paths dirty this generation before actual scene reload.
                T("SoundEvents").GetMethod("ReportShot").Invoke(null, new object[] { brains[0].transform.position });
                Call(brains[0].GetComponent(T("Health")), "TakeDamage", 10000f); yield return null;
                Assert.That(Find("DroneWreckMotion").Length, Is.EqualTo(1));
                Call(Find("PlayerController").Single().GetComponent(T("Health")), "TakeDamage", 10000f);
                Assert.That(Get(session, "Ended"), Is.True); Call(session, "Restart");
                yield return null; yield return null; Freeze(); HidePlayer();
            }
            Write("runtime-bindings.csv", csv);
        }

        [UnityTest] public IEnumerator MainLab_RealSoundAndVisionConnectAwarenessToSpeedAndPresentationForAllFour()
        {
            var brains = Find("DroneBrain").OrderBy(b => b.name).ToArray();
            // Let any legitimate startup visual evidence expire, using the real shared Update.
            float until = Time.time + 11f;
            while (Time.time < until) { foreach (var b in brains) Tick(b); yield return new WaitForSeconds(.3f); }
            foreach (var b in brains) Assert.That(Get(Get(b, "Awareness"), "State").ToString(), Is.EqualTo("Patrol"));
            string csv = "agent,evidence,state,mode,target_speed,current_cap,visual_state,visual_confirmation\n";
            foreach (var brain in brains)
            {
                // Use unchanged hearing settings and reachable emissions near the authored spawn.
                var hearing = Get(brain, "Hearing"); bool heard = false;
                for (int i = 0; i < 12 && !heard; i++)
                {
                    Vector3 point = brain.transform.position + Quaternion.Euler(0, i * 30, 0) * Vector3.forward * .6f;
                    if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                    T("SoundEvents").GetMethod("ReportShot").Invoke(null, new object[] { hit.position });
                    object[] args = { null }; heard = (bool)hearing.GetType().GetMethod("TryGetLatest").Invoke(hearing, args);
                    if (!heard) yield return null;
                }
                Assert.That(heard, Is.True, brain.name + " requires a credible nearby sound using the real sensor");
                Tick(brain); yield return null;
                csv += Check(brain, "gunshot", "Suspicious", "Patrol", "patrolSpeed");
                yield return new WaitForSeconds(.5f); Tick(brain);
                // Keep the navigation fixture stationary while executing the real motor speed ramp.
                for (int frame = 0; frame < 45; frame++) { Ramp(brain); yield return null; }
                csv += Check(brain, "gunshot", "Investigate", "Investigate", "investigationSpeed");
                Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.False);
                Assert.That((float)Get(Get(brain, "Awareness"), "LastSeenAt"), Is.LessThan(Time.time - 6f), "Sound cannot refresh genuine last-seen memory");
                until = Time.time + 10f;
                while (Time.time < until)
                {
                    Tick(brain); Ramp(brain); yield return new WaitForSeconds(.3f);
                    if (Get(Get(brain, "Awareness"), "State").ToString() == "Patrol") break;
                }
                Assert.That(Get(Get(brain, "Awareness"), "State").ToString(), Is.EqualTo("Patrol"), "Evidence and return phase must expire");

                // Genuine vision in the existing room; no injected visible flags or state replacements.
                var player = Find("PlayerController").Single(); bool visible = false;
                for (int i = 0; i < 16 && !visible; i++)
                {
                    Vector3 direction = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                    Vector3 point = brain.transform.position + direction * 4f;
                    if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                    var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = hit.position; cc.enabled = true;
                    Call(player, "ApplyCameraPose"); brain.transform.rotation = Quaternion.LookRotation(direction); Physics.SyncTransforms();
                    visible = (bool)Call(brain, "CanSeePlayer");
                }
                Assert.That(visible, Is.True, brain.name + " needs a real clear sightline");
                Field(brain, "nextShot").SetValue(brain, float.PositiveInfinity); Tick(brain);
                for (int frame = 0; frame < 45; frame++) { Ramp(brain); yield return null; }
                csv += Check(brain, "vision", "Combat", "Chase", "chaseSpeed");
                Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.True);
                HidePlayer();
            }
            Write("sensory-speed-presentation.csv", csv);
        }
        private static string Check(Component brain, string evidence, string state, string mode, string speed)
        {
            var awareness = Get(brain, "Awareness"); var motor = brain.GetComponent(T("DroneMotor"));
            var visual = brain.GetComponentInChildren(T("DroneRotorVisuals"));
            Assert.That(Get(awareness, "State").ToString(), Is.EqualTo(state), brain.name);
            Assert.That(Get(motor, "MovementMode").ToString(), Is.EqualTo(mode));
            var settings = Get(brain, "DifficultySettings"); float expected = (float)settings.GetType().GetField(speed).GetValue(settings);
            Assert.That(Get(motor, "TargetSpeed"), Is.EqualTo(expected));
            if (state != "Suspicious") Assert.That((float)Get(motor, "CruiseSpeed"), Is.EqualTo(expected).Within(.001f));
            Assert.That(Get(visual, "AwarenessState").ToString(), Is.EqualTo(state));
            if (state == "Investigate" || state == "Combat") Assert.That((float)Get(visual, "AlertStrength"), Is.GreaterThan(.01f));
            return $"{brain.name},{evidence},{state},{mode},{expected:F3},{Get(motor, "CruiseSpeed")},{Get(visual, "AwarenessState")},{Get(awareness, "HasVisualConfirmation")}\n";
        }
    }
}
#endif
