#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class BombPlantingPlayModeTests
    {
        private Component session, bomb, player;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args);
        private static void Field(object owner, string name, object value) => owner.GetType().GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = (Component)Object.FindAnyObjectByType(T("GameSession"));
            bomb = (Component)Object.FindAnyObjectByType(T("BombConsole"));
            player = (Component)Get(session, "Player");
            foreach (var brain in Object.FindObjectsByType(T("DroneBrain"))) ((Behaviour)brain).enabled = false;
            var phase = session.GetType().GetProperty("Phase");
            phase.SetValue(session, Enum.Parse(phase.PropertyType, "Playing"));
            Time.timeScale = 1f;
        }
        private void ClearObjectives()
        {
            var progress = Get(session, "Progress");
            for (int i = 0; i < 4; i++) { Call(progress, "DefeatDrone", i); Call(progress, "ActivateConsole", i); }
        }
        private void FaceStation()
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = bomb.transform.position + new Vector3(0, -.45f, -1.5f);
            body.enabled = true;
            Call(player, "ApplyCameraPose");
            ((Camera)Get(player, "ViewCamera")).transform.LookAt(bomb.transform.position + Vector3.up * .48f);
        }
        private static IEnumerator Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/BombPlantingQA";
            Directory.CreateDirectory(directory);
            var window = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            // Use a fixed landscape Game view so evidence matches a normal desktop play session.
            var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
            var groupType = group.GetType();
            int index = (int)groupType.GetMethod("GetBuiltinCount").Invoke(group, null) +
                (int)groupType.GetMethod("GetCustomCount").Invoke(group, null);
            var sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            var kind = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kind, "FixedResolution"), 1280, 720, "Planting QA" });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(window, index);
            window.Repaint();
            yield return new WaitForSecondsRealtime(.15f);
            for (Type type = window.GetType(); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (field.GetValue(window) is RenderTexture rt && rt.IsCreated() && rt.width >= 320)
                    {
                        var previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        try
                        {
                            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                            using var writer = new BinaryWriter(File.Create(Path.Combine(directory, name + ".tga")));
                            writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                            writer.Write((short)rt.width); writer.Write((short)rt.height);
                            writer.Write((byte)24); writer.Write((byte)(SystemInfo.graphicsUVStartsAtTop ? 0x20 : 0));
                            foreach (Color32 pixel in image.GetPixels32()) { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
                        }
                        finally { RenderTexture.active = previous; Object.Destroy(image); }
                        yield break;
                    }
        }

        [UnityTest]
        public IEnumerator FirstDoor_RequiresCompletedPlantingEvenAfterScoutAndConsoleAreCleared()
        {
            Component gate = null;
            foreach (Component candidate in Object.FindObjectsByType(T("LabDoor")))
                if ((int)Get(candidate, "GateId") == 0) gate = candidate;
            Assert.That(gate, Is.Not.Null);
            var progress = Get(session, "Progress");
            Call(progress, "DefeatDrone", 0);
            Call(progress, "ActivateConsole", 0);
            Call(gate, "Interact");
            yield return new WaitForSeconds(2f);
            Assert.That(Get(gate, "Status"), Is.EqualTo("LOCKED"));
            Assert.That(Get(gate, "Prompt").ToString(), Does.Contain("plant the bomb"));
            Assert.That(gate.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(gate.GetComponent<NavMeshObstacle>().enabled, Is.True);
            Assert.That(Call(Get(session, "Graph"), "IsGateOpen", 0), Is.False);
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(0));
            yield return Capture("room1-planting-required");

            FaceStation(); Call(bomb, "Interact");
            yield return new WaitForSeconds(1f);
            Assert.That(Get(gate, "IsOpen"), Is.False);
            Assert.That(Call(Get(session, "Graph"), "IsGateOpen", 0), Is.False);
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(0));
            while ((bool)Get(session, "IsPlanting")) yield return null;
            yield return new WaitForSeconds(2f);
            Assert.That(Get(gate, "IsOpen"), Is.True);
            Assert.That(gate.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(gate.GetComponent<NavMeshObstacle>().enabled, Is.False);
            Assert.That(Call(Get(session, "Graph"), "IsGateOpen", 0), Is.True);
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RoomTasks_AdvanceOnlyWhenAllTasksInTheRoomAreComplete()
        {
            FaceStation();
            var progress = Get(session, "Progress");
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(0));
            yield return Capture("tasks-room1");
            Call(bomb, "Interact");
            yield return new WaitForSeconds(5.2f);
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(0), "Planting alone does not clear Checkpoint.");
            for (int room = 0; room < 4; room++)
            {
                Assert.That(Get(session, "TaskRoom"), Is.EqualTo(room));
                Call(progress, "DefeatDrone", room);
                Assert.That(Get(session, "TaskRoom"), Is.EqualTo(room), "The console is still required.");
                yield return Capture("tasks-room" + (room + 1) + "-drone-complete");
                Call(progress, "ActivateConsole", room);
                Assert.That(Get(session, "TaskRoom"), Is.EqualTo(room + 1));
                // Walking back into an earlier room must not replace the next unfinished objective.
                Call(session, "EnterRoom", 0);
                Assert.That(Get(session, "TaskRoom"), Is.EqualTo(room + 1));
            }
            Assert.That(Get(session, "TaskRoom"), Is.EqualTo(4));
            Assert.That(Get(session, "Won"), Is.False, "Room completion must still require extraction.");
            yield return Capture("tasks-extraction");
        }

        [UnityTest]
        public IEnumerator CheckpointStation_IsReachableBeforeAnySectorIsCleared_AndHasNoActiveTimer()
        {
            Vector3 approach = bomb.transform.position + new Vector3(0, -.45f, -1.5f);
            Assert.That(NavMesh.SamplePosition(approach, out var destination, .4f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(player.transform.position, out var start, 1f, NavMesh.AllAreas), Is.True);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start.position, destination.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "No locked gate may separate spawn from the planting station.");
            FaceStation();
            Vector3 eye = ((Camera)Get(player, "ViewCamera")).transform.position;
            Vector3 ray = bomb.GetComponent<Collider>().bounds.center - eye;
            Assert.That(Physics.Raycast(eye, ray.normalized, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.GetComponent(T("BombConsole")), Is.SameAs(bomb));
            float remaining = (float)Get(session, "BombRemaining");
            yield return new WaitForSeconds(1f);
            Assert.That(Get(session, "MissionTimerActive"), Is.False);
            Assert.That(Get(session, "BombRemaining"), Is.EqualTo(remaining));
            Assert.That(Get(session, "ConsolesOnline"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator PlantingMotion_UsesRiggedArms_ContinuousCameraAndCarry_RestoresWeapon()
        {
            FaceStation();
            var camera = (Camera)Get(player, "ViewCamera");
            Vector3 cameraRest = camera.transform.position;
            Quaternion rotationRest = camera.transform.rotation;
            var weapon = Get(player, "Weapons");
            var state = Get(weapon, "ActiveWeapon");
            var ammo = Get(state, "RoundsInMagazine");
            Call(bomb, "Interact");
            var charge = bomb.transform.Find("Plantable time bomb");
            var rig = camera.GetComponentInChildren(T("FirstPersonArmsRig"), true);
            Assert.That(rig, Is.Not.Null);
            Vector3 previousCharge = charge.position, previousCamera = camera.transform.position;
            float previousTime = Time.time, started = Time.time;
            bool capturedPlace = false, capturedPress = false;
            while ((bool)Get(session, "IsPlanting") && Time.time - started < 6f)
            {
                Assert.That(Get(session, "MissionTimerActive"), Is.False);
                Assert.That(Call(weapon, "TryFire"), Is.False);
                float dt = Mathf.Max(.001f, Time.time - previousTime);
                Assert.That(Vector3.Distance(previousCharge, charge.position), Is.LessThan(6f * dt + .01f), "Charge must move continuously.");
                Assert.That(Vector3.Distance(previousCamera, camera.transform.position), Is.LessThan(6f * dt + .01f), "Camera must ease in and out.");
                previousCharge = charge.position; previousCamera = camera.transform.position; previousTime = Time.time;
                if (!capturedPlace && Time.time - started > 2.5f) { capturedPlace = true; yield return Capture("placing-rigged-arms"); }
                if (!capturedPress && Time.time - started > 3.4f) { capturedPress = true; yield return Capture("arming-keypad"); }
                yield return null;
            }
            Assert.That(Get(session, "BombPlanted"), Is.True);
            Assert.That(Vector3.Distance(cameraRest, camera.transform.position), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(rotationRest, camera.transform.rotation), Is.LessThan(.1f));
            Assert.That(Get(state, "RoundsInMagazine"), Is.EqualTo(ammo));
            yield return new WaitForSeconds(.5f);
            Assert.That(Get(player.GetComponent(T("FirstPersonWeaponMount")), "IsReady"), Is.True);
            Assert.That(camera.transform.Find("Bomb planting arms"), Is.Null);
        }

        [UnityTest]
        public IEnumerator CheckpointPlanting_AnimatesBeforeStartingDestructionTimer()
        {
            Assert.That(bomb.transform.position.x, Is.LessThan(0f));
            Assert.That(bomb.transform.position.z, Is.LessThan(0f));
            Assert.That(Object.FindObjectsByType(T("BombConsole")).Length, Is.EqualTo(1));
            Assert.That(Get(session, "MissionTimerActive"), Is.False);
            Assert.That(Get(session, "BombPlanted"), Is.False);
            FaceStation();
            Call(bomb, "Interact");
            Assert.That(Get(session, "IsPlanting"), Is.True);
            Assert.That(Get(player, "CanAct"), Is.False);
            var charge = bomb.transform.Find("Plantable time bomb");
            Assert.That(Get(session, "MissionTimerActive"), Is.False);
            Vector3 start = charge.position;
            Call(session, "TryExit");
            Assert.That(Get(session, "Won"), Is.False);
            yield return new WaitForSeconds(1.6f);
            Assert.That(Vector3.Distance(charge.position, start), Is.GreaterThan(.1f));
            Assert.That((float)Get(bomb, "PlantingProgress"), Is.InRange(.1f, .5f));
            Call(bomb, "Interact"); // Repeated interaction must not restart the sequence.
            yield return Capture("planting");
            yield return new WaitForSeconds(5.2f);
            Assert.That(Get(session, "BombPlanted"), Is.True);
            Assert.That(Get(session, "IsPlanting"), Is.False);
            Assert.That(Get(player, "CanAct"), Is.True);
            Assert.That((float)Get(session, "BombRemaining"), Is.InRange(116f, 120f));
            Assert.That(Vector3.Distance(charge.position, bomb.transform.position + Vector3.up * .48f), Is.LessThan(.01f));
            Assert.That(charge.gameObject.activeSelf, Is.True);
            yield return Capture("armed-bomb");
            yield return new WaitForSeconds(1f);
            Assert.That((bool)Call(Get(session, "Graph"), "IsGateOpen", 3), Is.False, "Planting in Room 1 must not bypass the remaining sectors.");
            Call(session, "TryExit"); Assert.That(Get(session, "Won"), Is.False);
            ClearObjectives();
            yield return new WaitForSeconds(2f);
            Assert.That((bool)Call(Get(session, "Graph"), "IsGateOpen", 3), Is.True);
        }

        [UnityTest]
        public IEnumerator InterruptedPlanting_RestoresControlsAndDoesNotArm()
        {
            ClearObjectives(); FaceStation(); Call(bomb, "Interact");
            yield return new WaitForSeconds(.3f);
            bomb.gameObject.SetActive(false);
            Assert.That(Get(session, "IsPlanting"), Is.False);
            Assert.That(Get(session, "BombPlanted"), Is.False);
            Assert.That(Get(player, "CanAct"), Is.True);
            bomb.gameObject.SetActive(true);
            Call(bomb, "Interact");
            yield return new WaitForSeconds(5.2f);
            Assert.That(Get(session, "BombPlanted"), Is.True);
        }

        [UnityTest]
        public IEnumerator DeathDuringPlanting_CancelsWithoutStartingTimer()
        {
            ClearObjectives(); Call(bomb, "Interact");
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            yield return null; yield return null;
            Assert.That(Get(session, "Ended"), Is.True);
            Assert.That(Get(session, "BombPlanted"), Is.False);
            Assert.That(Get(session, "IsPlanting"), Is.False);
            Call(session, "TryExit");
            Assert.That(Get(session, "Won"), Is.False);
        }

        [UnityTest]
        public IEnumerator ArmedBomb_ExpiresBeforeExtraction_AndCannotWin()
        {
            ClearObjectives(); Call(bomb, "Interact");
            yield return new WaitForSeconds(5.2f);
            Field(session, "detonationAt", Time.time - 1f);
            Call(session, "TryExit");
            yield return null;
            Assert.That(Get(session, "Ended"), Is.True);
            Assert.That(Get(session, "Won"), Is.False);
            StringAssert.Contains("BOMB DETONATED", (string)Get(session, "Outcome"));
        }

        [UnityTest]
        public IEnumerator Intro_DecodesAndCompletesWhileGameplayIsPaused()
        {
            var phase = session.GetType().GetProperty("Phase");
            phase.SetValue(session, Enum.Parse(phase.PropertyType, "NameEntry"));
            Call(session, "Begin", "VIDEO TEST");
            var intro = (Component)Get(session, "Intro");
            float deadline = Time.realtimeSinceStartup + 12f;
            bool decoded = false;
            while ((bool)Get(intro, "IsPlaying") && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Get(session, "CanPlay"), Is.False);
                Assert.That(Get(session, "MissionTimerActive"), Is.False);
                var video = intro.GetComponent<VideoPlayer>();
                if (!decoded && video != null && video.texture != null && video.time > .5)
                {
                    decoded = true;
                    Assert.That(video.audioTrackCount, Is.GreaterThan(0));
                    yield return Capture("intro-video");
                }
                yield return null;
            }
            Assert.That(decoded, Is.True, "The supplied MP4 must decode actual frames.");
            Assert.That(Get(intro, "IsPlaying"), Is.False);
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Countdown"));
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(Get(session, "CanPlay"), Is.True);
            Assert.That(Get(session, "MissionTimerActive"), Is.False);
        }

        [UnityTest]
        public IEnumerator Ending_DecodesSuppliedMovie_AndReturnsToWinScreen()
        {
            ClearObjectives(); Call(bomb, "Interact");
            yield return new WaitForSeconds(5.2f);
            Call(session, "TryExit");
            var ending = (Component)Get(session, "Ending");
            Assert.That(ending, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 55f;
            bool decoded = false;
            while ((bool)Get(ending, "IsPlaying") && Time.realtimeSinceStartup < deadline)
            {
                var video = ending.GetComponent<VideoPlayer>();
                if (!decoded && video != null && video.texture != null && video.time > .5)
                {
                    decoded = true;
                    Assert.That(video.url, Does.EndWith("ShiftFour_Final_Ending.mp4"));
                    Assert.That(video.audioTrackCount, Is.GreaterThan(0));
                    yield return Capture("ending-video");
                }
                yield return null;
            }
            Assert.That(decoded, Is.True);
            Assert.That(Get(ending, "IsPlaying"), Is.False);
            Assert.That(Get(session, "Won"), Is.True);
            Assert.That(((Camera)Get(player, "ViewCamera")).enabled, Is.True);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After bomb planting QA"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
