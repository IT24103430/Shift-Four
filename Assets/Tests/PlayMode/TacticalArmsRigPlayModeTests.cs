#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class TacticalArmsRigPlayModeTests
    {
        private Component model, arms, bindings;
        private SkinnedMeshRenderer skin;
        private Camera camera;
        private float timeScale;
        private CursorLockMode cursor;
        private bool visible;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private Transform Bone(string name) => skin.bones.Single(b => b.name == name);
        private static string Evidence
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("SHIFTFOUR_ARMS_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "TacticalArmsRig");
                Directory.CreateDirectory(path); return path;
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            timeScale = Time.timeScale; cursor = Cursor.lockState; visible = Cursor.visible; Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/FPS/FiveSevenRange.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            camera = (Camera)Get(player, "ViewCamera");
            model = (Component)Get(player.GetComponent(T("FirstPersonWeaponMount")), "Instance");
            arms = model.GetComponent(T("FirstPersonArms"));
            bindings = model.GetComponentInChildren(T("FirstPersonArmsRig"));
            Assert.That(bindings, Is.Not.Null);
            skin = (SkinnedMeshRenderer)Get(bindings, "Skin");
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            yield return PlayerWeaponTestActions.WaitForReady(player); Time.timeScale = 0;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = timeScale;
            Scene old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After arms fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        [UnityTest] public IEnumerator Runtime_UsesOwnedMaterialsAndRemainsOutsideGameplayPhysics()
        {
            Assert.That(Get(arms, "IsReady"), Is.True);
            Assert.That(Get(bindings, "IsValid"), Is.True);
            var authored = Resources.Load<GameObject>("Weapons/FirstPersonArmsRig").GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin.sharedMesh, Is.SameAs(authored.sharedMesh));
            CollectionAssert.AreEqual(authored.sharedMaterials, skin.sharedMaterials, "The runtime loader must preserve the authored surfaces.");
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(model.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(model.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 2), Is.True);
            arms.GetType().GetMethod("Initialize").Invoke(arms, new object[] { Get(model.GetComponent(T("PistolPresentation")), "RecoilPivot") });
            Assert.That(model.GetComponentsInChildren(T("FirstPersonArmsRig"), true).Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator GameCamera_RendersTheArmsAndAllSurfacesWithoutMissingShaders()
        {
            var rt = new RenderTexture(1600, 900, 24); rt.Create(); camera.targetTexture = rt; camera.aspect = 16f / 9f;
            try
            {
                yield return null; yield return null;
                Color32[] withArms = Capture(rt, "range-arms-idle");
                skin.enabled = false; yield return null; yield return null;
                Color32[] without = Capture(rt, "range-without-arms-control");
                int pixels = withArms.Zip(without, (a, b) => Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)).Count(d => d > 30);
                int pink = withArms.Count(c => c.r > 220 && c.b > 220 && c.g < 60);
                Assert.That(pixels, Is.GreaterThan(5000), "The real player camera must visibly render the owned arms.");
                Assert.That(pink, Is.EqualTo(0));
                skin.enabled = true;
                File.WriteAllText(Path.Combine(Evidence, "render-summary.json"), "{\"unity\":\"" + Application.unityVersion + "\",\"changedArmPixels\":" + pixels + ",\"missingShaderPinkPixels\":" + pink + ",\"capture\":\"Real game camera render target; HUD excluded\"}");
            }
            finally { skin.enabled = true; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }

        [UnityTest] public IEnumerator Articulation_WristFingerAndElbowMoveWithoutExplodingAndRestoreExactly()
        {
            // Pause automatic grip solving while inspecting individual deform joints.
            var grip = bindings.GetComponent(T("FirstPersonPistolGrip")) as Behaviour;
            if (grip != null) grip.enabled = false;
            var inspection = new GameObject("Arms deformation inspection camera").AddComponent<Camera>();
            inspection.CopyFrom(camera); inspection.orthographic = true; inspection.orthographicSize = .24f; inspection.nearClipPlane = .01f;
            var pivot = (Transform)Get(model.GetComponent(T("PistolPresentation")), "RecoilPivot");
            inspection.transform.position = pivot.position + camera.transform.right * .55f;
            inspection.transform.LookAt(pivot.position - camera.transform.up * .04f);
            var rt = new RenderTexture(1280, 960, 24); rt.Create(); inspection.targetTexture = rt;
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true); Vector3[] rest = baked.vertices;
                yield return null; yield return null; Capture(rt, "inspection-neutral");
                foreach (var pose in new[] {
                    ("hand.L", new Vector3(25, 0, 0), .16f, "wrist-flex-25"),
                    ("f_middle.01.L", new Vector3(15, 0, 0), .07f, "finger-curl-15"),
                    ("forearm.L", new Vector3(0, 0, 20), .22f, "elbow-bend-20") })
                {
                    Transform joint = Bone(pose.Item1); Quaternion rotation = joint.localRotation;
                    joint.localRotation = rotation * Quaternion.Euler(pose.Item2);
                    yield return null; yield return null;
                    skin.BakeMesh(baked, true); Vector3[] moved = baked.vertices;
                    Assert.That(moved.All(Finite), Is.True);
                    float displacement = rest.Zip(moved, Vector3.Distance).Max();
                    Assert.That(displacement, Is.InRange(.0005f, pose.Item3), pose.Item1 + " must move its skin without an invalid bind transform.");
                    Capture(rt, "inspection-" + pose.Item4);
                    joint.localRotation = rotation;
                    yield return null;
                    skin.BakeMesh(baked, true);
                    Assert.That(rest.Zip(baked.vertices, Vector3.Distance).Max(), Is.LessThan(.0001f), "Restoring the joint must reproduce the original surface.");
                }
            }
            finally { inspection.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(baked); Object.Destroy(inspection.gameObject); if (grip != null) grip.enabled = true; }
        }

        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        private static Color32[] Capture(RenderTexture rt, string name)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                Color32[] pixels = image.GetPixels32();
                using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence, name + ".tga")));
                writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                writer.Write((short)rt.width); writer.Write((short)rt.height); writer.Write((byte)24); writer.Write((byte)0);
                foreach (Color32 pixel in pixels) { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
                return pixels;
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }
    }
}
#endif
