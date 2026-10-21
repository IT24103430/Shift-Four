#if UNITY_EDITOR
using System;
using System.Collections;
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
    public sealed class FirstPersonReloadPlayModeTests
    {
        private PistolGripPlayModeTests fixture;
        private Component player, weapons, mount, model, presenter, reload, rig, grip;
        private Camera camera;
        private Transform magazine, pivot, right, left, rightTarget, magazineTarget;
        private SkinnedMeshRenderer skin;
        private Vector3 magazineRest, cameraRest;
        private Quaternion magazineRotation, cameraRotation, wristNeutral;
        private float captureDelta;
        private ScriptableObject temporaryDefinition;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;
        private static string Evidence
        {
            get { string p = Environment.GetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "ArmsReload"); Directory.CreateDirectory(p); return p; }
        }
        private object State => Get(weapons, "ActiveWeapon");
        private int Ammo => (int)Get(State, "RoundsInMagazine");
        private int Reserve => (int)Get(State, "ReserveRounds");
        private bool Reloading => Get(State, "ReloadState").ToString() == "Reloading";
        private float Duration => (float)Get(Get(State, "Definition"), "ReloadDuration");
        private float Progress => (float)Get(model, "ReloadProgress");
        private bool Fire() => (bool)Call(weapons, "TryFire");

        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime; fixture = new PistolGripPlayModeTests(); yield return fixture.Setup();
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component; camera = (Camera)Get(player, "ViewCamera");
            weapons = player.GetComponent(T("WeaponController")); mount = player.GetComponent(T("FirstPersonWeaponMount"));
            Time.timeScale = 1; Time.captureDeltaTime = 1f / 120f; Resolve();
            cameraRest = camera.transform.localPosition; cameraRotation = camera.transform.localRotation;
            yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = captureDelta; if (temporaryDefinition != null) Object.Destroy(temporaryDefinition);
            yield return fixture.Cleanup();
        }
        private void Resolve()
        {
            model = (Component)Get(mount, "Instance"); presenter = model.GetComponent(T("PistolPresentation"));
            reload = model.GetComponent(T("FirstPersonReloadMotion")); Assert.That(reload, Is.Not.Null);
            magazine = (Transform)Get(presenter, "MagazinePart"); pivot = (Transform)Get(presenter, "RecoilPivot");
            magazineTarget = (Transform)Get(reload, "MagazineGripTarget");
            rig = model.GetComponentInChildren(T("FirstPersonArmsRig")); grip = rig.GetComponent(T("FirstPersonPistolGrip"));
            right = (Transform)Get(rig, "RightHand"); left = (Transform)Get(rig, "LeftHand"); rightTarget = (Transform)Get(grip, "RightHandTarget");
            skin = (SkinnedMeshRenderer)Get(rig, "Skin"); wristNeutral = left.localRotation;
            magazineRest = magazine.localPosition; magazineRotation = magazine.localRotation;
        }
        private void AssertCameraAndDominant()
        {
            Assert.That(Vector3.Distance(camera.transform.localPosition, cameraRest), Is.LessThan(.00002f));
            Assert.That(Degrees(Quaternion.Inverse(cameraRotation) * camera.transform.localRotation), Is.LessThan(.001));
            Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
            Assert.That(Degrees(Quaternion.Inverse(wristNeutral) * left.localRotation), Is.LessThanOrEqualTo(35.02));
            Assert.That(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
        }
        private void AssertRest()
        {
            Assert.That(Vector3.Distance(magazine.localPosition, magazineRest), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(magazineRotation) * magazine.localRotation), Is.LessThan(.001));
            Assert.That(Get(reload, "ContactWeight"), Is.EqualTo(0f));
            Assert.That(Vector3.Distance(left.position, ((Transform)Get(grip, "SupportHandTarget")).position), Is.LessThan(.0002f));
        }
        private void Export(string name)
        {
            string old = Environment.GetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE");
            try { Environment.SetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE", Path.Combine(Evidence, "geometry", name)); Call(fixture, "ExportGeometry"); }
            finally { Environment.SetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE", old); }
        }
        private IEnumerator FireReady()
        {
            while (Time.time < (float)Get(State, "NextShotTime")) yield return null;
            Assert.That(Fire(), Is.True);
        }

        [UnityTest] public IEnumerator NormalReload_TracksMagazineWithoutStretchingAndTransfersAmmoOnlyAtGameplayCompletion()
        {
            yield return FireReady(); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            int ammo = Ammo, reserve = Reserve; float start = Time.time, maxPositionError = 0; double maxRotationError = 0;
            var offsets = skin.bones.Select(b => b.localPosition).ToArray(); var scales = skin.bones.Select(b => b.localScale).ToArray();
            var fingers = skin.bones.Where(b => b.name.StartsWith("f_") || b.name.StartsWith("thumb.") || b.name.StartsWith("palm_")).ToArray();
            var fingerRotations = fingers.Select(b => b.localRotation).ToArray();
            var trace = new StringBuilder("time,progress,contact,mag_x,mag_y,mag_z,wrist_position_error_m,wrist_rotation_error_degrees,ammo,reserve\n");
            float[] stages = { .16f, .28f, .38f, .49f, .56f, .70f, .83f, .92f }; int stage = 0;
            try
            {
                for (int frame = 0; frame < 250 && Reloading; frame++)
                {
                    yield return null;
                    AssertCameraAndDominant(); CollectionAssert.AreEqual(offsets, skin.bones.Select(b => b.localPosition).ToArray());
                    CollectionAssert.AreEqual(scales, skin.bones.Select(b => b.localScale).ToArray());
                    CollectionAssert.AreEqual(fingerRotations, fingers.Select(b => b.localRotation).ToArray());
                    float contact = (float)Get(reload, "ContactWeight");
                    float positionError = Vector3.Distance(left.position, magazineTarget.position);
                    double rotationError = Degrees(Quaternion.Inverse(magazineTarget.rotation) * left.rotation);
                    if (contact > .999f) { maxPositionError = Mathf.Max(maxPositionError, positionError); maxRotationError = Math.Max(maxRotationError, rotationError); }
                    trace.AppendLine(FormattableString.Invariant($"{Time.time - start},{Progress},{contact},{magazine.localPosition.x},{magazine.localPosition.y},{magazine.localPosition.z},{positionError},{rotationError},{Ammo},{Reserve}"));
                    if (Reloading)
                    {
                        Assert.That(Ammo, Is.EqualTo(ammo)); Assert.That(Reserve, Is.EqualTo(reserve)); Assert.That(Fire(), Is.False);
                        if (Time.time - start < Duration - .0001f) Assert.That(Call(weapons, "CompleteReload"), Is.EqualTo(false));
                        if (stage < stages.Length && Progress >= stages[stage]) { Export("stage-" + stages[stage].ToString("F2", System.Globalization.CultureInfo.InvariantCulture)); stage++; }
                    }
                }
                Assert.That(Reloading, Is.False); Assert.That(Time.time - start, Is.EqualTo(Duration).Within(1f / 120f + .001f));
                Assert.That(Ammo, Is.EqualTo(20)); Assert.That(Reserve, Is.EqualTo(reserve - 1)); Assert.That(Ammo + Reserve, Is.EqualTo(ammo + reserve));
                Assert.That(Call(weapons, "CompleteReload"), Is.EqualTo(false)); AssertRest();
                Assert.That(maxPositionError, Is.LessThan(.0002f)); Assert.That(maxRotationError, Is.LessThan(.1));
            }
            finally
            {
                File.WriteAllText(Path.Combine(Evidence, "normal-reload.csv"), trace.ToString());
                File.WriteAllText(Path.Combine(Evidence, "held-alignment.txt"), "Max held wrist target position error (m)=" + maxPositionError + ", rotation error (degrees)=" + maxRotationError + ".\n");
            }
        }

        private IEnumerator EquipTestDefinition(int capacity, int reserve, float duration)
        {
            temporaryDefinition = ScriptableObject.CreateInstance(T("WeaponDefinition")); var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var v in new[] { ("magazineCapacity", capacity), ("startingReserve", reserve) }) temporaryDefinition.GetType().GetField(v.Item1, flags).SetValue(temporaryDefinition, v.Item2);
            temporaryDefinition.GetType().GetField("reloadDuration", flags).SetValue(temporaryDefinition, duration);
            temporaryDefinition.GetType().GetField("viewmodelPrefab", flags).SetValue(temporaryDefinition, Get(Get(State, "Definition"), "ViewmodelPrefab"));
            Assert.That(Call(weapons, "Equip", temporaryDefinition), Is.EqualTo(true)); yield return null; yield return null; yield return PlayerWeaponTestActions.WaitForReady(player); Resolve();
        }
        private IEnumerator CompleteAndCheck(int expectedAmmo, int expectedReserve)
        {
            int ammo = Ammo, reserve = Reserve; float start = Time.time;
            Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true)); int changes = 0, last = ammo;
            for (int i = 0; i < 400 && Reloading; i++)
            {
                yield return null; AssertCameraAndDominant();
                if (Ammo != last) { changes++; last = Ammo; }
                if (Reloading) { Assert.That(Ammo, Is.EqualTo(ammo)); Assert.That(Reserve, Is.EqualTo(reserve)); Assert.That(Fire(), Is.False); }
            }
            Assert.That(Reloading, Is.False); Assert.That(Time.time - start, Is.EqualTo(Duration).Within(.01f));
            Assert.That(changes, Is.EqualTo(1)); Assert.That(Ammo, Is.EqualTo(expectedAmmo)); Assert.That(Reserve, Is.EqualTo(expectedReserve));
            Assert.That(Ammo + Reserve, Is.EqualTo(ammo + reserve)); Assert.That(Call(weapons, "CompleteReload"), Is.EqualTo(false)); AssertRest();
        }
        [UnityTest] public IEnumerator EmptyReload_UsesConfiguredShortDurationAndTransfersExactlyOnce()
        {
            yield return EquipTestDefinition(2, 5, .8f); yield return FireReady(); yield return FireReady();
            Assert.That(Ammo, Is.EqualTo(0)); yield return CompleteAndCheck(2, 3);
        }
        [UnityTest] public IEnumerator PartialReserve_UsesConfiguredLongDurationWithoutInventingRounds()
        {
            yield return EquipTestDefinition(5, 1, 2.4f); for (int i = 0; i < 4; i++) yield return FireReady();
            Assert.That(Ammo, Is.EqualTo(1)); yield return CompleteAndCheck(2, 0); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(false));
        }

        [UnityTest] public IEnumerator CancellationAtMultipleStages_RestoresMagazineAndExactGripWithoutTransferringAmmo()
        {
            foreach (float progress in new[] { .20f, .45f, .70f, .90f })
            {
                yield return FireReady(); int ammo = Ammo, reserve = Reserve;
                var baked = new Mesh(); skin.BakeMesh(baked, true); var initial = baked.vertices;
                try
                {
                    Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
                    while (Progress < progress) yield return null;
                    Assert.That(Call(weapons, "CancelReload"), Is.EqualTo(true)); yield return null; AssertRest();
                    skin.BakeMesh(baked, true); Assert.That(initial.Zip(baked.vertices, Vector3.Distance).Max(), Is.LessThan(.00005f));
                    Assert.That(Ammo, Is.EqualTo(ammo)); Assert.That(Reserve, Is.EqualTo(reserve)); Assert.That(Call(weapons, "CompleteReload"), Is.EqualTo(false));
                }
                finally { Object.Destroy(baked); }
            }
            Assert.That(Fire(), Is.True);
        }

        [UnityTest] public IEnumerator DeathDuringExtraction_CancelsReloadResetsHiddenPresentationAndGrantsNoAmmo()
        {
            yield return FireReady(); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            while (Progress < .45f) yield return null;
            int ammo = Ammo, reserve = Reserve; Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            yield return null; yield return null;
            Assert.That(Reloading, Is.False); Assert.That(Ammo, Is.EqualTo(ammo)); Assert.That(Reserve, Is.EqualTo(reserve));
            Assert.That(model.gameObject.activeInHierarchy, Is.False); Assert.That(Fire(), Is.False); AssertRest();
        }

        [UnityTest] public IEnumerator SwitchingDuringExtraction_RetainsAmmoAndCreatesAFreshSeatedMagazineOnReturn()
        {
            var original = (ScriptableObject)Get(State, "Definition"); yield return FireReady();
            Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true)); while (Progress < .45f) yield return null;
            object oldState = State; int ammo = Ammo, reserve = Reserve;
            temporaryDefinition = ScriptableObject.CreateInstance(T("WeaponDefinition")); Assert.That(Call(weapons, "Equip", temporaryDefinition), Is.EqualTo(true));
            yield return null; Assert.That(Get(oldState, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Get(oldState, "RoundsInMagazine"), Is.EqualTo(ammo)); Assert.That(Get(oldState, "ReserveRounds"), Is.EqualTo(reserve));
            Assert.That(Call(weapons, "Equip", original), Is.EqualTo(true)); yield return null; yield return null; yield return PlayerWeaponTestActions.WaitForReady(player); Resolve(); AssertRest();
            Assert.That(Ammo, Is.EqualTo(ammo)); Assert.That(Reserve, Is.EqualTo(reserve));
            Assert.That(model.GetComponentsInChildren<Transform>(true).Count(t => t.name == "FNFiveSeven_Magazine"), Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator GameViewEvidence_RecordsRealReloadStagesAndContinuousAnimation()
        {
            Time.captureDeltaTime = 1f / 60f;
            Type type = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            var view = Application.isBatchMode || type == null ? null : EditorWindow.GetWindow(type);
            if (view != null) { view.Show(); view.Focus(); }
            for (int i = 0; i < 30; i++) yield return null;
            yield return FireReady(); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            string[] names = { "access", "grasp", "extraction", "exchange", "insertion", "seating", "return" };
            float[] stages = { .14f, .28f, .40f, .55f, .72f, .84f, .94f }; int next = 0;
            for (int frame = 0; frame < 150; frame++)
            {
                yield return null; AssertCameraAndDominant();
                if (next < stages.Length && Progress >= stages[next]) { Capture(view, names[next]); next++; }
                if (frame % 2 == 0) Capture(view, "sequence/frame-" + (frame / 2).ToString("D4"));
            }
            Assert.That(next, Is.EqualTo(stages.Length)); Assert.That(Ammo, Is.EqualTo(20)); AssertRest(); Capture(view, "complete");
        }

        private static void Capture(EditorWindow view, string name)
        {
            if (view == null) return;
            for (Type type = view.GetType(); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(RenderTexture).IsAssignableFrom(field.FieldType) && field.GetValue(view) is RenderTexture rt && rt.IsCreated() && rt.width >= 320)
                    {
                        RenderTexture previous = RenderTexture.active; RenderTexture.active = rt; var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        try
                        {
                            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
                            string path = Path.Combine(Evidence, name + ".tga"); Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using var writer = new BinaryWriter(File.Create(path)); writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                            writer.Write((short)rt.width); writer.Write((short)rt.height); writer.Write((byte)24); writer.Write((byte)(SystemInfo.graphicsUVStartsAtTop ? 0x20 : 0));
                            foreach (Color32 p in texture.GetPixels32()) { writer.Write(p.b); writer.Write(p.g); writer.Write(p.r); }
                            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "Actual Unity Editor " + type.FullName + "." + field.Name + ", " + rt.width + "x" + rt.height + ", includes HUD. Top-origin=" + SystemInfo.graphicsUVStartsAtTop + ". Every second rendered frame at a 1/60-second simulation step; 30 FPS sequence.\n");
                        }
                        finally { RenderTexture.active = previous; Object.Destroy(texture); }
                        return;
                    }
        }
    }
}
#endif
