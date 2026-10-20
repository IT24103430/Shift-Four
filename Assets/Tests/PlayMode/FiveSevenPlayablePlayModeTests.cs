#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class FiveSevenPlayablePlayModeTests
    {
        private Component player, weapons, mount, model, presentation, target;
        private Camera camera;
        private NoiseListenerProbe noise;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private float captureDelta;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object instance, string property) => instance.GetType().GetProperty(property).GetValue(instance);
        private static object Call(object instance, string method, params object[] args)
            => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(instance, args);
        private bool Fire() => (bool)Call(weapons, "TryFire");
        private float HP => (float)Get(target, "Current");
        private Transform Pivot => (Transform)Get(presentation, "RecoilPivot");
        private Renderer Flash => (Renderer)Get(presentation, "MuzzleFlash");

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible; captureDelta = Time.captureDeltaTime;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/FPS/FiveSevenRange.unity", new LoadSceneParameters(LoadSceneMode.Single));
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            weapons = PlayerWeaponTestActions.Controller(player);
            camera = (Camera)Get(player, "ViewCamera"); mount = player.GetComponent(T("FirstPersonWeaponMount"));
            yield return null;
            Cursor.lockState = CursorLockMode.None;
            model = (Component)Get(mount, "Instance"); Assert.That(model, Is.Not.Null);
            presentation = model.GetComponent(T("PistolPresentation")); Assert.That(presentation, Is.Not.Null);
            target = GameObject.Find("Target - 25 damage per shot").GetComponent(T("Health"));
            T("SoundEvents").GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            noise = new GameObject("Pistol noise probe").AddComponent<NoiseListenerProbe>();
            Physics.SyncTransforms();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = captureDelta;
            Scene old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After pistol fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        [UnityTest] public IEnumerator RuntimeDefault_EquipsPistolBelowExistingCameraWithoutChangingCapsule()
        {
            Assert.That(PlayerWeaponTestActions.Data(player, "Damage"), Is.EqualTo(25f));
            Assert.That(Get(PlayerWeaponTestActions.Definition(player), "DisplayName"), Is.EqualTo("FN Five-seveN"));
            Transform socket = (Transform)Get(mount, "Socket");
            Assert.That(socket.name, Is.EqualTo("WeaponSocket")); Assert.That(socket.parent, Is.SameAs(camera.transform));
            Assert.That(socket.localPosition, Is.EqualTo(Vector3.zero)); Assert.That(socket.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(model.transform.parent, Is.SameAs(socket));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.3f).Within(.0001f));
            Assert.That(player.GetComponent<CharacterController>().height, Is.EqualTo(1.8f));
            Assert.That(player.GetComponents(T("WeaponController")).Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator ActualPistol_CrosshairDamageCooldownAndShotNoiseRemainCorrect()
        {
            var probe = new GameObject("Pistol events").AddComponent<WeaponEventProbe>(); probe.Bind(weapons, target);
            Vector3 pose = camera.transform.localPosition; Quaternion rotation = camera.transform.rotation;
            Assert.That(Fire(), Is.True); Assert.That(Fire(), Is.False);
            Assert.That(HP, Is.EqualTo(75f)); Assert.That(probe.Damages, Is.EqualTo(1)); Assert.That(probe.Shots, Is.EqualTo(1));
            Assert.That(noise.Shots, Is.EqualTo(1)); Assert.That(noise.Noises, Is.EqualTo(1));
            object shot = Get(model, "LastShot");
            RaycastHit hit = (RaycastHit)shot.GetType().GetField("Hit").GetValue(shot);
            Vector3 screen = camera.WorldToViewportPoint(hit.point);
            Assert.That(screen.x, Is.EqualTo(.5f).Within(.0001f)); Assert.That(screen.y, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(hit.collider.gameObject, Is.SameAs(target.gameObject));
            Assert.That(camera.transform.localPosition, Is.EqualTo(pose)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            yield return new WaitForSeconds(.26f);
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(50f)); Assert.That(noise.Shots, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator RecoilAndFlash_ArePresentationOnlyAndRecoverWithoutDuplicateShots()
        {
            Vector3 rest = Pivot.localPosition; Quaternion rotation = Pivot.localRotation;
            Assert.That(Flash.enabled, Is.False); Assert.That(Fire(), Is.True);
            Assert.That(Flash.enabled, Is.True); Assert.That(Vector3.Distance(Pivot.localPosition, rest), Is.InRange(.01f, .02f));
            Assert.That(Quaternion.Angle(Pivot.localRotation, rotation), Is.InRange(1f, 3f));
            Assert.That(Fire(), Is.False); Assert.That(noise.Shots, Is.EqualTo(1));
            yield return new WaitForSeconds(.065f); Assert.That(Flash.enabled, Is.False);
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(Pivot.localPosition, rest), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(Pivot.localRotation, rotation), Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator PitchYawAndJump_KeepSocketAndNearPlaneClearance()
        {
            Vector3 local = model.transform.localPosition; Vector3 rest = Pivot.localPosition;
            foreach (float pitch in new[] { -80f, 0f, 80f })
            {
                player.transform.rotation = Quaternion.Euler(0, 57, 0); camera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                yield return null;
                Assert.That(Vector3.Dot(model.transform.forward, camera.transform.forward), Is.GreaterThan(.9999f));
                AssertClearance();
            }
            camera.transform.localRotation = Quaternion.identity;
            for (int i = 0; i < 3; i++) Call(player, "MovePlayer", Vector3.zero, 1f / 60f);
            Assert.That(Call(player, "TryJump"), Is.True); float peak = player.transform.position.y;
            for (int i = 0; i < 80; i++)
            {
                Call(player, "MovePlayer", Vector3.zero, 1f / 60f); Call(player, "ApplyCameraPose");
                peak = Mathf.Max(peak, player.transform.position.y);
                Assert.That(model.transform.localPosition, Is.EqualTo(local)); Assert.That(Pivot.localPosition, Is.EqualTo(rest));
                Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.3f).Within(.0001f)); AssertClearance();
            }
            Assert.That(peak, Is.GreaterThan(1f)); Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
        }

        [UnityTest] public IEnumerator ViewmodelHasNoPhysicsAndCannotObstructInteractionOrCover()
        {
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(model.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true)) Assert.That(node.gameObject.layer, Is.EqualTo(2));
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.transform.position = camera.transform.position + camera.transform.forward * 2f;
            Physics.SyncTransforms(); Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(100f));
            Object.Destroy(cover); yield return null;
            target.gameObject.SetActive(false);
            var console = GameObject.CreatePrimitive(PrimitiveType.Cube); console.transform.position = camera.transform.position + camera.transform.forward * 2f;
            var interaction = console.AddComponent(T("ConsoleSwitch")); Call(interaction, "Configure", "Checkpoint", null);
            Physics.SyncTransforms(); Call(player, "UpdateInteraction");
            Assert.That(Get(player, "InteractionPrompt"), Is.EqualTo("E: activate Checkpoint"));
        }

        [UnityTest] public IEnumerator CameraReplacementAndEquipCycle_DoNotDuplicateModelsOrLeaveFlashRunning()
        {
            object pistol = PlayerWeaponTestActions.Definition(player); Assert.That(Fire(), Is.True); Assert.That(Flash.enabled, Is.True);
            var data = ScriptableObject.CreateInstance(T("WeaponDefinition"));
            try
            {
                Assert.That(Call(weapons, "Equip", data), Is.True); yield return null;
                Assert.That(Get(mount, "Instance"), Is.Null);
                Assert.That(Call(weapons, "Equip", pistol), Is.True); yield return null;
                model = (Component)Get(mount, "Instance"); presentation = model.GetComponent(T("PistolPresentation"));
                Assert.That(Flash.enabled, Is.False); Assert.That(Fire(), Is.False, "Switching must preserve the pistol cooldown.");
                var replacement = new GameObject("Replacement camera").AddComponent<Camera>(); replacement.enabled = false;
                replacement.transform.SetParent(player.transform, false); Call(player, "Configure", replacement); yield return null;
                Assert.That(((Transform)Get(mount, "Socket")).parent, Is.SameAs(replacement.transform));
                Assert.That(player.GetComponentsInChildren(T("PistolPresentation"), true).Length, Is.EqualTo(1));
            }
            finally { Object.Destroy(data); }
        }

        [UnityTest] public IEnumerator Death_HidesViewmodelAndStopsFlashAndFurtherShots()
        {
            Assert.That(Fire(), Is.True); Call(player.GetComponent(T("Health")), "TakeDamage", 1000f);
            yield return null;
            Assert.That(model.gameObject.activeInHierarchy, Is.False); Assert.That(Flash.enabled, Is.False);
            Assert.That(Fire(), Is.False); Assert.That(noise.Shots, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator GameCamera_RendersPistolWithMaterialsAndAcceptedShotPresentation()
        {
            Time.captureDeltaTime = 1f / 60f;
            float timeScale = Time.timeScale;
            var render = new RenderTexture(1280, 720, 24); render.Create(); camera.targetTexture = render;
            camera.aspect = 16f / 9f;
            try
            {
                yield return null; yield return null;
                Color32[] withGun = Capture(render, "range-idle");
                foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
                yield return null;
                Color32[] withoutGun = Capture(render, "range-without-gun-control");
                int changed = 0, pink = 0;
                for (int i = 0; i < withGun.Length; i++)
                {
                    Color32 a = withGun[i], b = withoutGun[i];
                    if (Math.Abs(a.r-b.r) + Math.Abs(a.g-b.g) + Math.Abs(a.b-b.b) > 30) changed++;
                    if (a.r > 220 && a.b > 220 && a.g < 60) pink++;
                }
                Assert.That(changed, Is.GreaterThan(1500), "Pistol must visibly contribute rendered pixels.");
                Assert.That(pink, Is.LessThan(5), "No missing-shader pink pixels.");
                foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = true;
                Flash.enabled = false;
                Time.timeScale = 0f; // Freeze the accepted-shot pose for a matched flash/no-flash render comparison.
                Assert.That(Fire(), Is.True); Assert.That(Flash.enabled, Is.True);
                yield return null; Color32[] flashed = Capture(render, "range-fired");
                Flash.enabled = false;
                yield return null; Color32[] noFlash = Capture(render, "range-fired-no-flash-control");
                int flashChanged = 0;
                for (int i = 0; i < flashed.Length; i++)
                    if (Math.Abs(flashed[i].r-noFlash[i].r)+Math.Abs(flashed[i].g-noFlash[i].g)+Math.Abs(flashed[i].b-noFlash[i].b)>30) flashChanged++;
                Assert.That(flashChanged, Is.GreaterThan(20), "Accepted-shot flash must visibly contribute pixels beyond the gun silhouette.");
                Time.timeScale = timeScale;
                yield return new WaitForSeconds(.45f); Capture(render, "range-recovered");
                string directory = Evidence();
                File.WriteAllText(Path.Combine(directory, "render-summary.json"), "{\"visibleChangedPixels\":"+changed+",\"flashChangedPixels\":"+flashChanged+",\"missingShaderPinkPixels\":"+pink+",\"capture\":\"Actual game-camera render target; HUD not included. Shot pose paused for flash comparison.\"}");
            }
            finally { Time.timeScale = timeScale; camera.targetTexture = null; render.Release(); Object.Destroy(render); }
        }

        [UnityTest] public IEnumerator OwnedAssembly_FrontAndSideInspectionRenders()
        {
            var inspection = new GameObject("Assembly inspection camera").AddComponent<Camera>();
            inspection.orthographic = true; inspection.orthographicSize = .12f;
            inspection.clearFlags = CameraClearFlags.SolidColor; inspection.backgroundColor = new Color(.08f, .1f, .13f);
            inspection.nearClipPlane = .01f;
            var render = new RenderTexture(960, 720, 24); render.Create(); inspection.targetTexture = render;
            Vector3 center = Pivot.position;
            var muzzle = (Transform)Get(presentation, "MuzzlePoint");
            try
            {
                inspection.transform.position = center + camera.transform.forward * .5f;
                inspection.transform.LookAt(center); yield return null; Capture(render, "assembly-front");
                inspection.transform.position = center + camera.transform.right * .5f;
                inspection.transform.LookAt(center); yield return null; Capture(render, "assembly-side");
                File.WriteAllText(Path.Combine(Evidence(), "muzzle-position.json"), JsonUtility.ToJson(new MuzzleAudit {
                    weaponLocal = muzzle.localPosition, cameraLocal = camera.transform.InverseTransformPoint(muzzle.position), forward = muzzle.forward }));
            }
            finally { inspection.targetTexture = null; render.Release(); Object.Destroy(render); Object.Destroy(inspection.gameObject); }
        }

        [Serializable] private sealed class MuzzleAudit { public Vector3 weaponLocal, cameraLocal, forward; }

        private void AssertClearance()
        {
            foreach (MeshFilter mesh in model.GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = mesh.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                    Assert.That(camera.transform.InverseTransformPoint(mesh.transform.TransformPoint(p)).z,
                        Is.GreaterThan(camera.nearClipPlane + .05f), mesh.name);
                }
            }
        }

        private static string Evidence()
        {
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_PISTOL_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "FiveSevenEvidence");
            Directory.CreateDirectory(directory); return directory;
        }
        private static Color32[] Capture(RenderTexture render, string name)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = render;
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); texture.Apply();
                Color32[] colors = texture.GetPixels32();
                using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence(), name + ".tga")));
                writer.Write(new byte[] {0,0,2,0,0,0,0,0,0,0,0,0}); writer.Write((short)render.width); writer.Write((short)render.height);
                writer.Write((byte)24); writer.Write((byte)0);
                foreach (Color32 color in colors) { writer.Write(color.b); writer.Write(color.g); writer.Write(color.r); }
                return colors;
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
    }
}
#endif
