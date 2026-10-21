#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.IO;
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
    public sealed class RapidResponseAudioPlayModeTests
    {
        private Component player, session, audio;
        private Component[] brains;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private float captureDelta;
        private int frameRate;
        private Action restoreEditorMute;
        private bool previousRunInBackground;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static Component[] Find(string n) => Object.FindObjectsByType(T(n), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name, params object[] args)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(target, args);
            }
            throw new MissingMethodException(name);
        }
        private static void Set(object target, string name, object value)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) { f.SetValue(target, value); return; }
            }
            throw new MissingFieldException(name);
        }
        private static bool Emit(Vector3 position, string category, Object source = null) => (bool)T("SoundEvents")
            .GetMethod("ReportNoise", new[] { typeof(Vector3), T("NoiseCategory"), typeof(float), typeof(Object) })
            .Invoke(null, new object[] { position, Enum.Parse(T("NoiseCategory"), category), 1f, source });
        private void MovePlayer(Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = position; cc.enabled = true; Call(player, "ApplyCameraPose"); Physics.SyncTransforms();
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            captureDelta = Time.captureDeltaTime; frameRate = Application.targetFrameRate; previousRunInBackground = Application.runInBackground;
            Time.captureDeltaTime = 1f / 60f; Application.targetFrameRate = 120;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = Find("PlayerController").Single(); session = Find("GameSession").Single();
            audio = Find("GameplayAudio").Single(); brains = Find("DroneBrain");
            ((Behaviour)player).enabled = false;
            foreach (var brain in brains)
            {
                ((Behaviour)brain).enabled = false;
                ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                brain.GetComponent<NavMeshAgent>().isStopped = true;
            }
            MovePlayer(new Vector3(100, 0, 100));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            restoreEditorMute?.Invoke(); restoreEditorMute = null; Application.runInBackground = previousRunInBackground;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After rapid response audio"));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            Time.captureDeltaTime = captureDelta; Application.targetFrameRate = frameRate;
        }

        [UnityTest] public IEnumerator AllFourBrains_AudibleEventWakesPendingDecisionWithoutVisualConfirmation()
        {
            foreach (var brain in brains)
            {
                Assert.That((float)Get(brain, "DecisionInterval"), Is.EqualTo(.08f).Within(.001f));
                Set(brain, "nextDecision", float.MaxValue);
                ((Behaviour)brain).enabled = true;
            }
            float started = Time.time;
            foreach (var brain in brains) Assert.That(Emit((Vector3)Get(brain, "Home"), "Impact"), Is.True);
            while (brains.Any(b => !Get(b, "CurrentDecision").ToString().Contains("Investigat")) && Time.time - started < .15f)
                yield return null;
            foreach (var brain in brains)
            {
                Assert.That(Get(brain, "CurrentDecision").ToString(), Does.Contain("Investigat"), brain.name);
                Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.False, brain.name);
                Assert.That(Get(brain.GetComponent(T("DroneMotor")), "MovementMode").ToString(), Is.EqualTo("Investigate"));
            }
            Debug.Log("[Rapid response QA] All four brains investigated after " + (Time.time - started).ToString("F4") + " simulated seconds");
        }

        [UnityTest] public IEnumerator OwnGunshot_IsNotReprocessedByShootersHearing()
        {
            foreach (var brain in brains)
            {
                var sensor = Get(brain, "Hearing"); int before = (int)Get(sensor, "EvaluationCount");
                Assert.That(Emit(brain.transform.position, "Gunshot", brain), Is.True);
                Assert.That(Get(sensor, "EvaluationCount"), Is.EqualTo(before), brain.name);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator Guard_PressuresVisiblePlayerBeyondOldFourMetreLimit()
        {
            var guard = brains.Single(b => b.GetType().Name == "GuardBrain");
            Assert.That(guard.GetComponent<NavMeshAgent>().Warp(new Vector3(-3, .01333333f, 4)), Is.True);
            MovePlayer(new Vector3(-3, 0, 10)); guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms(); Assert.That(Call(guard, "CanSeePlayer"), Is.True);
            Set(guard, "nextDecision", 0f); ((Behaviour)guard).enabled = true;
            yield return null;
            Assert.That(Get(guard, "CurrentDecision"), Is.EqualTo("Pressure visible player"));
            Assert.That(Get(Get(guard, "Awareness"), "HasVisualConfirmation"), Is.True);
        }

        [UnityTest] public IEnumerator AcceptedPlayerAndDroneShots_PlayOnce_DeclinedShotsStaySilent()
        {
            yield return PlayerWeaponTestActions.ReadyForCombat(player);
            int before = (int)Get(audio, "PlayerShotsPlayed");
            var camera = (Camera)Get(player, "ViewCamera"); var ray = new Ray(camera.transform.position, Vector3.up);
            Assert.That(PlayerWeaponTestActions.Fire(player, ray), Is.True);
            Assert.That(Get(audio, "PlayerShotsPlayed"), Is.EqualTo(before + 1));
            Assert.That(PlayerWeaponTestActions.Fire(player, ray), Is.False);
            Assert.That(Get(audio, "PlayerShotsPlayed"), Is.EqualTo(before + 1));
            ((Behaviour)player).enabled = false;
            var scout = brains.Single(b => b.GetType().Name == "ScoutBrain");
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(-10, .01333333f, -10)), Is.True);
            MovePlayer(new Vector3(-10, 0, -14)); scout.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms(); Set(scout, "nextShot", 0f);
            before = (int)Get(audio, "DroneShotsPlayed");
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            Call(scout, "TryShoot"); Call(scout, "TryShoot");
            Assert.That(Get(audio, "DroneShotsPlayed"), Is.EqualTo(before + 1));
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp - 10f));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(16));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator DoorSound_StartsOnceOnUnlocking_AndPassageStillWaitsForAnimation()
        {
            var door = Find("LabDoor").Single(d => (int)Get(d, "GateId") == 0);
            int before = (int)Get(audio, "DoorsPlayed");
            Call(door, "Interact"); yield return new WaitForFixedUpdate();
            Assert.That(Get(audio, "DoorsPlayed"), Is.EqualTo(before));
            var progress = Get(session, "Progress"); Call(progress, "DefeatDrone", 0); Call(progress, "ActivateConsole", 0);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(Get(audio, "DoorsPlayed"), Is.EqualTo(before + 1));
            Assert.That(door.GetComponent<BoxCollider>().enabled, Is.True);
            yield return new WaitForSeconds((float)Get(door, "OpeningDuration") + .15f);
            Assert.That(Get(door, "IsOpen"), Is.True); Assert.That(door.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(Get(audio, "DoorsPlayed"), Is.EqualTo(before + 1));
        }

        [UnityTest] public IEnumerator AudioReenable_DoesNotDuplicateCallbacksOrGrowVoicePool()
        {
            for (int i = 0; i < 4; i++) { ((Behaviour)audio).enabled = false; ((Behaviour)audio).enabled = true; }
            int before = (int)Get(audio, "PlayerShotsPlayed"); Emit(player.transform.position, "Gunshot");
            Assert.That(Get(audio, "PlayerShotsPlayed"), Is.EqualTo(before + 1));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(16));
            yield return null;
        }

        [UnityTest, Timeout(15000)] public IEnumerator ActualAudioMixer_RecordsNonSilentPlayerDroneAndDoorEffects()
        {
            if (Application.isBatchMode) Assert.Ignore("Audio output recording needs graphical frame-end rendering; PCM and playback-event tests run in batch mode.");
            var viewType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.GameView")).First(t => t != null);
            var view = UnityEditor.EditorWindow.GetWindow(viewType); view.Show(); view.Focus(); Application.runInBackground = true;
            foreach (var type in new[] { typeof(UnityEditor.EditorUtility),
                AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.AudioUtil")).FirstOrDefault(t => t != null) })
            {
                if (type == null) continue;
                foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (property.PropertyType != typeof(bool) || !property.CanRead || !property.CanWrite ||
                        property.GetIndexParameters().Length != 0 || !property.Name.ToLowerInvariant().Contains("mute")) continue;
                    bool value = (bool)property.GetValue(null);
                    Debug.Log("[Audio editor QA] " + type.Name + "." + property.Name + "=" + value);
                    if (value) { property.SetValue(null, false); restoreEditorMute += () => property.SetValue(null, value); }
                }
            }
            MovePlayer(new Vector3(-15, 0, -15));
            yield return PlayerWeaponTestActions.ReadyForCombat(player);
            Assert.That(AudioSettings.speakerMode, Is.EqualTo(AudioSpeakerMode.Stereo));
            Debug.Log("[Audio capture configuration] rate=" + AudioSettings.outputSampleRate +
                "; captureHz=" + Time.captureFramerate + "; listenerPaused=" + AudioListener.pause +
                "; listenerVolume=" + AudioListener.volume + "; batch=" + Application.isBatchMode + "; dspTime=" + AudioSettings.dspTime);
            try
            {
                var camera = (Camera)Get(player, "ViewCamera");
                Assert.That(PlayerWeaponTestActions.Fire(player, new Ray(camera.transform.position, Vector3.up)), Is.True);
                ((Behaviour)player).enabled = false;
                var listener = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Single();
                var live = audio.GetComponentsInChildren<AudioSource>().Where(s => s.isPlaying).ToArray();
                Debug.Log("[Audio playback QA] listener=" + listener.name + "; enabled=" + listener.enabled +
                    "; parentCamera=" + (listener.GetComponent<Camera>() == camera) + "; playingVoices=" + live.Length +
                    "; clips=" + string.Join(",", live.Select(s => s.clip.name + ":" + s.clip.loadState + ":" + s.volume + ":mute=" + s.mute + ":blend=" + s.spatialBlend + ":virtual=" + s.isVirtual)));
                yield return RecordMixer("player-gunshot", 30);
            }
            finally { }
            ((Behaviour)player).enabled = false;
            var scout = brains.Single(b => b.GetType().Name == "ScoutBrain");
            Assert.That(scout.GetComponent<NavMeshAgent>().Warp(new Vector3(-10, .01333333f, -10)), Is.True);
            MovePlayer(new Vector3(-10, 0, -14)); scout.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Physics.SyncTransforms(); Set(scout, "nextShot", 0f);
            yield return null;
            Assert.That(Call(scout, "CanSeePlayer"), Is.True, "Capture arrangement must provide a clear real shot");
            int droneBefore = (int)Get(audio, "DroneShotsPlayed");
            try
            {
                Call(scout, "TryShoot");
                Assert.That(Get(audio, "DroneShotsPlayed"), Is.EqualTo(droneBefore + 1));
                Debug.Log("[Audio drone QA] listener=" + ((Camera)Get(player, "ViewCamera")).transform.position +
                    "; voices=" + string.Join(",", audio.GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying)
                        .Select(s=>s.clip.name+":"+s.transform.position+":volume="+s.volume+":virtual="+s.isVirtual)));
                yield return RecordMixer("drone-gunshot", 30);
            }
            finally { }
            var door = Find("LabDoor").Single(d => (int)Get(d, "GateId") == 0);
            MovePlayer(door.transform.position + Vector3.back * 3f);
            try
            {
                var progress = Get(session, "Progress"); Call(progress, "DefeatDrone", 0); Call(progress, "ActivateConsole", 0);
                yield return RecordMixer("door-opening", 110);
            }
            finally { }
        }

        private static IEnumerator RecordMixer(string name, int frames)
        {
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE");
            if (string.IsNullOrEmpty(directory)) directory = Path.Combine(Application.temporaryCachePath, "ShiftFourAudioEvidence");
            Directory.CreateDirectory(directory);
            var samples = new float[1024]; float peak = 0; double energy = 0; int total = 0;
            float started = Time.realtimeSinceStartup, duration = frames / 60f;
            using (var writer = new StreamWriter(Path.Combine(directory, name + "-listener.csv")))
            {
                writer.WriteLine("realtime_seconds,peak,rms");
                while (Time.realtimeSinceStartup - started < duration)
                {
                    yield return null;
                    AudioListener.GetOutputData(samples, 0);
                    float framePeak = 0; double frameEnergy = 0;
                    foreach (float value in samples)
                    {
                        framePeak = Mathf.Max(framePeak, Mathf.Abs(value)); frameEnergy += value * value;
                    }
                    peak = Mathf.Max(peak, framePeak); energy += frameEnergy; total += samples.Length;
                    writer.WriteLine((Time.realtimeSinceStartup - started).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        framePeak.ToString("G9", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        Math.Sqrt(frameEnergy / samples.Length).ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            Assert.That(total, Is.GreaterThan(0)); Assert.That(peak, Is.GreaterThan(.001f), name + " listener output");
            Debug.Log("[Audio listener QA] " + name + ": sampled=" + total + "; peak=" + peak.ToString("F5") +
                "; RMS=" + Math.Sqrt(energy / total).ToString("F5"));
        }

    }
}
#endif
