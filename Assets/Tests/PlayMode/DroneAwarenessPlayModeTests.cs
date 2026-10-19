#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class DroneAwarenessPlayModeTests
    {
        private CursorLockMode cursor;
        private bool cursorVisible;
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
        private static object EmptySound => Activator.CreateInstance(T("SoundObservation"));
        private static void UpdateBrain(Component brain)
        {
            Field(brain, "nextDecision").SetValue(brain, 0f); Call(brain, "Update");
        }
        private static void Pause()
        {
            foreach (var b in Find("DroneBrain")) { ((Behaviour)b).enabled = false; ((Behaviour)b.GetComponent(T("DroneMotor"))).enabled = false; }
            foreach (var p in Find("PlayerController")) ((Behaviour)p).enabled = false;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; Pause();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Scene old = SceneManager.GetActiveScene(); Scene empty = SceneManager.GetSceneByName("After awareness tests");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("After awareness tests");
            SceneManager.SetActiveScene(empty); if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }
        private static void FaceAway(Component brain, Component player)
        {
            Vector3 away = brain.transform.position - player.transform.position; away.y = 0f;
            brain.transform.rotation = Quaternion.LookRotation(away);
        }
        private static void Report(Vector3 position)
            => T("SoundEvents").GetMethod("ReportShot").Invoke(null, new object[] { position });

        [UnityTest] public IEnumerator AllFour_ExposeAwarenessAndPreserveIndividualDecisionOptions()
        {
            var brains = Find("DroneBrain"); Assert.That(brains.Length, Is.EqualTo(4));
            var player = Find("PlayerController").Single();
            foreach (var brain in brains)
            {
                object model = Get(brain, "Awareness"); Assert.That(model, Is.Not.Null); Assert.That(Get(model, "IsEnabled"), Is.True);
                FaceAway(brain, player); Assert.That(Call(brain, "CanSeePlayer"), Is.False);
                Report(brain.transform.position);
                object before = Call(brain, "Decide", false, player.transform.position, Vector3.Distance(brain.transform.position, player.transform.position));
                float seenAt = (float)Field(brain, "lastSeenAt").GetValue(brain);
                UpdateBrain(brain);
                object after = Call(brain, "Decide", false, player.transform.position, Vector3.Distance(brain.transform.position, player.transform.position));
                Assert.That(Value(after, "Name"), Is.EqualTo(Value(before, "Name")), brain.GetType().Name);
                Assert.That(Value(after, "Destination"), Is.EqualTo(Value(before, "Destination")), brain.GetType().Name);
                Assert.That(Value(after, "Score"), Is.EqualTo(Value(before, "Score")));
                Assert.That(Get(model, "HasVisualConfirmation"), Is.False); Assert.That(Field(brain, "lastSeenAt").GetValue(brain), Is.EqualTo(seenAt));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RealVision_EntersCombatThenSearchWithoutReadingHiddenMovement()
        {
            var brain = Find("ScoutBrain").Single(); var player = Find("PlayerController").Single();
            var controller = player.GetComponent<CharacterController>();
            bool found = false;
            foreach (Vector3 offset in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                controller.enabled = false;
                player.transform.position = brain.transform.position + offset * 2.5f;
                controller.enabled = true;
                Call(player, "ApplyCameraPose"); brain.transform.rotation = Quaternion.LookRotation(offset); Physics.SyncTransforms();
                if ((bool)Call(brain, "CanSeePlayer")) { found = true; break; }
            }
            Assert.That(found, Is.True, "Fixture requires actual clear visual confirmation, not injected awareness flags");
            Field(brain, "nextShot").SetValue(brain, float.PositiveInfinity);
            UpdateBrain(brain); object model = Get(brain, "Awareness");
            Assert.That(Get(model, "State").ToString(), Is.EqualTo("Combat")); Assert.That(Get(model, "HasVisualConfirmation"), Is.True);
            Vector3 seen = player.transform.position;
            FaceAway(brain, player); UpdateBrain(brain); Assert.That(Get(model, "HasVisualConfirmation"), Is.False);
            player.transform.position += Vector3.right; Call(player, "ApplyCameraPose");
            yield return new WaitForSeconds(.7f); FaceAway(brain, player); UpdateBrain(brain);
            Assert.That(Get(model, "State").ToString(), Is.EqualTo("Search"));
            object[] args = { Time.time, null }; Assert.That(model.GetType().GetMethod("TryGetLastSeen").Invoke(model, args), Is.True);
            Assert.That(args[1], Is.EqualTo(seen));
        }
        [UnityTest] public IEnumerator RealHearing_IncreasesSuspicionWithoutVisualMemoryOrAttackAuthorization()
        {
            var brain = Find("ScoutBrain").Single(); var player = Find("PlayerController").Single(); FaceAway(brain, player);
            Assert.That(Call(brain, "CanSeePlayer"), Is.False);
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            float lastSeen = (float)Field(brain, "lastSeenAt").GetValue(brain);
            object hearing = Get(brain, "Hearing"); object settings = Field(hearing, "settings").GetValue(hearing);
            settings.GetType().GetField("minimumUncertainty").SetValue(settings, 0f);
            settings.GetType().GetField("additionalUncertainty").SetValue(settings, 0f);
            Report(brain.transform.position); UpdateBrain(brain); object model = Get(brain, "Awareness");
            Assert.That((float)Get(model, "Suspicion"), Is.GreaterThan(0f));
            Assert.That(Get(model, "HasVisualConfirmation"), Is.False);
            Assert.That((float)Get(model, "LastHeardAt"), Is.GreaterThanOrEqualTo(0f));
            Call(brain, "TryShoot"); Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp));
            Assert.That(Field(brain, "lastSeenAt").GetValue(brain), Is.EqualTo(lastSeen));
            yield return null;
        }
        [UnityTest] public IEnumerator ActualDeath_DisablesAwarenessBeforeAnyFurtherObservationProcessing()
        {
            foreach (var brain in Find("DroneBrain"))
            {
                object model = Get(brain, "Awareness"); int before = (int)Get(model, "Transitions");
                Call(brain.GetComponent(T("Health")), "TakeDamage", 10000f);
                Assert.That(Get(model, "IsEnabled"), Is.False); Assert.That(Get(model, "HasVisualConfirmation"), Is.False);
                Assert.That((bool)Call(model, "Step", Time.time, true, Vector3.one, false, EmptySound, false), Is.False);
                Assert.That(Get(model, "Transitions"), Is.EqualTo(before));
                Assert.That(((Behaviour)Get(brain, "Hearing")).enabled, Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ActualRestart_DisablesOldModelsAndCreatesFreshAwareness()
        {
            var old = Find("DroneBrain").Select(b => Get(b, "Awareness")).ToArray();
            var session = Find("GameSession").Single();
            Call(Find("PlayerController").Single().GetComponent(T("Health")), "TakeDamage", 10000f);
            Call(session, "Restart"); yield return null; yield return null; Pause();
            var fresh = Find("DroneBrain"); Assert.That(fresh.Length, Is.EqualTo(4));
            foreach (var model in old) Assert.That(Get(model, "IsEnabled"), Is.False);
            foreach (var brain in fresh)
            {
                object model = Get(brain, "Awareness"); Assert.That(Get(model, "IsEnabled"), Is.True);
                Assert.That(old.Any(o => ReferenceEquals(o, model)), Is.False);
            }
        }
        [UnityTest] public IEnumerator FourModels_4000TicksAndPhaseTransitions_AllocateNothing()
        {
            var models = new object[4]; var steps = new Action<float>[4];
            for (int i = 0; i < models.Length; i++)
            {
                models[i] = Activator.CreateInstance(T("DroneAwareness"), Activator.CreateInstance(T("AwarenessSettings")),
                    T("DroneDifficultySettings").GetProperty("Default").GetValue(null));
                Call(models[i], "Step", 0f, true, Vector3.one, false, EmptySound, false);
                var time = Expression.Parameter(typeof(float), "time");
                var call = Expression.Call(Expression.Constant(models[i]), T("DroneAwareness").GetMethod("Step"), time,
                    Expression.Constant(false), Expression.Constant(Vector3.zero), Expression.Constant(false),
                    Expression.Default(T("SoundObservation")), Expression.Constant(false));
                steps[i] = Expression.Lambda<Action<float>>(Expression.Block(call, Expression.Empty()), time).Compile();
                for (int warmup = 0; warmup < 16; warmup++) steps[i](warmup * .001f);
            }
            long calibration = MeasuredAllocation.Calibrate(); var watch = new Stopwatch();
            Action workload = () => { for (int tick = 0; tick < 1000; tick++) for (int i = 0; i < steps.Length; i++) steps[i](1f + tick * .01f); };
            watch.Start(); long allocations = MeasuredAllocation.Events(workload, out int samples); watch.Stop();
            Assert.That(allocations, Is.Zero);
            foreach (var model in models)
            {
                Assert.That(Get(model, "State").ToString(), Is.EqualTo("Patrol"));
                Assert.That(Get(model, "Transitions"), Is.EqualTo(4));
            }
            Write("awareness-ticks.csv", $"models,ticks,allocation_events,marker_samples,calibration_events,elapsed_ms\n4,4000,{allocations},{samples},{calibration},{watch.Elapsed.TotalMilliseconds}\n");
            yield return null;
        }
        private static void Write(string name, string value)
        {
            string path = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/AwarenessEvidence";
            Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, name), value);
        }
    }
}
#endif
