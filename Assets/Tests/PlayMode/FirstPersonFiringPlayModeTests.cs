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
    public sealed class FirstPersonFiringPlayModeTests
    {
        private PistolGripPlayModeTests fixture;
        private Component player, weapons, model, presenter, idle, movement, mount, bindings, grip, target;
        private Camera camera;
        private CharacterController controller;
        private Transform pivot, right, left, rightTarget, leftTarget;
        private SkinnedMeshRenderer skin;
        private Vector3 sourcePosition, cameraPosition, capsuleCenter;
        private Quaternion sourceRotation, cameraRotation;
        private float capsuleHeight, capsuleRadius, captureDelta;
        private WeaponEventProbe events;
        private NoiseListenerProbe noise;
        private static readonly Vector3 Origin = new Vector3(100, 0, 100);
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;
        private static string Evidence
        {
            get { string p = Environment.GetEnvironmentVariable("SHIFTFOUR_FIRING_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "ArmsFiring"); Directory.CreateDirectory(p); return p; }
        }
        private object State => Get(weapons, "ActiveWeapon");
        private int Ammo => (int)Get(State, "RoundsInMagazine");
        private int Capacity => (int)Get(Get(State, "Definition"), "MagazineCapacity");
        private float HP => (float)Get(target, "Current");
        private float Damage => PlayerWeaponTestActions.EffectiveDamage(player);
        private Renderer Flash => (Renderer)Get(presenter, "MuzzleFlash");
        private Vector3 RecoilPosition => (Vector3)Get(presenter, "RecoilPositionOffset");
        private Quaternion RecoilRotation => (Quaternion)Get(presenter, "RecoilRotationOffset");
        private double PoseTime => (double)Get(presenter, "RecoilSampleTime");
        private float Strength => (float)Get(presenter, "VisualRecoilStrength");
        private bool Fire() => (bool)Call(weapons, "TryFire");

        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime;
            fixture = new PistolGripPlayModeTests(); yield return fixture.Setup();
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            weapons = player.GetComponent(T("WeaponController")); controller = player.GetComponent<CharacterController>();
            camera = (Camera)Get(player, "ViewCamera"); mount = player.GetComponent(T("FirstPersonWeaponMount"));
            model = (Component)Get(mount, "Instance"); presenter = model.GetComponent(T("PistolPresentation"));
            idle = model.GetComponent(T("FirstPersonIdleMotion")); movement = model.GetComponent(T("FirstPersonMovementMotion"));
            bindings = model.GetComponentInChildren(T("FirstPersonArmsRig")); grip = bindings.GetComponent(T("FirstPersonPistolGrip"));
            right = (Transform)Get(bindings, "RightHand"); left = (Transform)Get(bindings, "LeftHand");
            rightTarget = (Transform)Get(grip, "RightHandTarget"); leftTarget = (Transform)Get(grip, "SupportHandTarget");
            skin = (SkinnedMeshRenderer)Get(bindings, "Skin"); pivot = (Transform)Get(presenter, "RecoilPivot");
            sourcePosition = player.transform.position; sourceRotation = player.transform.rotation;
            cameraPosition = camera.transform.localPosition; cameraRotation = camera.transform.localRotation;
            capsuleCenter = controller.center; capsuleHeight = controller.height; capsuleRadius = controller.radius;
            Box("Firing test floor", Origin - Vector3.up * .5f, new Vector3(100, 1, 100));
            Time.timeScale = 1; Time.captureDeltaTime = 1f / 120f;
            Place(Origin); yield return null; yield return null;
            var damageTarget = Box("Firing damage observer", camera.transform.position + camera.transform.forward * 10, new Vector3(20, 20, .3f));
            damageTarget.transform.rotation = sourceRotation;
            target = damageTarget.AddComponent(T("Health")); Call(target, "Configure", 10000f, false);
            events = new GameObject("Accepted firing observer").AddComponent<WeaponEventProbe>(); events.Bind(weapons, target);
            noise = new GameObject("Gunshot observer").AddComponent<NoiseListenerProbe>();
            Physics.SyncTransforms(); Call(idle, "ResetMotion"); Call(movement, "ResetMotion"); Call(presenter, "ApplyPose", true);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = captureDelta; yield return fixture.Cleanup();
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var root = new GameObject(name); root.transform.position = position;
            root.AddComponent<BoxCollider>().size = size; Physics.SyncTransforms(); return root;
        }
        private void Place(Vector3 position)
        {
            controller.enabled = false; player.transform.position = position + Vector3.up * .02f; player.transform.rotation = sourceRotation;
            controller.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 15; i++) Call(player, "MovePlayer", Vector3.zero, 1f / 60f);
            Call(player, "ApplyCameraPose"); Call(idle, "ResetMotion"); Call(movement, "ResetMotion");
        }
        private IEnumerator Step(Vector3 localDirection, int fps = 120)
        {
            Call(player, "MovePlayer", player.transform.TransformDirection(localDirection), 1f / fps);
            yield return null; AssertPresentation();
        }
        private void AssertPresentation(bool bothHands = true)
        {
            Assert.That(Vector3.Distance(camera.transform.localPosition, cameraPosition), Is.LessThan(.00002f));
            Assert.That(Degrees(Quaternion.Inverse(cameraRotation) * camera.transform.localRotation), Is.LessThan(.001));
            Assert.That(controller.center, Is.EqualTo(capsuleCenter)); Assert.That(controller.height, Is.EqualTo(capsuleHeight));
            Assert.That(controller.radius, Is.EqualTo(capsuleRadius));
            Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
            if (bothHands) Assert.That(Vector3.Distance(left.position, leftTarget.position), Is.LessThan(.0002f));
            Assert.That(RecoilPosition.magnitude, Is.LessThanOrEqualTo(.0181f));
            Assert.That(Degrees(RecoilRotation), Is.LessThanOrEqualTo(3.01));
            Assert.That(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) Assert.That(part.gameObject.layer, Is.EqualTo(2));
        }
        private void AssertComposedPose()
        {
            float weight = (float)Get(movement, "IdleWeight");
            Vector3 p = (Vector3)Field(presenter, "restPosition") + (Vector3)Call(model.GetComponent(T("FirstPersonViewmodelFraming")), "CoverPositionOffset", (float)Get(mount, "PresentationRetreat")) + RecoilPosition + (Vector3)Get(idle, "PositionOffset") * weight + (Vector3)Get(movement, "PositionOffset");
            Quaternion q = (Quaternion)Field(presenter, "restRotation") * (Quaternion)Call(model.GetComponent(T("FirstPersonViewmodelFraming")), "ObstructionRotation", (float)Get(mount, "PresentationRetreat")) * RecoilRotation *
                Quaternion.SlerpUnclamped(Quaternion.identity, (Quaternion)Get(idle, "RotationOffset"), weight) * (Quaternion)Get(movement, "RotationOffset");
            Assert.That(Vector3.Distance(pivot.localPosition, p), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(q) * pivot.localRotation), Is.LessThan(.001));
        }
        private void AssertShotRay(Vector3 origin, Vector3 direction)
        {
            var shot = Get(model, "LastShot"); var ray = (Ray)shot.GetType().GetField("Ray").GetValue(shot);
            Assert.That(ray.origin, Is.EqualTo(origin)); Assert.That(Vector3.Angle(ray.direction, direction), Is.LessThan(.001f));
            Assert.That(camera.transform.position, Is.EqualTo(origin)); Assert.That(camera.transform.forward, Is.EqualTo(direction));
        }
        private static StringBuilder Trace() => new StringBuilder("frame,sample_time,back_m,lift_m,muzzle_up_degrees,roll_degrees,flash,ammo,accepted,damage_events,target_hp,right_error_m,left_error_m\n");
        private void Append(StringBuilder trace, int frame)
        {
            var q = RecoilRotation.eulerAngles;
            trace.AppendLine(FormattableString.Invariant($"{frame},{PoseTime},{RecoilPosition.z},{RecoilPosition.y},{-Mathf.DeltaAngle(0, q.x)},{Mathf.DeltaAngle(0, q.z)},{Flash.enabled},{Ammo},{events.Shots},{events.Damages},{HP},{Vector3.Distance(right.position, rightTarget.position)},{Vector3.Distance(left.position, leftTarget.position)}"));
        }

        [UnityTest] public IEnumerator SingleShot_UsesOneAcceptedEventKeepsBothHandsAndFlashInSyncThenRecoversToLiveIdle()
        {
            var muzzle = (Transform)Get(presenter, "MuzzlePoint");
            Vector3 muzzleStart = camera.transform.InverseTransformPoint(muzzle.position), eye = camera.transform.position, aim = camera.transform.forward;
            var offsets = skin.bones.Select(b => b.localPosition).ToArray(); var scales = skin.bones.Select(b => b.localScale).ToArray();
            var fingers = skin.bones.Where(b => b.name.StartsWith("f_") || b.name.StartsWith("thumb.") || b.name.StartsWith("palm_")).ToArray();
            var fingerRotations = fingers.Select(b => b.localRotation).ToArray();
            var baked = new Mesh(); skin.BakeMesh(baked, true); Vector3[] initialSkin = baked.vertices;
            double shotTime = Time.timeAsDouble; Assert.That(Fire(), Is.True); AssertShotRay(eye, aim);
            Assert.That(RecoilPosition.z, Is.EqualTo(-.012f).Within(.000001f));
            Assert.That(camera.transform.InverseTransformPoint(muzzle.position).y - muzzleStart.y, Is.GreaterThan(.001f));
            Assert.That(Flash.enabled, Is.True); var first = RecoilPosition; Assert.That(Fire(), Is.False);
            Assert.That(RecoilPosition, Is.EqualTo(first)); Assert.That(Ammo, Is.EqualTo(Capacity - 1));
            Assert.That(events.Shots, Is.EqualTo(1)); Assert.That(events.Damages, Is.EqualTo(1));
            Assert.That(HP, Is.EqualTo(10000f - Damage)); Assert.That(noise.Shots, Is.EqualTo(1));
            var trace = Trace(); float maximumSkin = 0, peakPitch = 0;
            try
            {
                for (int frame = 0; frame < 130; frame++)
                {
                    yield return Step(Vector3.zero); AssertComposedPose(); Append(trace, frame);
                    double elapsed = PoseTime - shotTime;
                    Assert.That(Flash.enabled, Is.EqualTo(elapsed < .035 - .000001));
                    peakPitch = Mathf.Max(peakPitch, -Mathf.DeltaAngle(0, RecoilRotation.eulerAngles.x));
                    CollectionAssert.AreEqual(offsets, skin.bones.Select(b => b.localPosition).ToArray());
                    CollectionAssert.AreEqual(scales, skin.bones.Select(b => b.localScale).ToArray());
                    CollectionAssert.AreEqual(fingerRotations, fingers.Select(b => b.localRotation).ToArray());
                    if (frame % 15 == 0)
                    {
                        skin.BakeMesh(baked, true); maximumSkin = Mathf.Max(maximumSkin, initialSkin.Zip(baked.vertices, Vector3.Distance).Max());
                    }
                    Assert.That(camera.transform.position, Is.EqualTo(eye));
                }
                Assert.That(peakPitch, Is.InRange(1.9f, 2.1f)); Assert.That(Strength, Is.EqualTo(0f));
                Assert.That(maximumSkin, Is.LessThan(.0002f)); Assert.That(events.Shots, Is.EqualTo(1)); Assert.That(events.Damages, Is.EqualTo(1));
                File.WriteAllText(Path.Combine(Evidence, "single-shot-summary.txt"), "One accepted shot, one " + Damage + "-damage event, one gunshot event and one round consumed. Immediate rear travel 12 mm; muzzle peak " + peakPitch + " degrees. Max local skin variation (m)=" + maximumSkin + ". Flash follows original 35 ms timer; camera unchanged.\n");
            }
            finally { Object.Destroy(baked); File.WriteAllText(Path.Combine(Evidence, "single-shot.csv"), trace.ToString()); }
        }

        [UnityTest] public IEnumerator RepeatedFiring_RejectsCooldownAttemptsWithoutDuplicateDamageOrPoseAccumulation()
        {
            int accepted = 0; float lastAccepted = -100; var trace = Trace();
            for (int frame = 0; frame < 250; frame++)
            {
                Vector3 origin = camera.transform.position, aim = camera.transform.forward;
                if (Fire())
                {
                    Assert.That(Time.time - lastAccepted, Is.GreaterThanOrEqualTo(.4f - .00001f));
                    lastAccepted = Time.time; accepted++; AssertShotRay(origin, aim);
                }
                Assert.That(events.Shots, Is.EqualTo(accepted)); Assert.That(events.Damages, Is.EqualTo(accepted));
                Assert.That(noise.Shots, Is.EqualTo(accepted)); Assert.That(Ammo, Is.EqualTo(Capacity - accepted));
                Assert.That(HP, Is.EqualTo(10000f - Damage * accepted).Within(.01f));
                yield return Step(Vector3.zero); AssertComposedPose(); Append(trace, frame);
            }
            Assert.That(accepted, Is.GreaterThanOrEqualTo(5));
            for (int i = 0; i < 120; i++) { yield return Step(Vector3.zero); AssertComposedPose(); }
            Assert.That(Strength, Is.EqualTo(0f)); Assert.That(Flash.enabled, Is.False);
            File.WriteAllText(Path.Combine(Evidence, "repeated-firing.csv"), trace.ToString());
        }

        [UnityTest] public IEnumerator EmptyMagazine_AutoReloadsWithoutExtraShotsDuringReload()
        {
            int capacity = Capacity;
            for (int frame = 0; frame < 700 && events.Shots < capacity; frame++) { Fire(); yield return Step(Vector3.zero); }
            Assert.That(Ammo, Is.EqualTo(0)); Assert.That(events.Shots, Is.EqualTo(capacity)); Assert.That(events.Damages, Is.EqualTo(capacity));
            for (int frame = 0; frame < 130; frame++) yield return Step(Vector3.zero);
            for (int frame = 0; frame < 40; frame++)
            {
                Assert.That(Fire(), Is.False); Assert.That(Strength, Is.EqualTo(0f)); Assert.That(Flash.enabled, Is.False);
                yield return Step(Vector3.zero); AssertComposedPose();
            }
            Assert.That(noise.Shots, Is.EqualTo(capacity)); Assert.That(events.Damages, Is.EqualTo(capacity)); Assert.That(HP, Is.EqualTo(10000f - capacity * Damage).Within(.01f));
            for (int frame = 0; frame < 240 && Ammo == 0; frame++) yield return Step(Vector3.zero);
            Assert.That(Ammo, Is.EqualTo(capacity)); Assert.That(Fire(), Is.True);
            File.WriteAllText(Path.Combine(Evidence, "empty-magazine.txt"), capacity + " accepted shots exhaust the magazine and start automatic reload. Firing during reload causes no extra recoil, flash, shot event or damage.\n");
        }

        [UnityTest] public IEnumerator FireWhileWalking_PreservesActualMovementCameraRayAndSharedGrip()
        {
            for (int i = 0; i < 45; i++) yield return Step(Vector3.forward);
            Assert.That(Get(movement, "WalkingWeight"), Is.GreaterThan(.9f));
            var trace = Trace();
            for (int frame = 0; frame < 70; frame++)
            {
                if (frame == 0 || frame == 50)
                {
                    Vector3 origin = camera.transform.position, aim = camera.transform.forward;
                    Assert.That(Fire(), Is.True); AssertShotRay(origin, aim);
                }
                yield return Step(Vector3.forward); AssertComposedPose(); Append(trace, frame);
            }
            Assert.That(events.Shots, Is.EqualTo(2)); Assert.That(events.Damages, Is.EqualTo(2)); Assert.That(noise.Shots, Is.EqualTo(2));
            File.WriteAllText(Path.Combine(Evidence, "walking-fire.csv"), trace.ToString());
        }

        [UnityTest] public IEnumerator FireImmediatelyAfterTakeoffAndLanding_PreservesJumpArcAimAndBothHands()
        {
            float start = player.transform.position.y, peak = start; Assert.That(Call(player, "TryJump"), Is.EqualTo(true));
            yield return Step(Vector3.zero); Assert.That(controller.isGrounded, Is.False);
            Vector3 eye = camera.transform.position, aim = camera.transform.forward;
            Assert.That(Fire(), Is.True); AssertShotRay(eye, aim); AssertPresentation();
            bool landed = false; var trace = Trace();
            for (int frame = 0; frame < 130; frame++)
            {
                yield return Step(Vector3.zero); AssertComposedPose(); Append(trace, frame);
                peak = Mathf.Max(peak, player.transform.position.y);
                if (controller.isGrounded) { landed = true; break; }
            }
            Assert.That(landed, Is.True); Assert.That(peak - start, Is.EqualTo(1.1f).Within(.04f));
            eye = camera.transform.position; aim = camera.transform.forward;
            Assert.That(Fire(), Is.True); AssertShotRay(eye, aim);
            for (int i = 0; i < 130; i++) { yield return Step(Vector3.zero); AssertComposedPose(); }
            Assert.That(events.Shots, Is.EqualTo(2)); Assert.That(events.Damages, Is.EqualTo(2)); Assert.That(Strength, Is.EqualTo(0f));
            File.WriteAllText(Path.Combine(Evidence, "jump-fire.csv"), trace.ToString());
        }

        [UnityTest] public IEnumerator FireNearWall_PreservesRetreatAndHitsWallWithoutMovingCameraOrHandsAwayFromGrip()
        {
            var wall = Box("Firing cover wall", Origin + player.transform.forward * 1.3f + Vector3.up * 1.5f, new Vector3(3, 3, .3f));
            wall.transform.rotation = sourceRotation; Physics.SyncTransforms();
            for (int i = 0; i < 100; i++) yield return Step(Vector3.forward);
            Assert.That((float)Get(mount, "PresentationRetreat"), Is.GreaterThan(.05f));
            Vector3 eye = camera.transform.position, aim = camera.transform.forward;
            Assert.That(Fire(), Is.True); AssertShotRay(eye, aim);
            var shot = Get(model, "LastShot"); var hit = (RaycastHit)shot.GetType().GetField("Hit").GetValue(shot);
            Assert.That(hit.collider.gameObject, Is.SameAs(wall));
            Vector3 rootAtShot = player.transform.position;
            var muzzle = (Transform)Get(presenter, "MuzzlePoint"); var socket = (Transform)Get(mount, "Socket");
            for (int i = 0; i < 120; i++)
            {
                yield return Step(Vector3.forward); AssertComposedPose();
                Assert.That(socket.localPosition.z, Is.EqualTo(-(float)Get(mount, "PresentationRetreat")).Within(.000001f));
                Assert.That(Vector3.Dot(muzzle.position - wall.transform.position, wall.transform.forward), Is.LessThan(-.15f));
                // Contact corrections can move the CharacterController by a few micrometres.
                // The camera may follow that real root motion, but recoil must add no camera offset.
                Assert.That(Vector3.Distance(camera.transform.position, eye + player.transform.position - rootAtShot), Is.LessThan(.00002f));
            }
            Assert.That(events.Shots, Is.EqualTo(1)); Assert.That(events.Damages, Is.EqualTo(0)); Assert.That(HP, Is.EqualTo(10000f));
            File.WriteAllText(Path.Combine(Evidence, "near-wall.txt"), "Accepted shot hits cover, no target damage. Existing socket retreat retained; muzzle stays behind wall face throughout recoil/recovery, camera and both grip targets retained.\n");
        }

        [UnityTest] public IEnumerator ReloadAndPresentationDisable_ClearRecoilAndDoNotDuplicateShotSubscriptions()
        {
            Assert.That(Fire(), Is.True); ((Behaviour)presenter).enabled = false;
            Assert.That(Strength, Is.EqualTo(0f)); Assert.That(Flash.enabled, Is.False);
            ((Behaviour)presenter).enabled = true;
            for (int i = 0; i < 60; i++) yield return Step(Vector3.zero);
            Assert.That(Fire(), Is.True); Assert.That(RecoilPosition.z, Is.EqualTo(-.012f).Within(.000001f));
            Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            Assert.That(Strength, Is.EqualTo(0f)); Assert.That(Flash.enabled, Is.False); Assert.That(Fire(), Is.False);
            Assert.That(Call(weapons, "CancelReload"), Is.EqualTo(true));
            for (int i = 0; i < 120; i++) { yield return Step(Vector3.zero); AssertComposedPose(); }
            Assert.That(events.Shots, Is.EqualTo(2)); Assert.That(events.Damages, Is.EqualTo(2)); Assert.That(noise.Shots, Is.EqualTo(2));
            Assert.That(Ammo, Is.EqualTo(6)); Assert.That(Strength, Is.EqualTo(0f));
        }

        [UnityTest] public IEnumerator RenderedThirtySixtyAndOneTwentyFps_MatchAtActualRecoilSampleTimes()
        {
            Vector3[] expectedPosition = null; Quaternion[] expectedRotation = null; var summary = new StringBuilder();
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Time.captureDeltaTime = 1f / fps; yield return null; yield return null;
                for (int i = 0; i < fps; i++) yield return Step(Vector3.zero, fps);
                double shotTime = Time.timeAsDouble; Assert.That(Fire(), Is.True);
                var positions = new Vector3[5]; var rotations = new Quaternion[5]; int sample = 0;
                for (int frame = 0; frame < fps + 3; frame++)
                {
                    yield return Step(Vector3.zero, fps); AssertComposedPose();
                    double elapsed = PoseTime - shotTime;
                    if (sample < 5 && elapsed >= (sample + 1) * .1 - .000001)
                    {
                        Assert.That(elapsed, Is.EqualTo((sample + 1) * .1).Within(.000001));
                        positions[sample] = RecoilPosition; rotations[sample] = RecoilRotation; sample++;
                    }
                }
                Assert.That(sample, Is.EqualTo(5)); Assert.That(Strength, Is.EqualTo(0f));
                if (expectedPosition != null)
                    for (int i = 0; i < sample; i++)
                    {
                        Assert.That(Vector3.Distance(positions[i], expectedPosition[i]), Is.LessThan(.0000002f));
                        Assert.That(Degrees(Quaternion.Inverse(rotations[i]) * expectedRotation[i]), Is.LessThan(.001));
                    }
                expectedPosition = positions; expectedRotation = rotations;
                summary.AppendLine(fps + " FPS simulation: five matching recoil samples at 0.1–0.5 s; no camera/grip drift; fully settled after one second.");
            }
            File.WriteAllText(Path.Combine(Evidence, "rendered-rates.txt"), summary.ToString());
        }

        [UnityTest] public IEnumerator GameViewEvidence_RecordsActualSingleShotAndRepeatedFiringWithLiveMuzzleFlash()
        {
            Time.captureDeltaTime = 1f / 60f; Place(sourcePosition); yield return null; yield return null;
            Type viewType = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            var view = Application.isBatchMode || viewType == null ? null : EditorWindow.GetWindow(viewType);
            if (view != null) { view.Show(); view.Focus(); }
            for (int i = 0; i < 30; i++) yield return Step(Vector3.zero, 60);
            Capture(view, "idle"); var trace = Trace();
            for (int frame = 0; frame < 150; frame++)
            {
                if (frame == 0 || frame == 30 || frame == 56 || frame == 82) Assert.That(Fire(), Is.True);
                yield return Step(Vector3.zero, 60); AssertComposedPose(); Append(trace, frame);
                if (frame % 2 == 0) Capture(view, "sequence/frame-" + (frame / 2).ToString("D4"));
                if (frame == 0) Capture(view, "single-impulse");
                if (frame == 2) Capture(view, "single-muzzle-rise");
                if (frame == 8) Capture(view, "single-recovery");
                if (frame == 48) Capture(view, "repeated-muzzle-rise");
            }
            Capture(view, "settled"); Assert.That(events.Shots, Is.EqualTo(4)); Assert.That(Ammo, Is.EqualTo(4));
            Assert.That(Strength, Is.EqualTo(0f)); Assert.That(Flash.enabled, Is.False);
            File.WriteAllText(Path.Combine(Evidence, "visual-sequence.csv"), trace.ToString());
        }

        private static void Capture(EditorWindow view, string name)
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
                            string path = Path.Combine(Evidence, name + ".tga"); Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using var writer = new BinaryWriter(File.Create(path));
                            writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); writer.Write((short)rt.width); writer.Write((short)rt.height);
                            writer.Write((byte)24); writer.Write((byte)(SystemInfo.graphicsUVStartsAtTop ? 0x20 : 0));
                            foreach (Color32 p in image.GetPixels32()) { writer.Write(p.b); writer.Write(p.g); writer.Write(p.r); }
                            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "Actual Unity Editor " + type.FullName + "." + field.Name + ", " + rt.width + "x" + rt.height + ", includes HUD. Top-origin=" + SystemInfo.graphicsUVStartsAtTop + ". Sequence records every second rendered frame at a 1/60 s simulation step (30 FPS playback). No interpolated frames.\n");
                        }
                        finally { RenderTexture.active = previous; Object.Destroy(image); }
                        return;
                    }
        }
    }
}
#endif
