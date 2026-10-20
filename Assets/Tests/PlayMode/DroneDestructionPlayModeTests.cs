#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class DroneDestructionPlayModeTests
    {
        private Component[] brains, visuals;
        private Component player;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private float captureStep;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static FieldInfo Field(object c, string name)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new MissingFieldException(name);
        }
        private static object Get(object c, string n) => Field(c, n).GetValue(c);
        private static void Set(object c, string n, object v) => Field(c, n).SetValue(c, v);
        private static object Prop(object c, string n) => c.GetType().GetProperty(n).GetValue(c);
        private static object Call(object c, string name, params object[] args)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(c, args);
            }
            throw new MissingMethodException(name);
        }
        private static int Subscriptions(Component source, string eventName, object target)
            => ((Delegate)Get(source, eventName))?.GetInvocationList().Count(d => ReferenceEquals(d.Target, target)) ?? 0;
        private static float Pose(Component visual, string property) => (float)Prop(Get(visual, "pose"), property);
        private static string State(Component visual) => Prop(visual, "State").ToString();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible; captureStep = Time.captureDeltaTime;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = (Component)Object.FindFirstObjectByType(T("PlayerController"));
            ((Behaviour)player).enabled = false;
            brains = Object.FindObjectsByType(T("DroneBrain"), FindObjectsSortMode.None).Cast<Component>().ToArray();
            visuals = brains.Select(b => b.GetComponentInChildren(T("DroneRotorVisuals"))).ToArray();
            Assert.That(brains.Length, Is.EqualTo(4));
            foreach (Component brain in brains)
            {
                ((Behaviour)brain).enabled = false;
                ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                brain.GetComponent<NavMeshAgent>().enabled = false;
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After drone animation tests"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Time.timeScale = 1f; Time.captureDeltaTime = captureStep;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }

        private GameObject RaisedFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Raised wreck test floor";
            floor.transform.position = new Vector3(1008f, 2.75f, 1000f);
            floor.transform.localScale = new Vector3(40f, .5f, 20f);
            for (int i = 0; i < brains.Length; i++) brains[i].transform.position = new Vector3(1000f + i * 5f, 3f, 1000f);
            Physics.SyncTransforms(); return floor;
        }

        [UnityTest] public IEnumerator AllFourDestructionContracts_30FPS() => DestructionAtRate(30);
        [UnityTest] public IEnumerator AllFourDestructionContracts_60FPS() => DestructionAtRate(60);
        [UnityTest] public IEnumerator AllFourDestructionContracts_120FPS() => DestructionAtRate(120);

        private IEnumerator DestructionAtRate(int fps)
        {
            Time.captureDeltaTime = 1f / fps;
            RaisedFloor(); yield return null;
            var roots = brains.Select(b => b.transform.position).ToArray();
            var heights = visuals.Select(v => v.transform.position.y).ToArray();
            foreach (Component visual in visuals)
            {
                Component health = visual.GetComponentInParent(T("Health"));
                Call(health, "ApplyDamage", 10000f, player, "Destruction contract fatal hit");
                Assert.That(State(visual), Is.EqualTo("CriticalFailure"));
                Call(health, "TakeDamage", 10000f);
                Assert.That((int)Prop(visual, "DestructionCount"), Is.EqualTo(1));
            }
            yield return new WaitForSeconds(.3f);
            for (int i = 0; i < brains.Length; i++)
            {
                Assert.That(visuals[i].transform.position.y, Is.LessThan(heights[i] - .02f));
                Assert.That(State(visuals[i]), Is.EqualTo("Falling"));
                Assert.That(((Behaviour)brains[i].GetComponent(T("DroneMotor"))).enabled, Is.False);
                Assert.That(brains[i].GetComponent<Rigidbody>(), Is.Null);
                Assert.That(Pose(visuals[i], "RotorSpeed"), Is.InRange(.01f, .79f));
                var wreck = (Component)Prop(visuals[i], "Wreck");
                foreach (MeshCollider collider in wreck.GetComponents<MeshCollider>()) Assert.That(collider.convex, Is.True);
                foreach(var shape in wreck.GetComponents<Collider>())
                foreach(var actor in brains.Concat(new[]{player}))
                foreach(var other in actor.GetComponentsInChildren<Collider>())
                {
                    if(other.attachedRigidbody==wreck.GetComponent<Rigidbody>() || !other.enabled)continue;
                    Assert.That(Physics.GetIgnoreCollision(shape,other),Is.True,"Falling wreck must not push actors");
                }
            }
            float until = Time.time + 6f;
            while (visuals.Any(v => State(v) != "Disabled") && Time.time < until) yield return null;
            var positions = visuals.Select(v => v.transform.position).ToArray();
            var rotations = visuals.Select(v => v.transform.rotation).ToArray();
            for (int i = 0; i < brains.Length; i++)
            {
                Assert.That(State(visuals[i]), Is.EqualTo("Disabled"), brains[i].name);
                object wreck = Prop(visuals[i], "Wreck");
                Assert.That((int)Prop(wreck, "ImpactCount"), Is.EqualTo(1));
                Assert.That(((Vector3)Prop(wreck, "ImpactPoint")).y, Is.EqualTo(3f).Within(.04f));
                Assert.That(brains[i].transform.position, Is.EqualTo(roots[i]));
                Assert.That(positions[i].y, Is.LessThan(heights[i] - .5f));
                Assert.That(Pose(visuals[i], "RotorSpeed"), Is.Zero);
                Assert.That(((Rigidbody)Prop(wreck, "Body")).isKinematic, Is.True);
                Assert.That(((Component)wreck).GetComponents<Collider>().All(c => !c.enabled), Is.True);
                AssertNoMeshBelow(visuals[i], 3f - .025f);
                ((Behaviour)visuals[i]).enabled = false; ((Behaviour)visuals[i]).enabled = true;
            }
            yield return new WaitForSeconds(.6f);
            for (int i = 0; i < visuals.Length; i++)
            {
                Assert.That(Vector3.Distance(positions[i], visuals[i].transform.position), Is.LessThan(.002f));
                Assert.That(Quaternion.Angle(rotations[i], visuals[i].transform.rotation), Is.LessThan(.01f));
                Assert.That((int)Prop(Prop(visuals[i], "Wreck"), "ImpactCount"), Is.EqualTo(1));
            }
        }

        private static void AssertNoMeshBelow(Component visual, float floor)
        {
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                using var positions = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                data[0].GetVertices(positions);
                float minimum = float.PositiveInfinity;
                foreach (Vector3 vertex in positions) minimum = Mathf.Min(minimum, filter.transform.TransformPoint(vertex).y);
                Assert.That(minimum, Is.GreaterThanOrEqualTo(floor), visual.name + "/" + filter.name);
            }
        }

        [UnityTest]
        public IEnumerator SleepingSupportedWreck_CompletesShutdownWithoutFurtherContactCallbacks()
        {
            RaisedFloor();
            Component visual = visuals[0];
            Call(brains[0].GetComponent(T("Health")), "TakeDamage", 10000f);
            float until = Time.time + 3f;
            while (State(visual) != "GroundImpact" && Time.time < until) yield return null;
            Assert.That(State(visual), Is.EqualTo("GroundImpact"));
            var body = (Rigidbody)Prop(Prop(visual, "Wreck"), "Body");
            body.Sleep();
            yield return new WaitForSeconds(.8f);
            Assert.That(State(visual), Is.EqualTo("Disabled"), "Sleeping bodies stop receiving OnCollisionStay callbacks.");
        }

        [UnityTest]
        public IEnumerator FallingRig_CollidesWithCoverAndWallsBeforeBecomingNonblocking()
        {
            RaisedFloor();
            Component visual = visuals[0], brain = brains[0];
            float radius = (float)Prop(brain.GetComponent(T("DroneBody")), "HorizontalRadius");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = brain.transform.position + new Vector3(radius + .2f, 2f, 0f);
            wall.transform.localScale = new Vector3(.2f, 4f, 8f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = brain.transform.position + Vector3.up * .35f;
            cover.transform.localScale = new Vector3(3.5f, .7f, 3.5f);
            Physics.SyncTransforms();
            Call(brain.GetComponent(T("Health")), "TakeDamage", 10000f);
            float until = Time.time + 6f;
            while (State(visual) != "Disabled" && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                Component wreck = (Component)Prop(visual, "Wreck");
                if (wreck == null) continue;
                foreach (Collider shape in wreck.GetComponents<Collider>())
                {
                    if (!shape.enabled) continue;
                    bool overlap = Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                        wall.GetComponent<Collider>(), wall.transform.position, wall.transform.rotation, out _, out float depth);
                    Assert.That(overlap && depth > .04f, Is.False, "Wreck crossed the wall: " + depth);
                }
            }
            Assert.That(State(visual), Is.EqualTo("Disabled"));
            Assert.That(((Vector3)Prop(Prop(visual, "Wreck"), "ImpactPoint")).y, Is.EqualTo(3.7f).Within(.04f));
            AssertNoMeshBelow(visual, 3.7f - .025f);
        }

        [UnityTest]
        public IEnumerator DeathAndRestart_NotifyMissionOnceAndRemoveEveryWreckAndEffect()
        {
            foreach (Component brain in brains)
            {
                Call(brain.GetComponent(T("Health")), "TakeDamage", 10000f);
                Call(brain.GetComponent(T("Health")), "TakeDamage", 10000f);
            }
            var session = (Component)Object.FindFirstObjectByType(T("GameSession"));
            Assert.That((int)Prop(session, "DronesRemaining"), Is.Zero);
            yield return new WaitForSeconds(3f);
            Assert.That(Object.FindObjectsByType(T("DroneWreckMotion"), FindObjectsSortMode.None).Length, Is.EqualTo(4));
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            Call(session, "Restart");
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType(T("DroneWreckMotion"), FindObjectsSortMode.None).Length, Is.Zero);
            foreach (Component visual in Object.FindObjectsByType(T("DroneRotorVisuals"), FindObjectsSortMode.None))
            {
                Assert.That((bool)Prop(visual.GetComponentInParent(T("Health")), "IsAlive"), Is.True);
                Assert.That((int)Prop(visual, "DestructionCount"), Is.Zero);
                Assert.That((int)Prop(Prop(visual, "Effects"), "ShotCount"), Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator RenderedMainLab_AllFourAcceptedCombatAndDestructionSequences()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires the separately executed rendered Unity run.");
            string folder = System.Environment.GetEnvironmentVariable("SHIFTFOUR_DEMO_OUTPUT") ?? "/tmp/ShiftFour-AdvancedEvidence/frames";
            System.IO.Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("Temporary animation evidence camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55f; camera.nearClipPlane = .05f;
            var render = new RenderTexture(960, 540, 24); render.Create(); camera.targetTexture = render;
            try
            {
                foreach (Component brain in brains.OrderBy(b => b.name))
                {
                    Component visual = brain.GetComponentInChildren(T("DroneRotorVisuals"));
                    Component health = brain.GetComponent(T("Health"));
                    Call(player.GetComponent(T("Health")), "Configure", 100f, true);
                    bool found = false;
                    foreach (float distance in new[] { 5.5f, 4.5f, 3.3f })
                    {
                        for (int yaw = 0; yaw < 360; yaw += 30)
                        {
                            Vector3 point = brain.transform.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance;
                            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, .3f, NavMesh.AllAreas)) continue;
                            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
                            player.transform.position = hit.position; cc.enabled = true; Call(player, "ApplyCameraPose");
                            brain.transform.rotation = Quaternion.LookRotation((point - brain.transform.position).normalized) * Quaternion.Euler(0f, 35f, 0f);
                            Physics.SyncTransforms();
                            if (!(bool)Call(brain, "CanSeePlayer")) continue;
                            Component body = brain.GetComponent(T("DroneBody"));
                            Vector3 ring = point - (point - brain.transform.position).normalized * Mathf.Max(2.8f, (float)Prop(body, "HorizontalRadius") + .8f);
                            if (!(bool)Call(body, "CanOccupy", ring)) continue;
                            var session = (Component)Object.FindFirstObjectByType(T("GameSession"));
                            if (((IList)Call(Prop(session, "Graph"), "FindRoute", brain.transform.position, ring)).Count == 0) continue;
                            found = true; break;
                        }
                        if (found) break;
                    }
                    Assert.That(found, Is.True, brain.name + " needs a clear in-room firing position");
                    Vector3 forward = (player.transform.position - brain.transform.position).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, forward);
                    camera.transform.position = brain.transform.position + forward * 3f + side * 1.8f + Vector3.up * 2.7f;
                    camera.transform.LookAt(brain.transform.position + Vector3.up * 1.1f);
                    yield return new WaitForSeconds(.5f);
                    yield return Capture(camera, render, folder, brain.name + "-01-hover", visual);
                    Vector3 started = brain.transform.position;
                    Quaternion startedHeading = brain.transform.rotation;
                    var agent = brain.GetComponent<NavMeshAgent>(); agent.enabled = true;
                    ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = true;
                    Set(brain, "nextDecision", 0f); Set(brain, "nextShot", Time.time + 1.4f);
                    ((Behaviour)brain).enabled = true;
                    yield return new WaitForSeconds(1f);
                    Assert.That(Vector3.Distance(started, brain.transform.position), Is.GreaterThan(.02f), brain.name + " must actually navigate: " + Prop(brain, "CurrentDecision") + ", motor=" + Get(brain.GetComponent(T("DroneMotor")), "state"));
                    Assert.That(Quaternion.Angle(startedHeading, brain.transform.rotation), Is.GreaterThan(2f), brain.name + " must actually turn using its motor");
                    yield return Capture(camera, render, folder, brain.name + "-02-approach", visual);
                    Assert.That(PlacePlayerInSight(brain), Is.True, brain.name + " needs renewed sight after its tactical movement");
                    float until = Time.time + 4f;
                    while (!(bool)Prop(Prop(visual, "Effects"), "FlashVisible") && Time.time < until) yield return null;
                    Assert.That((bool)Prop(Prop(visual, "Effects"), "FlashVisible"), Is.True, brain.name + " accepted shot");
                    Assert.That((bool)Prop(Prop(visual, "Effects"), "TracerVisible"), Is.True);
                    Assert.That((Vector3)Prop(Prop(visual, "Effects"), "TracerTarget"), Is.EqualTo((Vector3)Prop(brain, "LastShotHit")));
                    yield return new WaitForSeconds(.035f); // Capture the tracer in flight, after its initial short segment.
                    yield return Capture(camera, render, folder, brain.name + "-03-fire", visual);
                    float before = (float)Prop(health, "Current");
                    PlayerShot(brain);
                    Assert.That((float)Prop(health, "Current"), Is.LessThan(before));
                    yield return Capture(camera, render, folder, brain.name + "-04-hit", visual);
                    int attempts = 0;
                    while ((bool)Prop(health, "IsAlive") && attempts++ < 10)
                    { yield return new WaitForSeconds(.3f); PlayerShot(brain); }
                    Assert.That((bool)Prop(health, "IsAlive"), Is.False, "Real player shots must destroy the drone");
                    yield return Capture(camera, render, folder, brain.name + "-05-critical", visual);
                    yield return new WaitForSeconds(.2f);
                    yield return Capture(camera, render, folder, brain.name + "-06-fall", visual);
                    until = Time.time + 6f;
                    while (State(visual) != "GroundImpact" && State(visual) != "Disabled" && Time.time < until) yield return null;
                    Assert.That((int)Prop(Prop(visual, "Wreck"), "ImpactCount"), Is.EqualTo(1));
                    yield return new WaitForSeconds(.06f); // Let the impact streaks separate from their contact origin.
                    yield return Capture(camera, render, folder, brain.name + "-07-impact", visual);
                    while (State(visual) != "Disabled" && Time.time < until) yield return null;
                    Assert.That(State(visual), Is.EqualTo("Disabled"));
                    yield return Capture(camera, render, folder, brain.name + "-08-wreck", visual);
                }
            }
            finally { camera.targetTexture = null; render.Release(); Object.Destroy(render); Object.Destroy(cameraObject); }
        }

        private void PlayerShot(Component brain)
        {
            Vector3 eye = (Vector3)Prop(player, "EyePosition");
            Vector3 target = brain.transform.position + Vector3.up * 1.8f;
            Physics.SyncTransforms();
            Assert.That(PlayerWeaponTestActions.Fire(player, new Ray(eye, (target - eye).normalized)), Is.True);
        }

        private bool PlacePlayerInSight(Component brain)
        {
            foreach (float distance in new[] { 3.3f, 4.5f, 5.5f })
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f })
            {
                Vector3 point = brain.transform.position + Quaternion.Euler(0f, angle, 0f) * brain.transform.forward * distance;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, .3f, NavMesh.AllAreas)) continue;
                var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
                player.transform.position = hit.position; cc.enabled = true; Call(player, "ApplyCameraPose");
                Physics.SyncTransforms();
                if ((bool)Call(brain, "CanSeePlayer")) return true;
            }
            return false;
        }

        private static IEnumerator Capture(Camera camera, RenderTexture render, string folder, string stage, Component visual)
        {
            Transform root = visual.GetComponentInParent(T("DroneBrain")).transform;
            // Keep the view tied to the stationary navigation root through death so the fall is visible.
            Vector3 center = root.position + Vector3.up * .9f;
            foreach (float angle in new[] { 35f, -35f, 90f, -90f, 150f, -150f, 180f })
            {
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * root.forward * 4f + Vector3.up * 1.5f;
                bool blocked = Physics.RaycastAll(center, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .Any(h => !h.transform.IsChildOf(root) && h.collider.GetComponentInParent(T("PlayerController")) == null);
                if (blocked) continue;
                camera.transform.position = center + offset; break;
            }
            camera.transform.LookAt(center);
            yield return null; // A real render frame, not Camera.Render or synthesized state imagery.
            RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
            var pixels = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); pixels.Apply();
                // ImageConversion is optional in this project. Write lossless TGA without adding a package.
                using var writer = new System.IO.BinaryWriter(System.IO.File.Create(System.IO.Path.Combine(folder, stage + ".tga")));
                writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                writer.Write((short)render.width); writer.Write((short)render.height);
                writer.Write((byte)24); writer.Write((byte)0);
                foreach (Color32 pixel in pixels.GetPixels32()) { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
            }
            finally { RenderTexture.active = previous; Object.Destroy(pixels); }
        }
    }
}
#endif
