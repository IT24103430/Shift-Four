#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonIdlePlayModeTests
    {
        private PistolGripPlayModeTests fixture;
        private Component player, model, presenter, idle, grip, weapons;
        private Camera camera;
        private Transform pivot, right, left, rightTarget, leftTarget;
        private SkinnedMeshRenderer skin;
        private float captureDelta;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;
        private static string Evidence
        {
            get { string p = Environment.GetEnvironmentVariable("SHIFTFOUR_IDLE_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "ArmsIdle"); Directory.CreateDirectory(p); return p; }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime;
            fixture = new PistolGripPlayModeTests(); yield return fixture.Setup();
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            weapons = player.GetComponent(T("WeaponController")); camera = (Camera)Get(player, "ViewCamera");
            model = (Component)Get(player.GetComponent(T("FirstPersonWeaponMount")), "Instance");
            presenter = model.GetComponent(T("PistolPresentation")); idle = model.GetComponent(T("FirstPersonIdleMotion"));
            grip = model.GetComponentInChildren(T("FirstPersonPistolGrip"));
            var bindings = model.GetComponentInChildren(T("FirstPersonArmsRig"));
            skin = (SkinnedMeshRenderer)Get(bindings, "Skin"); pivot = (Transform)Get(presenter, "RecoilPivot");
            right = (Transform)Get(bindings, "RightHand"); left = (Transform)Get(bindings, "LeftHand");
            rightTarget = (Transform)Get(grip, "RightHandTarget"); leftTarget = (Transform)Get(grip, "SupportHandTarget");
            Assert.That(idle, Is.Not.Null);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = captureDelta;
            yield return fixture.Cleanup();
        }

        [UnityTest] public IEnumerator StandingIdleThirtySeconds_KeepsCameraRayGripAndSkinStableWithoutDriftOrSnaps()
        {
            Time.captureDeltaTime = 1f / 60f; Time.timeScale = 1;
            var view = OpenGameView();
            if (view != null) { view.Repaint(); if (!Application.isBatchMode) view.Focus(); }
            yield return new WaitForSeconds(.3f);
            Call(idle, "ResetMotion"); Call(presenter, "ApplyPose", true);
            Vector3 eye = camera.transform.position, aim = camera.transform.forward, physicsRoot = player.transform.position;
            Quaternion viewRotation = camera.transform.rotation;
            Vector3 restPosition = (Vector3)Field(presenter, "restPosition"); Quaternion restRotation = (Quaternion)Field(presenter, "restRotation");
            Vector3 previous = pivot.localPosition; Quaternion previousRotation = pivot.localRotation;
            Vector3[] boneOffsets = skin.bones.Select(b => b.localPosition).ToArray();
            var baked = new Mesh(); skin.BakeMesh(baked, true); Vector3[] originalSkin = baked.vertices;
            float maxStep = 0, maxRight = 0, maxLeft = 0, maxSkin = 0; double maxAngularStep = 0;
            var csv = new StringBuilder("frame,elapsed,phase,weight,x_mm,y_mm,z_mm,rotation_degrees,right_error_mm,left_error_mm\n");
            try
            {
                for (int frame = 1; frame <= 1800; frame++)
                {
                    yield return null;
                    Vector3 p = (Vector3)Get(idle, "PositionOffset"); Quaternion q = (Quaternion)Get(idle, "RotationOffset");
                    float r = Vector3.Distance(right.position, rightTarget.position), l = Vector3.Distance(left.position, leftTarget.position);
                    maxRight = Mathf.Max(maxRight, r); maxLeft = Mathf.Max(maxLeft, l);
                    maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, pivot.localPosition));
                    maxAngularStep = Math.Max(maxAngularStep, Degrees(Quaternion.Inverse(previousRotation) * pivot.localRotation));
                    Assert.That(Vector3.Distance(camera.transform.position, eye), Is.LessThan(.00001f));
                    Assert.That(Degrees(Quaternion.Inverse(viewRotation) * camera.transform.rotation), Is.LessThan(.001));
                    Assert.That(Vector3.Distance(player.transform.position, physicsRoot), Is.LessThan(.00001f));
                    Assert.That(Vector3.Angle(camera.transform.forward, aim), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(pivot.localPosition - p, restPosition), Is.LessThan(.000001f));
                    Assert.That(Degrees(Quaternion.Inverse(restRotation * q) * pivot.localRotation), Is.LessThan(.001));
                    Assert.That(p.magnitude, Is.LessThan(.001f)); Assert.That(r, Is.LessThan(.0002f)); Assert.That(l, Is.LessThan(.0002f));
                    previous = pivot.localPosition; previousRotation = pivot.localRotation;
                    csv.AppendLine(string.Join(",", new[] { frame.ToString(), ((double)Get(idle, "ElapsedSeconds")).ToString("R", CultureInfo.InvariantCulture),
                        ((double)Get(idle, "Phase")).ToString("R", CultureInfo.InvariantCulture), ((float)Get(idle, "Weight")).ToString("R", CultureInfo.InvariantCulture),
                        (p.x * 1000).ToString("R", CultureInfo.InvariantCulture), (p.y * 1000).ToString("R", CultureInfo.InvariantCulture), (p.z * 1000).ToString("R", CultureInfo.InvariantCulture),
                        Degrees(q).ToString("R", CultureInfo.InvariantCulture), (r * 1000).ToString("R", CultureInfo.InvariantCulture), (l * 1000).ToString("R", CultureInfo.InvariantCulture) }));
                    if (frame % 300 == 0)
                    {
                        skin.BakeMesh(baked, true); maxSkin = Mathf.Max(maxSkin, originalSkin.Zip(baked.vertices, Vector3.Distance).Max());
                        CollectionAssert.AreEqual(boneOffsets, skin.bones.Select(b => b.localPosition).ToArray());
                    }
                    if (new[] { 1, 75, 150, 225, 300, 900, 1800 }.Contains(frame)) CaptureView(view, "idle-frame-" + frame.ToString("D4"));
                }
                Assert.That((double)Get(idle, "ElapsedSeconds"), Is.EqualTo(30).Within(.0001));
                Assert.That(Vector3.Distance(pivot.localPosition, restPosition), Is.LessThan(.000001f));
                Assert.That(maxStep, Is.LessThan(.00006f)); Assert.That(maxAngularStep, Is.LessThan(.02));
                Assert.That(maxSkin, Is.LessThan(.00003f), "Idle should rigidly carry the existing grip, without changing wrist/finger skin shape.");
                File.WriteAllText(Path.Combine(Evidence, "standing-summary.txt"), "1800 actual Play Mode frames, " + Get(idle, "ElapsedSeconds") + " simulated seconds at 60 FPS.\n" +
                    "Camera position/orientation, player physics root and camera ray stationary.\n" +
                    "Maximum per-frame position step (m)=" + maxStep + ", rotation step (degrees)=" + maxAngularStep +
                    "; right contact error (m)=" + maxRight + ", support contact error (m)=" + maxLeft + ", local skin variation (m)=" + maxSkin + ".\n");
            }
            finally { Object.Destroy(baked); File.WriteAllText(Path.Combine(Evidence, "standing-30-seconds.csv"), csv.ToString()); }
        }

        [UnityTest] public IEnumerator RecoilAndReload_SuppressIdleAndBlendBackWithoutMovingCameraOrBreakingGrip()
        {
            Time.captureDeltaTime = 1f / 60f; Time.timeScale = 1;
            yield return new WaitForSeconds(.3f);
            Call(idle, "ResetMotion"); Call(idle, "Advance", .7d, false); Call(presenter, "ApplyPose", true);
            Vector3 eye = camera.transform.position, aim = camera.transform.forward;
            Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
            var shot = Get(model, "LastShot"); var ray = (Ray)shot.GetType().GetField("Ray").GetValue(shot);
            Assert.That(ray.origin, Is.EqualTo(eye)); Assert.That(Vector3.Angle(ray.direction, aim), Is.LessThan(.001f));
            yield return new WaitForSeconds(.12f);
            Assert.That((float)Get(idle, "Weight"), Is.LessThan(.7f));
            Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
            Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            yield return new WaitForSeconds(.7f);
            Assert.That((float)Get(idle, "Weight"), Is.LessThan(.03f));
            Assert.That(Call(weapons, "CancelReload"), Is.EqualTo(true));
            Vector3 previous = (Vector3)Get(idle, "PositionOffset"); float maxStep = 0;
            for (int i = 0; i < 60; i++)
            {
                yield return null; Vector3 p = (Vector3)Get(idle, "PositionOffset");
                maxStep = Mathf.Max(maxStep, Vector3.Distance(previous, p)); previous = p;
                Assert.That(Vector3.Distance(camera.transform.position, eye), Is.LessThan(.00001f));
                Assert.That(Vector3.Angle(camera.transform.forward, aim), Is.LessThan(.001f));
                Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
                Assert.That(Vector3.Distance(left.position, leftTarget.position), Is.LessThan(.0002f));
            }
            Assert.That((float)Get(idle, "Weight"), Is.GreaterThan(.99f)); Assert.That(maxStep, Is.LessThan(.0001f));
            File.WriteAllText(Path.Combine(Evidence, "activity-blending.txt"), "Real shot and reload suppress breathing; cancel/recovery fades back above 99% with max idle offset step " + maxStep + " m. Camera ray and both wrist contacts retained.\n");
        }

        [UnityTest] public IEnumerator RenderedFrames_AtThirtySixtyAndOneTwentyFps_RetainGripAndMatchIdleSamples()
        {
            Vector3[] expected = null;
            var report = new StringBuilder("Actual rendered Play Mode frames with captureDeltaTime = 1/FPS. Five seconds per rate.\n");
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Time.captureDeltaTime = 1f / fps; Time.timeScale = 1;
                yield return null; yield return null;
                Call(idle, "ResetMotion"); Call(presenter, "ApplyPose", true);
                Vector3 eye = camera.transform.position, aim = camera.transform.forward;
                var samples = new Vector3[5]; float maxRight = 0, maxLeft = 0;
                for (int frame = 1; frame <= fps * 5; frame++)
                {
                    yield return null;
                    maxRight = Mathf.Max(maxRight, Vector3.Distance(right.position, rightTarget.position));
                    maxLeft = Mathf.Max(maxLeft, Vector3.Distance(left.position, leftTarget.position));
                    Assert.That(Vector3.Distance(camera.transform.position, eye), Is.LessThan(.00001f));
                    Assert.That(Vector3.Angle(camera.transform.forward, aim), Is.LessThan(.001f));
                    if (frame % fps == 0) samples[frame / fps - 1] = (Vector3)Get(idle, "PositionOffset");
                }
                Assert.That(maxRight, Is.LessThan(.0002f)); Assert.That(maxLeft, Is.LessThan(.0002f));
                Assert.That((double)Get(idle, "ElapsedSeconds"), Is.EqualTo(5).Within(.0001));
                if (expected != null)
                    for (int i = 0; i < samples.Length; i++)
                        Assert.That(Vector3.Distance(samples[i], expected[i]), Is.LessThan(.000001f));
                expected = samples;
                report.AppendLine(fps + " FPS: " + fps * 5 + " frames; matching one-second samples, max dominant/support contact errors (m)=" + maxRight + "/" + maxLeft);
            }
            File.WriteAllText(Path.Combine(Evidence, "rendered-rate-comparison.txt"), report.ToString());
        }

        private static EditorWindow OpenGameView()
        {
            Type type = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            if (type == null) return null;
            return Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => type.IsInstanceOfType(w)) ??
                (Application.isBatchMode ? null : EditorWindow.GetWindow(type));
        }

        private static void CaptureView(EditorWindow view, string name)
        {
            if (view == null) return;
            for (Type type = view.GetType(); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(RenderTexture).IsAssignableFrom(field.FieldType) && field.GetValue(view) is RenderTexture rt && rt.IsCreated() && rt.width >= 320)
                    {
                        RenderTexture previous = RenderTexture.active; RenderTexture.active = rt;
                        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        try
                        {
                            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
                            using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence, name + ".tga")));
                            writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); writer.Write((short)rt.width); writer.Write((short)rt.height);
                            writer.Write((byte)24); writer.Write((byte)(SystemInfo.graphicsUVStartsAtTop ? 0x20 : 0));
                            foreach (Color32 p in image.GetPixels32()) { writer.Write(p.b); writer.Write(p.g); writer.Write(p.r); }
                            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "Actual Unity Editor " + type.FullName + "." + field.Name + ", " + rt.width + "x" + rt.height + ", includes HUD. TGA top-origin=" + SystemInfo.graphicsUVStartsAtTop + ".\n");
                        }
                        finally { RenderTexture.active = previous; Object.Destroy(image); }
                        return;
                    }
        }
    }
}
#endif
