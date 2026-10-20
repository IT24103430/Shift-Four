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
    public sealed class MainLabProximityPlayModeTests
    {
        private Component player, session;
        private Component[] brains;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private int previousFrameRate;
        private float previousCaptureDelta;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Call(Component value, string method, params object[] args)
        {
            var type = value.GetType();
            while (type != null)
            {
                var info = type.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (info != null) return info.Invoke(value, args);
                type = type.BaseType;
            }
            throw new MissingMethodException(method);
        }
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static void Set(Component value, string field, object data)
        {
            var type = value.GetType();
            while (type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance) == null) type = type.BaseType;
            type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, data);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            previousFrameRate = Application.targetFrameRate; previousCaptureDelta = Time.captureDeltaTime;
            Application.targetFrameRate = 60; Time.captureDeltaTime = 1f / 60f;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = Find("PlayerController").Single();
            session = Find("GameSession").Single();
            brains = Find("DroneBrain");
            ((Behaviour)player).enabled = false;
            foreach (var brain in brains) ((Behaviour)brain).enabled = false;
            yield return new WaitForSeconds(.2f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After proximity test"));
            if (scene.path == "Assets/Scenes/MainLab.unity") yield return SceneManager.UnloadSceneAsync(scene);
            Application.targetFrameRate = previousFrameRate; Time.captureDeltaTime = previousCaptureDelta;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }

        [UnityTest]
        public IEnumerator Camera_RemainsProtectedWhenControllerWalksIntoWallAtSteepPitch()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + Vector3.left * 2f + Vector3.up * 1.5f;
            wall.transform.localScale = new Vector3(.2f, 3f, 5f);
            Physics.SyncTransforms();
            var cc = player.GetComponent<CharacterController>();
            cc.Move(Vector3.left * 5f);
            Call(player, "ApplyCameraPose");
            var camera = player.GetComponentInChildren<Camera>();
            camera.transform.rotation = Quaternion.Euler(80f, -90f, 0f);
            Bounds bounds = wall.GetComponent<BoxCollider>().bounds;
            Assert.That(camera.transform.position.x, Is.GreaterThan(bounds.max.x + .2f));
            foreach (var corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                Assert.That(bounds.Contains(camera.ViewportToWorldPoint(new Vector3(corner.x, corner.y, camera.nearClipPlane))), Is.False);
            Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainLab_CameraStartsAtEyeHeightAndDeathRestartRestoresIt()
        {
            var camera = player.GetComponentInChildren<Camera>();
            Assert.That(camera.transform.position.y, Is.EqualTo(1.3f).Within(.06f));
            Assert.That(camera.nearClipPlane, Is.EqualTo(.05f).Within(.0001f));
            Capture(camera, "mainlab-eye-height.tga");
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            Assert.That((bool)T("GameSession").GetProperty("Ended").GetValue(session), Is.True);
            Call(session, "Restart");
            yield return null;
            yield return null;
            var restarted = Find("PlayerController").Single();
            Assert.That(restarted.GetComponentInChildren<Camera>().transform.position.y, Is.EqualTo(1.3f).Within(.06f));
            Assert.That((float)T("Health").GetProperty("Current").GetValue(restarted.GetComponent(T("Health"))), Is.EqualTo(100f));
            Assert.That((bool)T("GameSession").GetProperty("Ended").GetValue(Find("GameSession").Single()), Is.False);
        }

        [UnityTest]
        public IEnumerator Combat_RequiresClearLivingTargetAndHonorsCooldownAndMeshHits()
        {
            var scout = brains.Single(b => b.GetType().Name == "ScoutBrain");
            var agent = scout.GetComponent<NavMeshAgent>();
            agent.isStopped = true;
            ((Behaviour)scout.GetComponent(T("DroneMotor"))).enabled = false;
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = scout.transform.position + Vector3.back * 3.5f;
            cc.enabled = true;
            scout.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Call(player, "ApplyCameraPose");
            Physics.SyncTransforms();
            var health = player.GetComponent(T("Health"));
            Set(health, "regenerate", false);
            Assert.That((bool)Call(scout, "CanSeePlayer"), Is.True);
            float before = (float)T("Health").GetProperty("Current").GetValue(health);
            Call(scout, "TryShoot");
            Call(scout, "TryShoot");
            Assert.That((float)T("Health").GetProperty("Current").GetValue(health), Is.EqualTo(before - 10f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = scout.transform.position + Vector3.up * 1.6f + Vector3.back * 1.7f;
            wall.transform.localScale = new Vector3(5f, 3f, .2f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.2f);
            Assert.That((bool)Call(scout, "CanSeePlayer"), Is.False);
            Call(scout, "TryShoot");
            Assert.That((float)T("Health").GetProperty("Current").GetValue(health), Is.EqualTo(before - 10f));
            // A ray starting inside cover must also fail, not see through the containing solid.
            wall.transform.position = scout.transform.position + Vector3.up * 1.8f;
            Physics.SyncTransforms();
            Assert.That((bool)Call(scout, "CanSeePlayer"), Is.False);
            Object.Destroy(wall);
            yield return null;
            var camera = player.GetComponentInChildren<Camera>();
            camera.transform.LookAt(scout.transform.position + Vector3.up * 1.7f);
            Physics.SyncTransforms();
            var droneHealth = scout.GetComponent(T("Health"));
            float droneBefore = (float)T("Health").GetProperty("Current").GetValue(droneHealth);
            Assert.That(PlayerWeaponTestActions.Fire(player, new Ray(camera.transform.position, camera.transform.forward)), Is.True);
            Assert.That((float)T("Health").GetProperty("Current").GetValue(droneHealth), Is.EqualTo(droneBefore - 25f));
            Capture(camera, "mainlab-scout-standoff.tga");
            Call(health, "TakeDamage", 10000f);
            Call(scout, "TryShoot");
            Assert.That((float)T("Health").GetProperty("Current").GetValue(health), Is.Zero);
        }

        [UnityTest]
        public IEnumerator MainLab_AllFourSpawnEnvelopesAndPolicyDestinationsRemainUsable()
        {
            var starts = brains.ToDictionary(b => b, b => b.transform.position);
            foreach (var brain in brains)
            {
                var body = brain.GetComponent(T("DroneBody"));
                Assert.That((bool)Call(body, "CanOccupy", brain.transform.position), Is.True, brain.name + " spawn clearance");
                var motor = brain.GetComponent(T("DroneMotor"));
                Vector3 start = brain.transform.position;
                // Each native policy remains callable and its selected route remains a normal graph request.
                var choice = Call(brain, "Decide", true, player.transform.position, Vector3.Distance(start, player.transform.position));
                var destination = (Vector3)choice.GetType().GetField("Destination").GetValue(choice);
                Call(motor, "GoToCombat", destination, player, true);
            }
            float until = Time.time + 8f;
            var previous = brains.ToDictionary(b => b, b => b.transform.position);
            while (Time.time < until)
            {
                yield return null;
                foreach (var brain in brains)
                {
                    Assert.That((bool)Call(brain.GetComponent(T("DroneBody")), "CanOccupy", brain.transform.position), Is.True, brain.name + " clearance during movement");
                    Assert.That(Vector3.Distance(previous[brain], brain.transform.position), Is.LessThanOrEqualTo(brain.GetComponent<NavMeshAgent>().speed * Time.deltaTime + .03f), brain.name + " speed limit");
                    previous[brain] = brain.transform.position;
                }
            }
            foreach (var brain in brains)
            {
                Assert.That(Vector3.Distance(brain.transform.position, starts[brain]), Is.GreaterThan(1f), brain.name + " policy must move");
                Assert.That(((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled, Is.True);
                Assert.That(brain.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
                Assert.That(T("DroneMotor").GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(brain.GetComponent(T("DroneMotor"))).ToString(), Is.EqualTo("Arrived"), brain.name + " must finish its selected route");
            }
        }

        private static void Capture(Camera camera, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/ProximityEvidence";
            Directory.CreateDirectory(directory);
            var target = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                // Uncompressed TGA avoids adding the optional ImageConversion package just for evidence.
                using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, name))))
                {
                    writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                    writer.Write((short)1280); writer.Write((short)720);
                    writer.Write((byte)24); writer.Write((byte)0);
                    foreach (Color32 pixel in image.GetPixels32())
                    { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(image); Object.Destroy(target);
            }
        }
    }
}
#endif
