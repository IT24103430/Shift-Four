#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
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
    public sealed class PistolGripPlayModeTests
    {
        private Component player, weapons, model, presenter, arms, grip, bindings;
        private SkinnedMeshRenderer skin;
        private Camera camera;
        private Transform pivot, right, left, rightTarget, leftTarget;
        private float timeScale;
        private CursorLockMode cursor;
        private bool visible;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static string Evidence
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "PistolGrip");
                Directory.CreateDirectory(path); return path;
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            timeScale = Time.timeScale; cursor = Cursor.lockState; visible = Cursor.visible; Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            weapons = player.GetComponent(T("WeaponController")); camera = (Camera)Get(player, "ViewCamera");
            model = (Component)Get(player.GetComponent(T("FirstPersonWeaponMount")), "Instance");
            presenter = model.GetComponent(T("PistolPresentation")); arms = model.GetComponent(T("FirstPersonArms"));
            bindings = model.GetComponentInChildren(T("FirstPersonArmsRig")); grip = bindings.GetComponent(T("FirstPersonPistolGrip"));
            Assert.That(grip, Is.Not.Null); Assert.That(Get(grip, "IsReady"), Is.True);
            skin = (SkinnedMeshRenderer)Get(bindings, "Skin"); pivot = (Transform)Get(presenter, "RecoilPivot");
            right = (Transform)Get(bindings, "RightHand"); left = (Transform)Get(bindings, "LeftHand");
            rightTarget = (Transform)Get(grip, "RightHandTarget"); leftTarget = (Transform)Get(grip, "SupportHandTarget");
            foreach (Component brain in Object.FindObjectsByType(T("DroneBrain")))
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            yield return PlayerWeaponTestActions.WaitForReady(player); Time.timeScale = 0;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = timeScale;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After grip fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        private void AssertTargets()
        {
            Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
            Assert.That(Vector3.Distance(left.position, leftTarget.position), Is.LessThan(.0002f));
            Assert.That(Quaternion.Angle(right.rotation, rightTarget.rotation), Is.LessThan(.05f));
            Assert.That(Quaternion.Angle(left.rotation, leftTarget.rotation), Is.LessThan(.05f));
        }

        [UnityTest] public IEnumerator Grip_RemainsAlignedThroughPitchYawAndOrdinaryRecoil_WithoutMovingTheAimRay()
        {
            var originalOffsets = skin.bones.Select(b => b.localPosition).ToArray();
            var gun = model.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && (m.name.EndsWith("_Body") || m.name.EndsWith("_Magazine"))).ToArray();
            var gunOffsets = gun.Select(g => g.transform.localPosition).ToArray();
            Vector3 pistolPosition = pivot.localPosition; Quaternion pistolRotation = pivot.localRotation;
            Quaternion playerRotation = player.transform.rotation, viewRotation = camera.transform.localRotation;
            Vector3 viewPosition = camera.transform.localPosition;
            foreach (float yaw in new[] { -135f, 0f, 135f }) foreach (float pitch in new[] { -80f, 0f, 80f })
            {
                player.transform.rotation = Quaternion.Euler(0, yaw, 0);
                camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                Vector3 eye = camera.transform.position, aim = camera.transform.forward;
                Call(arms, "ApplyPose", 0f, null, Vector3.zero);
                AssertTargets();
                Assert.That(camera.transform.position, Is.EqualTo(eye));
                Assert.That(Vector3.Angle(camera.transform.forward, aim), Is.LessThan(.001f));
                Assert.That(pivot.localPosition, Is.EqualTo(pistolPosition));
                Assert.That(Quaternion.Angle(pivot.localRotation, pistolRotation), Is.LessThan(.001f));
                CollectionAssert.AreEqual(originalOffsets, skin.bones.Select(b => b.localPosition).ToArray());
                CollectionAssert.AreEqual(gunOffsets, gun.Select(g => g.transform.localPosition).ToArray());
            }
            player.transform.rotation = playerRotation; camera.transform.localRotation = viewRotation;
            Vector3 origin = camera.transform.position, direction = camera.transform.forward;
            Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
            var shot = Get(model, "LastShot"); var ray = (Ray)shot.GetType().GetField("Ray").GetValue(shot);
            Assert.That(ray.origin, Is.EqualTo(origin)); Assert.That(Vector3.Angle(ray.direction, direction), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(pivot.localPosition, pistolPosition), Is.GreaterThan(.005f));
            AssertTargets();
            Assert.That(camera.transform.localPosition, Is.EqualTo(viewPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, viewRotation), Is.LessThan(.001f));
            yield return null; AssertTargets(); yield return CapturePlayer("game-camera-recoil");
            File.WriteAllText(Path.Combine(Evidence, "aim-and-grip.txt"), "Nine pitch/yaw combinations (-80/0/+80 pitch; -135/0/+135 yaw): both wrist targets retained, bone local positions and pistol local offsets unchanged. Actual accepted shot ray equals camera position/forward; ordinary recoil retains both grips.\n");
        }

        [UnityTest] public IEnumerator RealReloadAndCancel_MoveTheSupportWristWithRotationsAndRestoreThePose()
        {
            var bones = skin.bones; var offsets = bones.Select(b => b.localPosition).ToArray();
            var scales = bones.Select(b => b.localScale).ToArray();
            Vector3 initialWrist = pivot.InverseTransformPoint(left.position);
            var mesh = new Mesh(); skin.BakeMesh(mesh, true); Vector3[] initialSkin = mesh.vertices;
            try
            {
                Time.timeScale = 1;
                Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
                Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
                yield return new WaitForSeconds(.7f);
                Assert.That(Vector3.Distance(pivot.InverseTransformPoint(left.position), initialWrist), Is.GreaterThan(.04f));
                CollectionAssert.AreEqual(offsets, bones.Select(b => b.localPosition).ToArray());
                CollectionAssert.AreEqual(scales, bones.Select(b => b.localScale).ToArray());
                foreach (string side in new[] { "R", "L" })
                {
                    Transform upper = bones.Single(b => b.name == "upper_arm." + side), forearm = bones.Single(b => b.name == "forearm." + side), wrist = bones.Single(b => b.name == "hand." + side);
                    Assert.That(Vector3.Distance(upper.position, forearm.position), Is.EqualTo(.2203115f).Within(.00005f));
                    Assert.That(Vector3.Distance(forearm.position, wrist.position), Is.EqualTo(.227094f).Within(.00005f));
                }
                yield return CapturePlayer("game-camera-reload");
                Assert.That(Call(weapons, "CancelReload"), Is.EqualTo(true));
                yield return null; Time.timeScale = 0; AssertTargets();
                skin.BakeMesh(mesh, true);
                Assert.That(initialSkin.Zip(mesh.vertices, Vector3.Distance).Max(), Is.LessThan(.00005f));
                File.WriteAllText(Path.Combine(Evidence, "reload-lengths.txt"), "Real reload/cancel: support wrist travels >4 cm; all bone local positions/scales unchanged, upper arms 0.2203115 m and forearms 0.227094 m; cancel restores the initial baked surface within 0.05 mm.\n");
            }
            finally { Object.Destroy(mesh); }
        }

        [UnityTest] public IEnumerator ExtremeTargets_ClampReachAndWristRotationWithoutStretchingOrNonfiniteSkin()
        {
            var offsets = skin.bones.Select(b => b.localPosition).ToArray();
            Quaternion neutral = right.localRotation;
            rightTarget.localPosition += Vector3.forward * 2;
            rightTarget.localRotation *= Quaternion.Euler(150, 120, 80);
            Call(arms, "ApplyPose", 0f, null, Vector3.zero);
            CollectionAssert.AreEqual(offsets, skin.bones.Select(b => b.localPosition).ToArray());
            Assert.That(Quaternion.Angle(neutral, right.localRotation), Is.LessThanOrEqualTo((float)Get(grip, "MaximumWristDeviation") + .01f));
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true);
                Assert.That(mesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), Is.True);
                Assert.That(Vector3.Distance(((Transform)Get(bindings, "RightUpperArm")).position, right.position), Is.LessThan(.4475f));
            }
            finally { Object.Destroy(mesh); }
            yield return null;
        }

        [UnityTest] public IEnumerator VisualEvidence_RecordsActualFrontSidesAndGameViewAndBakedGeometry()
        {
            var type = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            var view = type == null ? null : Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => type.IsInstanceOfType(w));
            if (view == null && !Application.isBatchMode && type != null) view = EditorWindow.GetWindow(type);
            if (view != null) { view.Repaint(); if (!Application.isBatchMode) view.Focus(); }
            for (int i = 0; i < 6; i++) yield return null;
            CaptureGameView(view);
            AssertTargets(); yield return CapturePlayer("game-camera-idle"); ExportGeometry();
            var inspection = new GameObject("Grip inspection camera").AddComponent<Camera>();
            inspection.CopyFrom(camera); inspection.orthographic = true; inspection.orthographicSize = .16f;
            inspection.nearClipPlane = .005f; inspection.farClipPlane = 3; inspection.cullingMask = 1 << 2;
            inspection.clearFlags = CameraClearFlags.SolidColor; inspection.backgroundColor = new Color(.18f, .21f, .25f);
            var rt = new RenderTexture(1400, 1100, 24); rt.Create(); inspection.targetTexture = rt;
            inspection.aspect = (float)rt.width / rt.height;
            try
            {
                foreach (var pose in new[] {
                    ("front", new Vector3(0, -.025f, .6f)),
                    ("right-side", new Vector3(.6f, -.025f, -.06f)),
                    ("left-side", new Vector3(-.6f, -.025f, -.06f)),
                    ("rear", new Vector3(0, .03f, -.6f)) })
                {
                    inspection.transform.position = pivot.TransformPoint(pose.Item2);
                    inspection.transform.LookAt(pivot.TransformPoint(new Vector3(0, -.03f, -.06f)), pivot.up);
                    yield return null; yield return null; Capture(rt, "inspection-" + pose.Item1);
                }
            }
            finally { inspection.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(inspection.gameObject); }
        }

        private IEnumerator CapturePlayer(string name)
        {
            var rt = new RenderTexture(1600, 900, 24); rt.Create();
            var previous = camera.targetTexture; float aspect = camera.aspect;
            try { camera.targetTexture = rt; camera.aspect = 16f / 9f; yield return null; yield return null; Capture(rt, name); }
            finally { camera.targetTexture = previous; camera.aspect = aspect; rt.Release(); Object.Destroy(rt); }
        }

        private static void CaptureGameView(EditorWindow view)
        {
            if (view != null)
                for (Type type = view.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (typeof(RenderTexture).IsAssignableFrom(field.FieldType) && field.GetValue(view) is RenderTexture rt && rt.IsCreated() && rt.width >= 320)
                        {
                            Capture(rt, "actual-game-view", SystemInfo.graphicsUVStartsAtTop);
                            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "Actual Unity Editor " + type.FullName + "." + field.Name + ", " + rt.width + "x" + rt.height + ", top-origin=" + SystemInfo.graphicsUVStartsAtTop + ". Includes HUD.\n");
                            return;
                        }
            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "No Game-view surface present; direct player-camera evidence is separate.\n");
        }

        private static void Capture(RenderTexture rt, string name, bool topOrigin = false)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence, name + ".tga")));
                writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                writer.Write((short)rt.width); writer.Write((short)rt.height); writer.Write((byte)24); writer.Write((byte)(topOrigin ? 0x20 : 0));
                foreach (Color32 p in texture.GetPixels32()) { writer.Write(p.b); writer.Write(p.g); writer.Write(p.r); }
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }

        [Serializable] private sealed class Part { public string name; public Vector3[] vertices; public int[] triangles; }
        [Serializable] private sealed class Bone { public string name; public Vector3 position; public Quaternion rotation; }
        [Serializable] private sealed class Weight { public int[] bones; public float[] weights; }
        [Serializable] private sealed class Geometry { public Vector3[] vertices; public Bone[] bones; public Weight[] weights; public Part[] arms, pistol; }
        private void ExportGeometry()
        {
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                var data = new Geometry {
                    vertices = baked.vertices.Select(v => pivot.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray(),
                    bones = skin.bones.Select(b => new Bone { name = b.name, position = pivot.InverseTransformPoint(b.position), rotation = Quaternion.Inverse(pivot.rotation) * b.rotation }).ToArray(),
                    weights = skin.sharedMesh.boneWeights.Select(w => new Weight { bones = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }, weights = new[] { w.weight0, w.weight1, w.weight2, w.weight3 } }).ToArray(),
                    arms = Enumerable.Range(0, skin.sharedMesh.subMeshCount).Select(i => new Part { name = skin.sharedMaterials[i].name, triangles = skin.sharedMesh.GetTriangles(i) }).ToArray(),
                    pistol = model.GetComponentsInChildren<MeshFilter>().Where(m => m.sharedMesh != null && m.gameObject.activeInHierarchy && (m.name.EndsWith("_Body") || m.name.EndsWith("_Magazine"))).Select(ReadPistol).ToArray()
                };
                File.WriteAllText(Path.Combine(Evidence, "actual-baked-grip.json"), JsonUtility.ToJson(data));
            }
            finally { Object.Destroy(baked); }
        }

        private Part ReadPistol(MeshFilter filter)
        {
            // Editor-only readback supports vendor meshes without changing their Read/Write importer flag.
            using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
            var mesh = data[0];
            using var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
            mesh.GetVertices(vertices);
            var triangles = new System.Collections.Generic.List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                using var indices = new NativeArray<int>(mesh.GetSubMesh(sub).indexCount, Allocator.Temp);
                mesh.GetIndices(indices, sub); triangles.AddRange(indices.ToArray());
            }
            return new Part { name = filter.name, vertices = vertices.ToArray().Select(v => pivot.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray(), triangles = triangles.ToArray() };
        }
    }
}
#endif
