#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class SoundEventsPlayModeTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private CursorLockMode cursor;
        private bool cursorVisible;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Type Bus => T("SoundEvents");
        private static Type NoiseType => T("NoiseEvent");
        private static object Category(string name) => Enum.Parse(T("NoiseCategory"), name);
        private static object Noise(float timestamp = 1f)
            => Activator.CreateInstance(NoiseType, Vector3.one * 1000f, Category("Gunshot"), 1f, timestamp, null);
        private static bool Report(object noise) => (bool)Bus.GetMethod("ReportNoise", new[] { NoiseType }).Invoke(null, new[] { noise });
        private static void Shot() => Bus.GetMethod("ReportShot").Invoke(null, new object[] { Vector3.one * 1000f });
        private static object Get(object c, string property) => c.GetType().GetProperty(property).GetValue(c);
        private static FieldInfo Field(object c, string name)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new MissingFieldException(name);
        }
        private static object Call(object c, string method, params object[] values)
            => c.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(c, values);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static void Reset() => Bus.GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static Delegate[] Subscribers(string name)
        {
            object signal = Bus.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            return ((Array)Get(signal, "Snapshot")).Cast<Delegate>().ToArray();
        }
        private static int TargetCount(string signal, object owner)
            => Subscribers(signal).Count(callback => ReferenceEquals(callback.Target, owner));

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            Reset(); owned.Clear();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Noise fixture " + Guid.NewGuid()));
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in owned) if (go != null) Object.Destroy(go);
            Reset();
            Scene old = SceneManager.GetActiveScene();
            Scene empty = SceneManager.GetSceneByName("After noise tests");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("After noise tests");
            SceneManager.SetActiveScene(empty);
            if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        private NoiseListenerProbe Probe(string name = "Noise observer")
        {
            var go = new GameObject(name); owned.Add(go); return go.AddComponent<NoiseListenerProbe>();
        }

        private static IEnumerator MainLab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            PauseInputAndMovement();
        }
        private static void PauseInputAndMovement()
        {
            foreach (var player in Find("PlayerController")) ((Behaviour)player).enabled = false;
            foreach (var motor in Find("DroneMotor")) ((Behaviour)motor).enabled = false;
        }

        [UnityTest] public IEnumerator EnableDisableCycles_RegisterOnceAndUnregisterBothChannels()
        {
            var probe = Probe(); probe.Register(); probe.Register();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(TargetCount("noises", probe), Is.EqualTo(1)); Assert.That(TargetCount("shots", probe), Is.EqualTo(1));
                Shot(); Assert.That(probe.Noises, Is.EqualTo(i + 1)); Assert.That(probe.Shots, Is.EqualTo(i + 1));
                probe.gameObject.SetActive(false);
                Assert.That(TargetCount("noises", probe), Is.Zero); Assert.That(TargetCount("shots", probe), Is.Zero);
                Shot(); Assert.That(probe.Noises, Is.EqualTo(i + 1));
                probe.gameObject.SetActive(true); yield return null;
            }
        }

        [UnityTest] public IEnumerator DestroyedUnityTargets_AreSkippedAndReleasedEvenWithoutUnsubscribe()
        {
            var stale = Probe("Forgotten unsubscribe"); stale.UnsubscribeOnDisable = false;
            var live = Probe("Live observer");
            Object.Destroy(stale.gameObject); yield return null;
            Assert.That(TargetCount("noises", stale), Is.EqualTo(1), "Negative control: stale subscription exists before dispatch");
            Report(Noise());
            Assert.That(stale.Noises, Is.Zero); Assert.That(stale.Shots, Is.Zero);
            Assert.That(live.Noises, Is.EqualTo(1)); Assert.That(live.Shots, Is.EqualTo(1));
            Assert.That(TargetCount("noises", stale), Is.Zero); Assert.That(TargetCount("shots", stale), Is.Zero);
            stale.Register(); // A destroyed target cannot be reintroduced by a late registration.
            Assert.That(TargetCount("noises", stale), Is.Zero); Assert.That(TargetCount("shots", stale), Is.Zero);
        }

        [UnityTest] public IEnumerator SceneUnload_PrunesOnlyOldSceneAndRetainsPersistentObservers()
        {
            Scene old = SceneManager.GetActiveScene();
            var stale = Probe("Old scene observer"); stale.UnsubscribeOnDisable = false;
            var persistent = Probe("Persistent observer"); persistent.UnsubscribeOnDisable = false;
            Object.DontDestroyOnLoad(persistent.gameObject);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Next noise scene " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Assert.That(TargetCount("noises", stale), Is.Zero); Assert.That(TargetCount("shots", stale), Is.Zero);
            Assert.That(TargetCount("noises", persistent), Is.EqualTo(1)); Assert.That(TargetCount("shots", persistent), Is.EqualTo(1));
            Shot(); Assert.That(persistent.Noises, Is.EqualTo(1)); Assert.That(persistent.Shots, Is.EqualTo(1));
            Assert.That(stale.Noises, Is.Zero); Assert.That(stale.Shots, Is.Zero);
        }

        [UnityTest] public IEnumerator ActualMainLabRestart_CleansOldObserversAndKeepsOneTypedSubscriptionPerDrone()
        {
            yield return MainLab();
            string trace = "restart,old_observer_noise_refs,old_observer_shot_refs,new_hearing_sensors,legacy_scout_refs\n";
            for (int i = 0; i < 3; i++)
            {
                var scout = Find("ScoutBrain").Single();
                var oldSensors = Find("DroneHearingSensor");
                Assert.That(oldSensors.Length, Is.EqualTo(4));
                Assert.That(TargetCount("shots", scout), Is.Zero);
                foreach (var hearing in oldSensors) Assert.That(TargetCount("noises", hearing), Is.EqualTo(1));
                var observer = Probe("Old mission observer"); observer.UnsubscribeOnDisable = false;
                Shot(); Assert.That(observer.Noises, Is.EqualTo(1)); Assert.That(observer.Shots, Is.EqualTo(1));
                Call(Find("PlayerController").Single().GetComponent(T("Health")), "TakeDamage", 10000f);
                var session = Find("GameSession").Single(); Assert.That((bool)Get(session, "Ended"), Is.True);
                Call(session, "Restart"); yield return null; yield return null;
                PauseInputAndMovement();
                var fresh = Find("ScoutBrain").Single();
                Assert.That(ReferenceEquals(fresh, scout), Is.False);
                Assert.That(TargetCount("shots", scout), Is.Zero);
                Assert.That(TargetCount("noises", observer), Is.Zero); Assert.That(TargetCount("shots", observer), Is.Zero);
                foreach (var hearing in oldSensors) Assert.That(TargetCount("noises", hearing), Is.Zero);
                var newSensors = Find("DroneHearingSensor");
                Assert.That(newSensors.Length, Is.EqualTo(4));
                foreach (var hearing in newSensors) Assert.That(TargetCount("noises", hearing), Is.EqualTo(1));
                Assert.That(TargetCount("shots", fresh), Is.Zero);
                Assert.That(Subscribers("shots"), Is.Empty); Assert.That(Subscribers("noises").Length, Is.EqualTo(4));
                trace += $"{i + 1},0,0,4,0\n";
            }
            Write("noise-restart.csv", trace);
        }

        [UnityTest] public IEnumerator ActualPlayerShot_EmitsOncePerChannelAndPreservesDamageAndCooldown()
        {
            yield return MainLab(); var player = Find("PlayerController").Single(); var observer = Probe();
            var victim = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(victim);
            victim.transform.position = new Vector3(1000, 2, 1003);
            var health = victim.AddComponent(T("Health")); Call(health, "Configure", 100f, false);
            Physics.SyncTransforms(); float before = Time.time;
            Assert.That(PlayerWeaponTestActions.Fire(player, new Ray(new Vector3(1000, 2, 1000), Vector3.forward)), Is.True);
            Assert.That(observer.Noises, Is.EqualTo(1)); Assert.That(observer.Shots, Is.EqualTo(1));
            Assert.That((float)Get(health, "Current"), Is.EqualTo(75f));
            Assert.That(PlayerWeaponTestActions.Data(player, "Damage"), Is.EqualTo(25f));
            Assert.That(PlayerWeaponTestActions.Data(player, "FireInterval"), Is.EqualTo(.24f));
            Assert.That(PlayerWeaponTestActions.NextShotTime(player), Is.EqualTo(before + .24f).Within(.00001f));
            object noise = observer.LastNoise;
            Assert.That(NoiseType.GetField("Position").GetValue(noise), Is.EqualTo(player.transform.position));
            Assert.That(NoiseType.GetField("Category").GetValue(noise).ToString(), Is.EqualTo("Gunshot"));
            Assert.That(NoiseType.GetField("Intensity").GetValue(noise), Is.EqualTo(1f));
            Assert.That((float)NoiseType.GetField("Timestamp").GetValue(noise), Is.InRange(before, Time.time));
            Assert.That(NoiseType.GetField("Source").GetValue(noise), Is.Null, "Legacy producer remains unchanged in Task 3");
        }

        [UnityTest] public IEnumerator LiveReport_CapturesClockAndOptionalSourceWithoutTrackingIt()
        {
            var probe = Probe(); var source = new GameObject("Noise source identity"); owned.Add(source);
            Vector3 emission = new Vector3(4, 5, 6); source.transform.position = emission;
            float before = Time.time;
            var report = Bus.GetMethod("ReportNoise", new[] { typeof(Vector3), T("NoiseCategory"), typeof(float), typeof(Object) });
            Assert.That(report.Invoke(null, new object[] { emission, Category("Footstep"), .2f, source }), Is.True);
            source.transform.position = Vector3.one * 50f; yield return null;
            Assert.That(probe.Noises, Is.EqualTo(1)); Assert.That(probe.Shots, Is.Zero);
            Assert.That(NoiseType.GetField("Position").GetValue(probe.LastNoise), Is.EqualTo(emission));
            Assert.That(NoiseType.GetField("Source").GetValue(probe.LastNoise), Is.SameAs(source));
            Assert.That((float)NoiseType.GetField("Timestamp").GetValue(probe.LastNoise), Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(Time.time));
        }

        [UnityTest] public IEnumerator SubsystemResetAndEnableCycles_DoNotAccumulateStaticListeners()
        {
            var probe = Probe();
            for (int i = 0; i < 3; i++)
            {
                Reset(); Reset(); Shot(); Assert.That(probe.Noises, Is.EqualTo(i));
                Assert.That(Subscribers("noises"), Is.Empty); Assert.That(Subscribers("shots"), Is.Empty);
                probe.gameObject.SetActive(false); probe.gameObject.SetActive(true);
                Shot(); Assert.That(probe.Noises, Is.EqualTo(i + 1)); Assert.That(probe.Shots, Is.EqualTo(i + 1));
                Assert.That(TargetCount("noises", probe), Is.EqualTo(1)); Assert.That(TargetCount("shots", probe), Is.EqualTo(1));
                yield return null;
            }
        }

        [UnityTest] public IEnumerator WarmDispatch_RecordsNoAllocationEventsForBothGunshotEntryPoints()
        {
            int noiseCalls = 0, shotCalls = 0;
            var value = Expression.Parameter(NoiseType, "noise"); Action count = () => noiseCalls++;
            var listener = Expression.Lambda(Bus.GetEvent("NoiseEmitted").EventHandlerType,
                Expression.Invoke(Expression.Constant(count)), value).Compile();
            Bus.GetEvent("NoiseEmitted").AddEventHandler(null, listener);
            Bus.GetEvent("ShotFired").AddEventHandler(null, (Action<Vector3>)(_ => shotCalls++));
            var call = Expression.Call(Bus.GetMethod("ReportNoise", new[] { NoiseType }), Expression.Constant(Noise(), NoiseType));
            var report = Expression.Lambda<Action>(Expression.Block(call, Expression.Empty())).Compile();
            var legacy = (Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>), Bus.GetMethod("ReportShot"));
            for (int i = 0; i < 32; i++) { report(); legacy(Vector3.zero); }
            long calibration = MeasuredAllocation.Calibrate();
            int beforeNoise = noiseCalls, beforeShot = shotCalls;
            Action workload = () => { for (int i = 0; i < 1000; i++) { report(); legacy(Vector3.zero); } };
            long events = MeasuredAllocation.Events(workload, out int samples);
            Assert.That(events, Is.Zero);
            Assert.That(noiseCalls - beforeNoise, Is.EqualTo(2000)); Assert.That(shotCalls - beforeShot, Is.EqualTo(2000));
            Write("noise-dispatch.csv", $"logical_reports,modern_notifications,legacy_notifications,allocation_events,marker_samples,calibration_events\n2000,2000,2000,{events},{samples},{calibration}\n");
            yield return null;
        }

        private static void Write(string name, string content)
        {
            string path = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/NoiseEvidence";
            Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, name), content);
        }
    }
}
#endif
