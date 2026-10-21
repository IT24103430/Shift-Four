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
    public sealed class DroneAnimationPlayModeTests
    {
        private Component[] brains, visuals;
        private Component player;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private int shots;
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
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
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
            Time.timeScale = 1f;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }

        [UnityTest]
        public IEnumerator AllFourBindings_RecoverMissingReferencesWithoutChangingRestPose()
        {
            foreach (Component brain in brains)
            {
                Component motor = brain.GetComponent(T("DroneMotor"));
                Component visual = brain.GetComponentInChildren(T("DroneRotorVisuals"));
                Assert.That(Get(motor, "visual"), Is.EqualTo(visual.transform));
                Assert.That((Vector3)Get(visual, "restPosition"), Is.EqualTo((Vector3)Get(motor, "visualRestPosition")));
                // Simulate an old scene override on an inactive clone, using Unity's API only.
                var parent = new GameObject("Missing binding fixture"); parent.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/" + brain.name + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                var clone = Object.Instantiate(prefab, parent.transform);
                var cloneMotor = clone.GetComponent(T("DroneMotor"));
                var cloneVisual = clone.GetComponentInChildren(T("DroneRotorVisuals"), true);
                Vector3 rest = cloneVisual.transform.localPosition;
                Set(cloneMotor, "visual", null);
                clone.GetComponent<NavMeshAgent>().enabled = false;
                ((Behaviour)clone.GetComponent(T("DroneBrain"))).enabled = false;
                parent.SetActive(true);
                Assert.That(Get(cloneMotor, "visual"), Is.EqualTo(cloneVisual.transform));
                Assert.That((Vector3)Get(cloneMotor, "visualRestPosition"), Is.EqualTo(rest));
                var rotorMeshes = cloneVisual.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("Rotor")).ToArray();
                Assert.That(rotorMeshes.Length, Is.EqualTo(4));
                Assert.That(rotorMeshes.Select(m => m.transform.parent).Distinct().Count(), Is.EqualTo(4));
                Object.Destroy(parent);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedEnableDisable_UnsubscribesAndRestoresWithoutDuplicatePivots()
        {
            foreach (Component visual in visuals)
            {
                Component health = visual.GetComponentInParent(T("Health"));
                Component brain = visual.GetComponentInParent(T("DroneBrain"));
                int transforms = visual.GetComponentsInChildren<Transform>(true).Length;
                Vector3 scale = visual.transform.localScale;
                Call(visual, "GroundImpact", visual.transform.position, Vector3.up);
                yield return null; // Include active impact streaks in disable/re-enable cleanup.
                for (int cycle = 0; cycle < 4; cycle++)
                {
                    foreach (string e in new[] { "Damaged", "Died" }) Assert.That(Subscriptions(health, e, visual), Is.EqualTo(1));
                    Assert.That(Subscriptions(brain, "ShotFired", visual), Is.EqualTo(1));
                    ((Behaviour)visual).enabled = false;
                    Assert.That(brain.GetComponentsInChildren<LineRenderer>().All(line => !line.enabled), Is.True);
                    foreach (string e in new[] { "Damaged", "Died" }) Assert.That(Subscriptions(health, e, visual), Is.Zero);
                    Assert.That(Subscriptions(brain, "ShotFired", visual), Is.Zero);
                    Assert.That(visual.transform.localPosition, Is.EqualTo((Vector3)Get(visual, "restPosition")));
                    Assert.That(visual.transform.localRotation, Is.EqualTo((Quaternion)Get(visual, "restRotation")));
                    Assert.That(visual.transform.localScale, Is.EqualTo(scale));
                    foreach (object slot in (IList)Get(visual, "tints"))
                    {
                        Type type = slot.GetType();
                        var renderer = (Renderer)type.GetField("Renderer").GetValue(slot);
                        var original = (MaterialPropertyBlock)type.GetField("Original").GetValue(slot);
                        var restored = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(restored, (int)type.GetField("Index").GetValue(slot));
                        Assert.That(restored.isEmpty, Is.EqualTo(original.isEmpty));
                    }
                    ((Behaviour)visual).enabled = true;
                    brain.gameObject.SetActive(false); brain.gameObject.SetActive(true);
                }
                Assert.That(visual.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(transforms));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllFourMotionStates_FollowAcceptedDisplacementAndPreserveRoot()
        {
            var starts = brains.Select(b => b.transform.position).ToArray();
            float until = Time.time + .3f;
            while (Time.time < until)
            {
                foreach (Component brain in brains) brain.transform.position += brain.transform.forward * (2f * Time.deltaTime);
                yield return null;
            }
            foreach (Component visual in visuals)
            {
                Assert.That(State(visual), Is.EqualTo("Moving"));
                Assert.That(Pose(visual, "Pitch"), Is.LessThan(-.05f));
            }
            until = Time.time + .3f;
            while (Time.time < until)
            {
                foreach (Component brain in brains)
                {
                    brain.transform.Rotate(0f, 60f * Time.deltaTime, 0f);
                    brain.transform.position += brain.transform.forward * (2f * Time.deltaTime);
                }
                yield return null;
            }
            foreach (Component visual in visuals)
            {
                Assert.That(State(visual), Is.EqualTo("Turning"));
                Assert.That(Pose(visual, "Bank"), Is.LessThan(-.05f));
            }
            yield return null; yield return null;
            foreach (Component visual in visuals) Assert.That(State(visual), Is.EqualTo("Braking"));
            var held = brains.Select(b => b.transform.position).ToArray();
            var headings = brains.Select(b => b.transform.rotation).ToArray();
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < visuals.Length; i++)
            {
                Assert.That(State(visuals[i]), Is.EqualTo("Idle"));
                Assert.That(brains[i].transform.position, Is.EqualTo(held[i]));
                Assert.That(brains[i].transform.rotation, Is.EqualTo(headings[i]));
                Assert.That(held[i].y, Is.EqualTo(starts[i].y));
                Assert.That(Mathf.Abs(visuals[i].transform.localPosition.y - ((Vector3)Get(visuals[i], "restPosition")).y), Is.LessThanOrEqualTo(.03501f));
            }
        }

        private void CountShot(object source) => shots++;

        [UnityTest]
        public IEnumerator MisplacedPresentationOnHealthRoot_DisablesWithoutMovingRoot()
        {
            var root = new GameObject("Invalid visual owner");
            root.transform.position = new Vector3(1000f, 4f, 1000f);
            root.transform.rotation = Quaternion.Euler(5f, 33f, 10f);
            root.AddComponent(T("Health"));
            Vector3 position = root.transform.position;
            Quaternion rotation = root.transform.rotation;
            var visual = (Behaviour)root.AddComponent(T("DroneRotorVisuals"));
            yield return null;
            Assert.That(visual.enabled, Is.False);
            Assert.That(root.transform.position, Is.EqualTo(position));
            Assert.That(root.transform.rotation, Is.EqualTo(rotation));
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator FireEvent_RequiresLivingAttackerRangeSightAndCooldown()
        {
            var arena = new Vector3(1000f, 0f, 1000f);
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = arena; cc.enabled = true; Call(player, "ApplyCameraPose");
            Component playerHealth = player.GetComponent(T("Health")); Call(playerHealth, "Configure", 100f, false);
            for (int i = 0; i < brains.Length; i++) brains[i].transform.position = arena + Vector3.right * (50f + i * 20f);
            Component brain = brains[0], visual = visuals[0];
            brain.transform.position = arena + Vector3.forward * 5f;
            brain.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Set(brain, "nextShot", 0f);
            EventInfo fired = T("DroneBrain").GetEvent("ShotFired");
            Delegate listener = Delegate.CreateDelegate(fired.EventHandlerType, this, GetType().GetMethod(nameof(CountShot), BindingFlags.NonPublic | BindingFlags.Instance));
            fired.AddEventHandler(brain, listener);
            Physics.SyncTransforms(); shots = 0;
            int effectObjects = brain.GetComponentsInChildren<Transform>(true).Length;
            Call(brain, "TryShoot");
            Assert.That(shots, Is.EqualTo(1)); Assert.That(Pose(visual, "FireFlash"), Is.EqualTo(1f));
            object effects = Prop(visual, "Effects");
            Assert.That((bool)Prop(effects, "FlashVisible"), Is.True);
            Assert.That((bool)Prop(effects, "TracerVisible"), Is.True);
            Assert.That((Vector3)Prop(effects, "TracerTarget"), Is.EqualTo((Vector3)Prop(brain, "LastShotHit")));
            Assert.That(brain.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(effectObjects), "Shots reuse their effects.");
            Assert.That((float)Prop(playerHealth, "Current"), Is.EqualTo(90f));
            Call(brain, "TryShoot"); Assert.That(shots, Is.EqualTo(1));
            Set(brain, "nextShot", 0f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = arena + new Vector3(0f, 1.5f, 2.5f); cover.transform.localScale = new Vector3(3f, 3f, .5f);
            Physics.SyncTransforms(); Call(brain, "TryShoot"); Assert.That(shots, Is.EqualTo(1));
            Object.Destroy(cover); yield return null;
            brain.transform.position = arena + Vector3.forward * 30f;
            Physics.SyncTransforms(); Call(brain, "TryShoot"); Assert.That(shots, Is.EqualTo(1));
            brain.transform.position = arena + Vector3.forward * 5f;
            Call(brain.GetComponent(T("Health")), "TakeDamage", 10000f);
            Physics.SyncTransforms(); Call(brain, "TryShoot"); Assert.That(shots, Is.EqualTo(1));
            Assert.That(State(visual), Is.EqualTo("CriticalFailure"));
            Assert.That((int)Prop(effects, "ShotCount"), Is.EqualTo(1));
            Assert.That((bool)Prop(effects, "FlashVisible"), Is.False);
            Assert.That((bool)Prop(effects, "TracerVisible"), Is.False);
            fired.RemoveEventHandler(brain, listener);
        }

        [UnityTest]
        public IEnumerator AllFourDamageAndDeath_TintWithoutCloningMaterialsAndStopRotors()
        {
            var materials = visuals.Select(v => v.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).ToArray()).ToArray();
            var rotorStarts = visuals.Select(v => ((List<Transform>)Get(v, "pivots"))[0].localRotation).ToArray();
            foreach (Component visual in visuals)
            {
                Component health = visual.GetComponentInParent(T("Health"));
                Call(health, "TakeDamage", 0f); Call(health, "TakeDamage", -10f);
                Assert.That(Pose(visual, "HitFlash"), Is.Zero, "Rejected damage must not produce feedback.");
                Call(health, "TakeDamage", 5f);
                Assert.That(Pose(visual, "HitFlash"), Is.EqualTo(1f));
            }
            yield return null; yield return null;
            for (int i = 0; i < visuals.Length; i++)
            {
                Assert.That(State(visuals[i]), Is.EqualTo("Damaged"));
                Assert.That(Pose(visuals[i], "HitFlash"), Is.GreaterThan(0f));
                Assert.That(Quaternion.Angle(rotorStarts[i], ((List<Transform>)Get(visuals[i], "pivots"))[0].localRotation), Is.GreaterThan(.001f));
                var slots = (IList)Get(visuals[i], "tints");
                Assert.That(slots.Count, Is.GreaterThan(0));
                object slot = slots[0]; var slotType = slot.GetType();
                var renderer = (Renderer)slotType.GetField("Renderer").GetValue(slot);
                int property = (int)slotType.GetField("Property").GetValue(slot);
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, (int)slotType.GetField("Index").GetValue(slot));
                Assert.That(block.GetColor(property), Is.Not.EqualTo((Color)slotType.GetField("Color").GetValue(slot)));
                Call(visuals[i].GetComponentInParent(T("Health")), "TakeDamage", 10000f);
                Assert.That(State(visuals[i]), Is.EqualTo("CriticalFailure"));
            }
            yield return new WaitForSeconds(2.5f);
            for (int i = 0; i < visuals.Length; i++)
            {
                Assert.That(Pose(visuals[i], "RotorSpeed"), Is.Zero);
                Assert.That(Pose(visuals[i], "ShutdownBlend"), Is.GreaterThan(.999f));
                CollectionAssert.AreEqual(materials[i], visuals[i].GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).ToArray());
                ((Behaviour)visuals[i]).enabled = false; ((Behaviour)visuals[i]).enabled = true;
                Assert.That(State(visuals[i]), Is.EqualTo("Disabled"));
                Assert.That(Pose(visuals[i], "RotorSpeed"), Is.Zero, "Re-enabling a dead visual must not restart its rotors.");
            }
            var stopped = visuals.Select(v => ((List<Transform>)Get(v, "pivots"))[0].localRotation).ToArray();
            yield return new WaitForSeconds(.2f);
            for (int i = 0; i < visuals.Length; i++)
                Assert.That(Quaternion.Angle(stopped[i], ((List<Transform>)Get(visuals[i], "pivots"))[0].localRotation), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator AllFourMeshes_MaximumTiltHoverAndRotorSweepStayInsideExistingBody()
        {
            var vertices = new Dictionary<Mesh, Vector3[]>();
            float margin = float.PositiveInfinity;
            foreach (Component visual in visuals)
            {
                Component body = visual.GetComponentInParent(T("DroneBody"));
                var root = body.transform;
                var envelope = new Bounds(root.position + (Vector3)Get(body, "envelopeCenter"), (Vector3)Get(body, "envelopeExtents") * 2f);
                Vector3 rest = (Vector3)Get(visual, "restPosition");
                Quaternion restRotation = (Quaternion)Get(visual, "restRotation");
                var meshes = visual.GetComponentsInChildren<MeshFilter>();
                foreach (var filter in meshes)
                {
                    if (vertices.ContainsKey(filter.sharedMesh)) continue;
                    using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                    using var positions = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                    data[0].GetVertices(positions); vertices[filter.sharedMesh] = positions.ToArray();
                }
                var pivots = (List<Transform>)Get(visual, "pivots");
                for (int yaw = 0; yaw < 360; yaw += 15)
                for (int axis = 0; axis < 360; axis += 45)
                foreach (float hover in new[] { -.02f, .02f })
                for (int rotor = 0; rotor < 180; rotor += 45)
                {
                    root.rotation = Quaternion.Euler(0f, yaw, 0f);
                    visual.transform.localPosition = rest + Vector3.up * hover;
                    visual.transform.localRotation = Quaternion.AngleAxis(4f, Quaternion.Euler(0f, axis, 0f) * Vector3.right) * restRotation;
                    for (int p = 0; p < pivots.Count; p++) pivots[p].localRotation = Quaternion.Euler(0f, p % 2 == 0 ? rotor : -rotor, 0f);
                    foreach (var mesh in meshes)
                    {
                        Matrix4x4 matrix = mesh.transform.localToWorldMatrix;
                        foreach (Vector3 vertex in vertices[mesh.sharedMesh])
                        {
                            Vector3 offset = matrix.MultiplyPoint3x4(vertex) - envelope.center;
                            float clearance = Mathf.Min(envelope.extents.x - Mathf.Abs(offset.x), envelope.extents.y - Mathf.Abs(offset.y), envelope.extents.z - Mathf.Abs(offset.z));
                            if (clearance < -.00001f) Assert.Fail(root.name + "/" + mesh.name + " escapes envelope by " + -clearance + " yaw=" + yaw + " tilt axis=" + axis + " rotor=" + rotor);
                            margin = Mathf.Min(margin, clearance);
                        }
                    }
                }
            }
            Debug.Log("[Animation clearance] Minimum sampled mesh-to-envelope margin: " + margin.ToString("F6") + " m");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PresentationTick_AfterBindingHasNoManagedAllocations()
        {
            var ticks = visuals.Select(v => (Action)Delegate.CreateDelegate(typeof(Action), v, v.GetType().GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance))).ToArray();
            foreach (Action tick in ticks) tick();
            foreach (Component visual in visuals) Call(visual.GetComponentInParent(T("Health")), "TakeDamage", 5f);
            long calibration=MeasuredAllocation.Calibrate();
            var timer=new System.Diagnostics.Stopwatch();
            Action workload=()=>{for(int i=0;i<1000;i++)foreach(Action tick in ticks)tick();};
            timer.Start();long allocated=MeasuredAllocation.Events(workload,out var samples);timer.Stop();
            Debug.Log($"[Animation tick] 4000 ticks: {timer.Elapsed.TotalMilliseconds:F3} ms; allocation events={allocated}; samples={samples}; calibration events={calibration}");
            Assert.That(allocated, Is.Zero);
            yield return null;
        }
    }
}
#endif
