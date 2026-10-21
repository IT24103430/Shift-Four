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
        private static object FieldValue(object target, string name)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
            }
            throw new MissingFieldException(name);
        }
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
                // The first scene frame can now spot the spawn behind a stationed drone.
                // These fixtures begin with fresh awareness after parking the player elsewhere.
                var oldAwareness = Get(brain, "Awareness");
                Set(brain, "<Awareness>k__BackingField", Activator.CreateInstance(T("DroneAwareness"),
                    FieldValue(oldAwareness, "settings"), Get(brain, "DifficultySettings")));
                Set(brain, "<HasCommittedToAttack>k__BackingField", false);
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

        [UnityTest] public IEnumerator EachBrain_AudibleEventWakesItsOwnPolicyWithoutVisualConfirmation()
        {
            foreach (var brain in brains)
            {
                Assert.That((float)Get(brain, "DecisionInterval"), Is.EqualTo(.08f).Within(.001f));
                var hearing = Get(brain, "Hearing");
                var settings = FieldValue(hearing, "settings");
                Assert.That(settings.GetType().GetField("queriesPerFrame").GetValue(settings), Is.GreaterThanOrEqualTo(4));
                // Isolate wake-up from uncertain placement and simultaneous equal-time clues.
                settings.GetType().GetField("minimumUncertainty").SetValue(settings, 0f);
                settings.GetType().GetField("additionalUncertainty").SetValue(settings, 0f);
                Set(brain, "nextDecision", float.MaxValue);
                ((Behaviour)brain).enabled = true;
                float started = Time.time;
                Assert.That(Emit(brain.transform.position, "Gunshot"), Is.True);
                while (((float)FieldValue(brain, "nextDecision") <= started ||
                    (float)FieldValue(brain, "nextDecision") == float.MaxValue) && Time.time - started < .15f)
                    yield return null;
                Assert.That((float)FieldValue(brain, "nextDecision"), Is.LessThan(float.MaxValue), brain.name);
                Assert.That((float)FieldValue(brain, "nextDecision"), Is.GreaterThan(started), brain.name);
                Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.False, brain.name);
                if (brain.GetType().Name == "ScoutBrain")
                {
                    Assert.That(Get(brain, "CurrentDecision"), Is.EqualTo("Turn toward sound"));
                    Assert.That(Get(Get(brain, "Awareness"), "State").ToString(), Is.EqualTo("Suspicious"));
                }
                else Assert.That(Get(brain, "CurrentDecision").ToString(), Does.Contain("Investigat"), brain.name);
                ((Behaviour)brain).enabled = false;
                yield return null;
            }
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

        [UnityTest] public IEnumerator Guard_UsesCoverForVisiblePlayerBeyondFourMetres()
        {
            var guard = brains.Single(b => b.GetType().Name == "GuardBrain");
            Assert.That(guard.GetComponent<NavMeshAgent>().Warp(new Vector3(-3, .01333333f, 4)), Is.True);
            MovePlayer(new Vector3(-3, 0, 10)); guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms(); Assert.That(Call(guard, "CanSeePlayer"), Is.True);
            Set(guard, "nextDecision", 0f); ((Behaviour)guard).enabled = true;
            yield return null;
            Assert.That(Get(guard, "CurrentDecision").ToString(), Is.EqualTo("Take protected cover").Or.EqualTo("Reposition"));
            Assert.That((Vector3)FieldValue(guard.GetComponent(T("DroneMotor")), "goal"),
                Is.EqualTo((Vector3)FieldValue(guard, "committedCover")));
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
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Count(s => s.name.StartsWith("Gameplay audio voice ")), Is.EqualTo(16));
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
            var sound = door.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "LabDoorOpen");
            Assert.That(sound.spatialBlend, Is.EqualTo(1f));
            Assert.That(sound.pitch, Is.EqualTo(1f));
            Assert.That(sound.clip.length, Is.EqualTo(4.824f).Within(.01f));
            Assert.That(sound.volume, Is.EqualTo(.85f).Within(.001f));
            Assert.That(sound.minDistance, Is.EqualTo(8f));
            Assert.That(sound.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            for (int i = 0; i < 64; i++) Emit(player.transform.position, "Gunshot");
            Assert.That(sound.isPlaying, Is.True, "Gunshots must not steal the door's voice.");
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
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Count(s => s.name.StartsWith("Gameplay audio voice ")), Is.EqualTo(16));
            yield return null;
        }

        [UnityTest] public IEnumerator BackgroundMusic_LoopsQuietlyAndReenableDoesNotDuplicateSources()
        {
            var music = audio.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "BackgroundMusic");
            yield return null;
            Assert.That(music.loop, Is.True);
            Assert.That(music.spatialBlend, Is.EqualTo(0f));
            Assert.That(music.volume, Is.EqualTo(.12f).Within(.001f));
            Assert.That(music.isPlaying, Is.True);
            for (int i = 0; i < 3; i++)
            {
                ((Behaviour)audio).enabled = false;
                Assert.That(music.isPlaying, Is.False);
                ((Behaviour)audio).enabled = true;
                yield return null;
                Assert.That(music.isPlaying, Is.True);
            }
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Count(s => s.clip != null && s.clip.name == "BackgroundMusic"), Is.EqualTo(1));
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(17));
        }

        [UnityTest] public IEnumerator ReloadAudio_OnlyAcceptedReloadsPlayAndCancelOrDeathStopsPlayback()
        {
            yield return PlayerWeaponTestActions.ReadyForCombat(player);
            var weapons = PlayerWeaponTestActions.Controller(player);
            var source = player.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "PlayerReload");
            Assert.That(Call(weapons, "RequestReload"), Is.False, "A full magazine must stay silent.");
            Assert.That(Get(audio, "ReloadsPlayed"), Is.EqualTo(0));
            var camera = (Camera)Get(player, "ViewCamera");
            Assert.That(PlayerWeaponTestActions.Fire(player, new Ray(camera.transform.position, Vector3.up)), Is.True);
            Assert.That(Call(weapons, "RequestReload"), Is.True);
            Assert.That(source.isPlaying, Is.True); Assert.That(source.spatialBlend, Is.EqualTo(0f));
            Assert.That(Get(audio, "ReloadsPlayed"), Is.EqualTo(1));
            Assert.That(Call(weapons, "RequestReload"), Is.False);
            Assert.That(Get(audio, "ReloadsPlayed"), Is.EqualTo(1));
            Assert.That(Call(weapons, "CancelReload"), Is.True); Assert.That(source.isPlaying, Is.False);
            for (int i = 0; i < 3; i++) { ((Behaviour)audio).enabled = false; ((Behaviour)audio).enabled = true; }
            Assert.That(Call(weapons, "RequestReload"), Is.True);
            Assert.That(Get(audio, "ReloadsPlayed"), Is.EqualTo(2), "Re-enabling audio must not duplicate reload subscriptions.");
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            Assert.That(source.isPlaying, Is.False);
        }

        [UnityTest] public IEnumerator Scout_CommitsAndKeepsAttacking() => CheckSustainedAttack("ScoutBrain");
        [UnityTest] public IEnumerator Guard_CommitsAndKeepsAttacking() => CheckSustainedAttack("GuardBrain");
        [UnityTest] public IEnumerator Flanker_CommitsAndKeepsAttacking() => CheckSustainedAttack("FlankerBrain");
        [UnityTest] public IEnumerator Interceptor_CommitsAndKeepsAttacking() => CheckSustainedAttack("InterceptorBrain");

        private IEnumerator CheckSustainedAttack(string type)
        {
            var brain = brains.Single(b => b.GetType().Name == type);
            foreach (var other in brains)
                if (other != brain)
                    foreach (var collider in other.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Assert.That(brain.GetComponent<NavMeshAgent>().Warp(new Vector3(-10, .01333333f, -10)), Is.True);
            MovePlayer(new Vector3(-10, 0, -14));
            // Look away: a stationed drone must still acquire a nearby clear target.
            brain.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms();
            Assert.That(Call(brain, "CanSeePlayer"), Is.True);
            float hp = (float)Get(player.GetComponent(T("Health")), "Current");
            Set(brain, "nextShot", 0f); Call(brain, "TryShoot");
            Assert.That(Get(brain, "HasCommittedToAttack"), Is.True);
            float shotDamage = (float)Get(brain, "ShotDamage");
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp - shotDamage));
            float interval = (float)FieldValue(brain, "nextShot") - Time.time;
            Assert.That(interval, Is.EqualTo(type == "GuardBrain" ? .825f : 1.1f).Within(.001f));
            yield return new WaitForSeconds(interval + .05f);
            Call(brain, "TryShoot");
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp - 2f * shotDamage));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Sustained attack test cover";
            wall.transform.position = new Vector3(-10, 1.3f, -12);
            wall.transform.localScale = new Vector3(3, 3, .4f);
            Physics.SyncTransforms();
            try
            {
                Set(brain, "nextShot", 0f); Set(brain, "nextDecision", 0f); Call(brain, "Update");
                Assert.That(Get(brain, "CurrentDecision"), Is.EqualTo("Pursue engaged player"));
                Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(hp - 2f * shotDamage));
                Assert.That(Get(Get(brain, "Awareness"), "State").ToString(), Is.EqualTo("Combat"));
            }
            finally { Object.Destroy(wall); }
            yield return null;
            Call(player.GetComponent(T("Health")), "TakeDamage", 1000f);
            Set(brain, "nextShot", 0f); Call(brain, "TryShoot");
            Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(0f));
        }

        [UnityTest] public IEnumerator FlightLoops_FollowDronesAndStopOnDeathAndSessionEnd()
        {
            yield return null;
            foreach (var brain in brains)
            {
                var loop = brain.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "DroneFlight");
                Assert.That(loop.loop, Is.True); Assert.That(loop.spatialBlend, Is.EqualTo(1f));
                Assert.That(loop.volume, Is.EqualTo(.45f).Within(.001f));
                Assert.That(loop.minDistance, Is.EqualTo(4f));
                Assert.That(loop.isPlaying, Is.True);
                Assert.That(loop.transform.localPosition, Is.EqualTo(Vector3.zero));
            }
            var first = brains[0].GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "DroneFlight");
            Call(brains[0].GetComponent(T("Health")), "TakeDamage", 10000f);
            yield return null;
            Assert.That(first.isPlaying, Is.False);
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            yield return null;
            foreach (var brain in brains)
                Assert.That(brain.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "DroneFlight").isPlaying, Is.False);
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
            // Isolate the supplied door recording so music/flight cannot mask a silent door.
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.mute = true;
            door.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "LabDoorOpen").mute = false;
            try
            {
                var progress = Get(session, "Progress"); Call(progress, "DefeatDrone", 0); Call(progress, "ActivateConsole", 0);
                yield return RecordMixer("door-opening", 110);
            }
            finally { }
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) source.mute = true;
            var reload = player.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "PlayerReload");
            reload.mute = false;
            yield return PlayerWeaponTestActions.ReadyForCombat(player);
            Assert.That(Call(PlayerWeaponTestActions.Controller(player), "RequestReload"), Is.True);
            yield return RecordMixer("player-reload", 90);
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
