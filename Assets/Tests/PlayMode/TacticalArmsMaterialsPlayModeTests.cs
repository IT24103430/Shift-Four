#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class TacticalArmsMaterialsPlayModeTests
    {
        private Component player, arms, session;
        private SkinnedMeshRenderer skin;
        private Camera camera;
        private float timeScale;
        private CursorLockMode cursor;
        private bool visible;
        private static readonly string[] SurfaceNames = { "GloveShell", "SleeveRipstop", "CuffElastic", "GloveReinforcement", "KnuckleRubber" };
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static string Evidence
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("SHIFTFOUR_ARMS_MATERIAL_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "TacticalArmsMaterials");
                Directory.CreateDirectory(path); return path;
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            timeScale = Time.timeScale; cursor = Cursor.lockState; visible = Cursor.visible; Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return BindAndFreeze();
        }

        private IEnumerator BindAndFreeze()
        {
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            session = Object.FindAnyObjectByType(T("GameSession")) as Component;
            camera = (Camera)Get(player, "ViewCamera");
            arms = player.GetComponentInChildren(T("FirstPersonArms"), true);
            skin = arms.GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach (Component brain in Object.FindObjectsByType(T("DroneBrain")))
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false; Time.timeScale = 1;
            yield return PlayerWeaponTestActions.WaitForReady(player); Time.timeScale = 0;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = timeScale;
            Scene old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After material fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        [UnityTest] public IEnumerator MainLab_SurfacesRenderWithoutPinkOrClippedWhiteHighlights()
        {
            var view = GameViewWindow();
            if (view != null) { view.Repaint(); if (!Application.isBatchMode) view.Focus(); }
            for (int i = 0; i < 6; i++) yield return null;
            CaptureGameView(view, "mainlab-game-view");

            var rt = new RenderTexture(1600, 900, 24); rt.Create();
            camera.targetTexture = rt; camera.aspect = 16f / 9f;
            var report = new StringBuilder("Unity=" + Application.unityVersion + ", graphics=" + SystemInfo.graphicsDeviceName + ", API=" + SystemInfo.graphicsDeviceType + "\n");
            try
            {
                foreach (Material material in skin.sharedMaterials)
                    report.AppendLine(material.name + " color=" + material.GetColor("_BaseColor") + ", smoothnessCap=" + material.GetFloat("_Smoothness") + ", normalStrength=" + material.GetFloat("_BumpScale") + ", mask=" + material.GetTexture("_MetallicGlossMap").name);
                foreach (Light light in Object.FindObjectsByType<Light>())
                    report.AppendLine("Existing light=" + light.name + ", type=" + light.type + ", color=" + light.color + ", intensity=" + light.intensity);
                Quaternion rotation = camera.transform.rotation;
                int totalArmPixels = 0, totalPink = 0, totalWhite = 0;
                foreach (int yaw in new[] { 0, -35, 35 })
                {
                    camera.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rotation;
                    yield return null; yield return null;
                    Color32[] painted = Capture(rt, "mainlab-materials-yaw-" + yaw);
                    skin.enabled = false; yield return null; yield return null;
                    Color32[] control = Capture(rt, "mainlab-without-arms-yaw-" + yaw);
                    skin.enabled = true;
                    int pixels = 0, pink = 0, white = 0;
                    for (int i = 0; i < painted.Length; i++)
                    {
                        Color32 a = painted[i], b = control[i];
                        if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) <= 30) continue;
                        pixels++;
                        if (a.r > 220 && a.b > 220 && a.g < 60) pink++;
                        if (a.r > 235 && a.g > 235 && a.b > 235) white++;
                    }
                    Assert.That(pixels, Is.GreaterThan(5000));
                    Assert.That(pink, Is.Zero, "No arm surface may render missing-shader pink.");
                    Assert.That((float)white / pixels, Is.LessThan(.005f), "Dry tactical materials must not produce broad clipped-white highlights under MainLab lighting.");
                    report.AppendLine("yaw=" + yaw + ", armPixels=" + pixels + ", pink=" + pink + ", clippedWhite=" + white);
                    totalArmPixels += pixels; totalPink += pink; totalWhite += white;
                }
                camera.transform.rotation = rotation;
                File.WriteAllText(Path.Combine(Evidence, "mainlab-material-render.txt"), report.ToString());
                File.WriteAllText(Path.Combine(Evidence, "render-summary.json"), "{\"sampledViews\":3,\"armPixels\":" + totalArmPixels + ",\"pinkArmPixels\":" + totalPink + ",\"clippedWhiteArmPixels\":" + totalWhite + "}");
            }
            finally { skin.enabled = true; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); }
        }

        [UnityTest] public IEnumerator SceneRestart_PreservesTheSameSharedMaterialAssets()
        {
            Material[] expected = skin.sharedMaterials;
            Material[] inventory = ArmsMaterials();
            for (int i = 0; i < 3; i++)
            {
                var weapons = Get(player, "Weapons");
                Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
                Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
                Call(player.GetComponent(T("Health")), "TakeDamage", 1000f);
                Assert.That(Get(session, "Ended"), Is.EqualTo(true));
                Call(session, "Restart");
                yield return null; yield return null; yield return BindAndFreeze();
                CollectionAssert.AreEqual(expected, skin.sharedMaterials, "Restart must retain the exact authored material asset references.");
                CollectionAssert.AreEquivalent(inventory, ArmsMaterials(), "Restart must not leave extra arms materials resident.");
                Assert.That(Get(arms, "IsReady"), Is.EqualTo(true));
            }
            File.WriteAllText(Path.Combine(Evidence, "restart-materials.txt"), "Three real MainLab scene restarts: shared material references and material inventory unchanged.\n");
        }

        [UnityTest] public IEnumerator RepeatedInitialization_DoesNotAllocateOrOverwriteArmsMaterials()
        {
            Material[] expected = skin.sharedMaterials;
            Material[] inventory = ArmsMaterials();
            var pivot = (Transform)Get(arms.GetComponent(T("PistolPresentation")), "RecoilPivot");
            Call(arms, "Initialize", pivot);
            CollectionAssert.AreEqual(expected, skin.sharedMaterials);
            for (int i = 0; i < 12; i++)
            {
                var probe = new GameObject("Arms material lifetime probe");
                var presenter = probe.AddComponent(T("FirstPersonArms"));
                Call(presenter, "Initialize", probe.transform);
                CollectionAssert.AreEqual(expected, probe.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterials);
                Object.Destroy(probe); yield return null;
            }
            yield return null;
            CollectionAssert.AreEquivalent(inventory, ArmsMaterials());
            File.WriteAllText(Path.Combine(Evidence, "material-lifetime.txt"), "Twelve create/destroy cycles and repeated Initialize: zero additional arms Material objects.\n");
        }

        private static Material[] ArmsMaterials() => Resources.FindObjectsOfTypeAll<Material>()
            .Where(m => SurfaceNames.Any(n => m.name == n || m.name.StartsWith(n + " (") || m.name.StartsWith("First person ")))
            .OrderBy(m => m.name).ToArray();

        private static EditorWindow GameViewWindow()
        {
            Type type = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            if (type == null) return null;
            var existing = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => type.IsInstanceOfType(w));
            return existing != null ? existing : Application.isBatchMode ? null : EditorWindow.GetWindow(type);
        }

        // Capture the actual Game-view render surface when the editor exposes one, including its HUD.
        // Raw camera captures above are independent evidence if a batch editor has no Game view.
        private static void CaptureGameView(EditorWindow view, string name)
        {
            if (view != null)
                for (Type type = view.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        if (typeof(RenderTexture).IsAssignableFrom(field.FieldType) && field.GetValue(view) is RenderTexture target && target.IsCreated() && target.width >= 320 && target.height >= 200)
                        {
                            // Metal's Editor Game-view surface is vertically inverted relative to camera targets.
                            // Record the TGA origin accordingly; retain every captured pixel unchanged.
                            Capture(target, name, SystemInfo.graphicsUVStartsAtTop);
                            File.WriteAllText(Path.Combine(Evidence, "game-view-capture.txt"), "Actual Unity Editor Game-view render surface: " + type.FullName + "." + field.Name + ", " + target.width + "x" + target.height + ". TGA top origin=" + SystemInfo.graphicsUVStartsAtTop + ".\n");
                            return;
                        }
            File.WriteAllText(Path.Combine(Evidence, "game-view-capture.txt"), "No readable Game-view surface was present in this editor session. Separate captures use the actual player camera.\n");
        }

        private static Color32[] Capture(RenderTexture target, string name, bool topOrigin = false)
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                Color32[] pixels = texture.GetPixels32();
                using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence, name + ".tga")));
                writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                writer.Write((short)target.width); writer.Write((short)target.height); writer.Write((byte)24); writer.Write((byte)(topOrigin ? 0x20 : 0));
                foreach (Color32 pixel in pixels) { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
                return pixels;
            }
            finally { RenderTexture.active = previous; Object.Destroy(texture); }
        }
    }
}
#endif
