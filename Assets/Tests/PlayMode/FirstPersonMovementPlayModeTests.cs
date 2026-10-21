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
    public sealed class FirstPersonMovementPlayModeTests
    {
        private PistolGripPlayModeTests fixture;
        private Component player, weapons, model, presenter, motion, idle, mount, rig, grip;
        private CharacterController controller;
        private Camera camera;
        private Transform pivot, right, left, rightTarget, leftTarget;
        private Vector3 viewPosition, capsuleCenter, sourcePosition;
        private Quaternion viewRotation, sourceRotation;
        private float capsuleHeight, capsuleRadius, captureDelta;
        private static readonly Vector3 Origin = new Vector3(100, 0, 100);
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;
        private static string Evidence
        {
            get { string p = Environment.GetEnvironmentVariable("SHIFTFOUR_MOVEMENT_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "ArmsMovement"); Directory.CreateDirectory(p); return p; }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            captureDelta = Time.captureDeltaTime;
            fixture = new PistolGripPlayModeTests(); yield return fixture.Setup();
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component;
            weapons = player.GetComponent(T("WeaponController")); controller = player.GetComponent<CharacterController>();
            camera = (Camera)Get(player, "ViewCamera"); mount = player.GetComponent(T("FirstPersonWeaponMount"));
            model = (Component)Get(mount, "Instance"); presenter = model.GetComponent(T("PistolPresentation"));
            motion = model.GetComponent(T("FirstPersonMovementMotion")); idle = model.GetComponent(T("FirstPersonIdleMotion"));
            Assert.That(motion, Is.Not.Null);
            rig = model.GetComponentInChildren(T("FirstPersonArmsRig")); grip = rig.GetComponent(T("FirstPersonPistolGrip"));
            right = (Transform)Get(rig, "RightHand"); left = (Transform)Get(rig, "LeftHand");
            rightTarget = (Transform)Get(grip, "RightHandTarget"); leftTarget = (Transform)Get(grip, "SupportHandTarget");
            pivot = (Transform)Get(presenter, "RecoilPivot");
            capsuleCenter = controller.center; capsuleHeight = controller.height; capsuleRadius = controller.radius;
            sourcePosition = player.transform.position; sourceRotation = player.transform.rotation;
            viewPosition = camera.transform.localPosition; viewRotation = camera.transform.localRotation;
            // Test-only isolated collision floor; use the real MainLab player, controller and viewmodel.
            Box("Movement test floor", Origin - Vector3.up * .5f, new Vector3(100, 1, 100));
            Time.timeScale = 1; Time.captureDeltaTime = 1f / 60f;
            Place(Origin); yield return null; yield return null;
            Call(motion, "ResetMotion"); Call(idle, "ResetMotion"); Call(presenter, "ApplyPose", true);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = captureDelta;
            yield return fixture.Cleanup();
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var root = new GameObject(name); root.transform.position = position;
            root.AddComponent<BoxCollider>().size = size; Physics.SyncTransforms(); return root;
        }

        private void Place(Vector3 position)
        {
            controller.enabled = false; player.transform.position = position + Vector3.up * .02f;
            player.transform.rotation = sourceRotation; controller.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 15; i++) Call(player, "MovePlayer", Vector3.zero, 1f / 60f);
            Call(player, "ApplyCameraPose"); Call(motion, "ResetMotion"); Call(idle, "ResetMotion");
        }

        private IEnumerator Step(Vector3 localDirection, float dt = 1f / 60f)
        {
            Call(player, "MovePlayer", player.transform.TransformDirection(localDirection), dt);
            yield return null;
            AssertStable();
        }

        private void AssertStable(bool bothHands = true)
        {
            Assert.That(Vector3.Distance(camera.transform.localPosition, viewPosition), Is.LessThan(.00002f));
            Assert.That(Degrees(Quaternion.Inverse(viewRotation) * camera.transform.localRotation), Is.LessThan(.001));
            Assert.That(controller.center, Is.EqualTo(capsuleCenter)); Assert.That(controller.height, Is.EqualTo(capsuleHeight));
            Assert.That(controller.radius, Is.EqualTo(capsuleRadius));
            Assert.That(Vector3.Distance(right.position, rightTarget.position), Is.LessThan(.0002f));
            if (bothHands) Assert.That(Vector3.Distance(left.position, leftTarget.position), Is.LessThan(.0002f));
            Assert.That(((Vector3)Get(motion, "PositionOffset")).magnitude, Is.LessThan(.012f));
            Assert.That(Degrees((Quaternion)Get(motion, "RotationOffset")), Is.LessThan(2));
            Assert.That(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
        }

        private void AssertComposedPose()
        {
            float weight = (float)Get(motion, "IdleWeight");
            Vector3 p = (Vector3)Field(presenter, "restPosition") + (Vector3)Call(model.GetComponent(T("FirstPersonViewmodelFraming")), "CoverPositionOffset", (float)Get(mount, "PresentationRetreat")) + (Vector3)Get(idle, "PositionOffset") * weight + (Vector3)Get(motion, "PositionOffset");
            Quaternion q = (Quaternion)Field(presenter, "restRotation") * (Quaternion)Call(model.GetComponent(T("FirstPersonViewmodelFraming")), "ObstructionRotation", (float)Get(mount, "PresentationRetreat")) *
                Quaternion.SlerpUnclamped(Quaternion.identity, (Quaternion)Get(idle, "RotationOffset"), weight) * (Quaternion)Get(motion, "RotationOffset");
            Assert.That(Vector3.Distance(pivot.localPosition, p), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(q) * pivot.localRotation), Is.LessThan(.001));
        }

        [UnityTest] public IEnumerator ForwardBackwardAndBothStrafes_UseActualTravelAndPreserveGripCameraAndCapsule()
        {
            var skin = (SkinnedMeshRenderer)Get(rig, "Skin");
            var boneOffsets = skin.bones.Select(b => b.localPosition).ToArray();
            var boneScales = skin.bones.Select(b => b.localScale).ToArray();
            var mesh = new Mesh(); skin.BakeMesh(mesh, true); var originalSkin = mesh.vertices;
            float skinVariation = 0;
            var trace = new StringBuilder("local_x,local_z,actual_distance_m,measured_velocity_x,measured_velocity_z,walking_weight\n");
            try
            {
                foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    Vector3 start = player.transform.position;
                    for (int i = 0; i < 60; i++) { yield return Step(direction); AssertComposedPose(); }
                    float distance = Vector3.Distance(player.transform.position, start);
                    Assert.That(distance, Is.EqualTo(5f).Within(.004f));
                    Vector3 velocity = (Vector3)Get(motion, "LocalVelocity");
                    Assert.That(Vector3.Dot(velocity, direction), Is.EqualTo(5f).Within(.005f));
                    Assert.That(Get(motion, "WalkingWeight"), Is.GreaterThan(.99f));
                    CollectionAssert.AreEqual(boneOffsets, skin.bones.Select(b => b.localPosition).ToArray());
                    CollectionAssert.AreEqual(boneScales, skin.bones.Select(b => b.localScale).ToArray());
                    skin.BakeMesh(mesh, true); skinVariation = Mathf.Max(skinVariation, originalSkin.Zip(mesh.vertices, Vector3.Distance).Max());
                    Assert.That(skinVariation, Is.LessThan(.0002f), "Movement must carry the existing wrist/finger skin shape rigidly.");
                    trace.AppendLine($"{direction.x},{direction.z},{distance},{velocity.x},{velocity.z},{Get(motion, "WalkingWeight")}");
                }
            }
            finally { Object.Destroy(mesh); }
            File.WriteAllText(Path.Combine(Evidence, "walking-directions.csv"), trace.ToString());
            File.WriteAllText(Path.Combine(Evidence, "skin-stability.txt"), "Maximum local baked-skin variation while moving (m)=" + skinVariation + ". All bone local offsets/scales preserved.\n");
        }

        [UnityTest] public IEnumerator StandingAndPushingAWall_StopBobbingAndPreserveExistingWallRetreatAndAimRay()
        {
            Box("Movement blocking wall", Origin + player.transform.forward * 1.3f + Vector3.up * 1.5f, new Vector3(3, 3, .3f));
            Vector3 start = player.transform.position;
            for (int i = 0; i < 180; i++) yield return Step(Vector3.forward);
            Assert.That(Vector3.Distance(player.transform.position, start), Is.LessThan(1.1f));
            double phase = (double)Get(motion, "Phase");
            for (int i = 0; i < 90; i++)
            {
                yield return Step(Vector3.forward); AssertComposedPose();
                Assert.That(Get(motion, "Phase"), Is.EqualTo(phase));
            }
            Assert.That(Get(motion, "WalkingWeight"), Is.EqualTo(0f));
            Assert.That(((Vector3)Get(motion, "PositionOffset")).magnitude, Is.LessThan(.000001f));
            Assert.That((float)Get(mount, "PresentationRetreat"), Is.GreaterThan(.05f));
            var socket = (Transform)Get(mount, "Socket");
            Assert.That(socket.localPosition.z, Is.EqualTo(-(float)Get(mount, "PresentationRetreat")).Within(.000001f));
            for (int i = 0; i < 60; i++) yield return Step(Vector3.zero);
            Assert.That(Get(motion, "Phase"), Is.EqualTo(phase));
            Assert.That(Get(motion, "IdleWeight"), Is.EqualTo(1f));
            Vector3 eye = camera.transform.position, aim = camera.transform.forward;
            Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
            var shot = Get(model, "LastShot"); var ray = (Ray)shot.GetType().GetField("Ray").GetValue(shot);
            Assert.That(ray.origin, Is.EqualTo(eye)); Assert.That(Vector3.Angle(ray.direction, aim), Is.LessThan(.001f));
            File.WriteAllText(Path.Combine(Evidence, "wall-and-standing.txt"), "Blocked travel and standing freeze walk phase and recover neutral movement offsets. Existing socket retreat remains active. Real shot retains camera ray; hands remain excluded from colliders/raycast layers.\n");
        }

        [UnityTest] public IEnumerator StandingJump_HasOneTakeoffStableAirHoldAndOneLandingThenReturnsToIdle()
        {
            Vector3 start = player.transform.position; float peak = start.y, minimum = 0; bool inAir = false, landed = false;
            double phase = (double)Get(motion, "Phase");
            Assert.That(Call(player, "TryJump"), Is.EqualTo(true));
            var trace = TraceHeader();
            for (int i = 0; i < 150; i++)
            {
                yield return Step(Vector3.zero); AssertComposedPose();
                peak = Mathf.Max(peak, player.transform.position.y); inAir |= !controller.isGrounded;
                landed |= inAir && controller.isGrounded;
                minimum = Mathf.Min(minimum, ((Vector3)Get(motion, "PositionOffset")).y);
                Assert.That(Get(motion, "Phase"), Is.EqualTo(phase));
                Assert.That(Get(motion, "WalkingWeight"), Is.EqualTo(0f));
                if (i == 18) Assert.That(Get(motion, "AirborneWeight"), Is.GreaterThan(.95f));
                AppendTrace(trace, i, 60);
            }
            Assert.That(landed, Is.True); Assert.That(peak - start.y, Is.EqualTo(1.1f).Within(.04f));
            Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(.04f));
            Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(1)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(1));
            Assert.That(minimum, Is.LessThan(-.003f)); Assert.That(((Vector3)Get(motion, "PositionOffset")).magnitude, Is.LessThan(.000001f));
            Assert.That(Get(motion, "IdleWeight"), Is.EqualTo(1f));
            File.WriteAllText(Path.Combine(Evidence, "standing-jump.csv"), trace.ToString());
        }

        [UnityTest] public IEnumerator MovingJump_AtThirtySixtyAndOneTwentyFps_PreservesActualPhysicsAndComposedPose()
        {
            var summary = new StringBuilder("FPS,apex_m,air_seconds,air_horizontal_distance_m,takeoffs,landings\n");
            foreach (int fps in new[] { 30, 60, 120 })
            {
                float dt = 1f / fps; Time.captureDeltaTime = dt; Place(Origin);
                yield return null; yield return null; Call(motion, "ResetMotion");
                for (int i = 0; i < fps; i++) yield return Step(Vector3.forward, dt);
                Vector3 launch = player.transform.position; float peak = launch.y, airSeconds = 0; bool landed = false;
                Assert.That(Call(player, "TryJump"), Is.EqualTo(true));
                var trace = TraceHeader();
                for (int i = 0; i < fps * 2; i++)
                {
                    yield return Step(Vector3.forward, dt); AssertComposedPose(); airSeconds += dt;
                    peak = Mathf.Max(peak, player.transform.position.y); AppendTrace(trace, i, fps);
                    if (i > 2 && controller.isGrounded) { landed = true; break; }
                }
                Assert.That(landed, Is.True); Assert.That(peak - launch.y, Is.EqualTo(1.1f).Within(.05f));
                Assert.That(airSeconds, Is.InRange(.60f, .76f));
                float distance = Vector3.ProjectOnPlane(player.transform.position - launch, Vector3.up).magnitude;
                Assert.That(distance, Is.EqualTo(5 * airSeconds).Within(.005f));
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(1)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(1));
                summary.AppendLine($"{fps},{peak - launch.y},{airSeconds},{distance},{Get(motion, "TakeoffCount")},{Get(motion, "LandingCount")}");
                for (int i = 0; i < fps * 2; i++) { yield return Step(Vector3.zero, dt); AssertComposedPose(); }
                Assert.That(((Vector3)Get(motion, "PositionOffset")).magnitude, Is.LessThan(.000001f));
                File.WriteAllText(Path.Combine(Evidence, "moving-jump-" + fps + ".csv"), trace.ToString());
            }
            File.WriteAllText(Path.Combine(Evidence, "rendered-rates.csv"), summary.ToString());
        }

        [UnityTest] public IEnumerator LookingFiringAndReloadingWhileMoving_PreserveCameraRayDominantGripAndRecovery()
        {
            viewRotation = Quaternion.Euler(-65, 0, 0); camera.transform.localRotation = viewRotation;
            player.transform.rotation *= Quaternion.Euler(0, 90, 0);
            for (int i = 0; i < 60; i++) yield return Step(Vector3.forward);
            Vector3 eye = camera.transform.position, aim = camera.transform.forward;
            Assert.That(Call(weapons, "TryFire"), Is.EqualTo(true));
            var shot = Get(model, "LastShot"); var ray = (Ray)shot.GetType().GetField("Ray").GetValue(shot);
            Assert.That(ray.origin, Is.EqualTo(eye)); Assert.That(Vector3.Angle(ray.direction, aim), Is.LessThan(.001f));
            Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            for (int i = 0; i < 45; i++)
            {
                Call(player, "MovePlayer", player.transform.right, 1f / 60f); yield return null; AssertStable(false);
            }
            Assert.That(Call(weapons, "CancelReload"), Is.EqualTo(true));
            for (int i = 0; i < 120; i++) { yield return Step(Vector3.zero); AssertComposedPose(); }
            Assert.That(Get(motion, "WalkingWeight"), Is.EqualTo(0f));
            Assert.That(((Vector3)Get(motion, "PositionOffset")).magnitude, Is.LessThan(.000001f));
            File.WriteAllText(Path.Combine(Evidence, "activity-and-look.txt"), "Forward/strafe movement while looking up and turning: real shot ray unchanged; reload/cancel retains dominant target and restores both hands. Absolute composed pivot verified after recovery.\n");
        }

        [UnityTest] public IEnumerator BeforeTheFirstControllerMove_UnknownGroundContactDoesNotInventAirborneMotion()
        {
            // Disabling/re-enabling an existing controller retains its previous collision flags in Unity 6.
            // Create a genuinely fresh controller that has never executed Move.
            var root = new GameObject("Never-moved player"); root.SetActive(false);
            root.transform.position = Origin + Vector3.right * 12;
            var cc = root.AddComponent<CharacterController>(); cc.height = capsuleHeight; cc.center = capsuleCenter; cc.radius = capsuleRadius;
            var view = new GameObject("Never-moved camera").AddComponent<Camera>(); view.enabled = false; view.transform.SetParent(root.transform, false);
            var actor = root.AddComponent(T("PlayerController")); root.SetActive(true); ((Behaviour)actor).enabled = false;
            Call(actor, "Configure", view);
            var settings = new GameObject("Never-moved presentation"); settings.transform.SetParent(root.transform, false);
            var observer = settings.AddComponent(T("FirstPersonMovementMotion"));
            try
            {
                Assert.That(cc.isGrounded, Is.False);
                for (int i = 0; i < 30; i++)
                {
                    Call(observer, "SamplePlayer", 1d / 60, false); yield return null;
                    Assert.That(Get(observer, "PositionOffset"), Is.EqualTo(Vector3.zero));
                    Assert.That(Get(observer, "AirborneWeight"), Is.EqualTo(0f));
                    Assert.That(Get(observer, "TakeoffCount"), Is.EqualTo(0)); Assert.That(Get(observer, "LandingCount"), Is.EqualTo(0));
                }
                Call(actor, "MovePlayer", Vector3.zero, 1f / 60f); Call(observer, "SamplePlayer", 1d / 60, false);
                Assert.That(cc.isGrounded, Is.True); Assert.That(Call(actor, "TryJump"), Is.EqualTo(true));
                Call(actor, "MovePlayer", Vector3.zero, 1f / 60f); Call(observer, "SamplePlayer", 1d / 60, false);
                Assert.That(Get(observer, "TakeoffCount"), Is.EqualTo(1)); Assert.That(Get(observer, "AirborneWeight"), Is.GreaterThan(0f));
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest] public IEnumerator TeleportAndComponentReenable_ResetWithoutFalseJumpLandingOrAccumulatedOffsets()
        {
            for (int i = 0; i < 30; i++) yield return Step(Vector3.forward);
            controller.enabled = false; player.transform.position += Vector3.right * 8; controller.enabled = true;
            yield return null;
            Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(0)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(0));
            Assert.That(Get(motion, "Phase"), Is.EqualTo(0d)); Assert.That(Get(motion, "PositionOffset"), Is.EqualTo(Vector3.zero));
            ((Behaviour)motion).enabled = false; yield return null; AssertStable();
            ((Behaviour)motion).enabled = true;
            for (int i = 0; i < 30; i++) yield return Step(Vector3.zero);
            Assert.That(Get(motion, "PositionOffset"), Is.EqualTo(Vector3.zero)); AssertComposedPose();
        }

        [UnityTest] public IEnumerator GameViewEvidence_RecordsMainLabWalkingTakeoffAirLandingAndRecovery()
        {
            Place(sourcePosition); yield return null; yield return null;
            var viewType = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            var view = Application.isBatchMode || viewType == null ? null : EditorWindow.GetWindow(viewType);
            if (view != null) { view.Show(); view.Focus(); }
            for (int i = 0; i < 30; i++) yield return Step(Vector3.zero);
            Capture(view, "standing");
            for (int i = 0; i < 9; i++) yield return Step(Vector3.forward);
            Capture(view, "walking"); Assert.That(Call(player, "TryJump"), Is.EqualTo(true));
            for (int i = 0; i < 3; i++) yield return Step(Vector3.zero);
            Capture(view, "takeoff");
            for (int i = 0; i < 15; i++) yield return Step(Vector3.zero);
            Capture(view, "airborne");
            for (int i = 0; i < 60 && !controller.isGrounded; i++) yield return Step(Vector3.zero);
            Assert.That(controller.isGrounded, Is.True);
            for (int i = 0; i < 3; i++) yield return Step(Vector3.zero);
            Capture(view, "landing");
            for (int i = 0; i < 90; i++) yield return Step(Vector3.zero);
            Capture(view, "recovered"); AssertComposedPose();
        }

        private static StringBuilder TraceHeader() => new StringBuilder("frame,fps,root_y,grounded,velocity_x,velocity_y,velocity_z,walk,air,offset_x,offset_y,offset_z,right_error,left_error\n");
        private void AppendTrace(StringBuilder trace, int frame, int fps)
        {
            var v = (Vector3)Get(motion, "LocalVelocity"); var p = (Vector3)Get(motion, "PositionOffset");
            trace.AppendLine(FormattableString.Invariant($"{frame},{fps},{player.transform.position.y},{controller.isGrounded},{v.x},{v.y},{v.z},{Get(motion, "WalkingWeight")},{Get(motion, "AirborneWeight")},{p.x},{p.y},{p.z},{Vector3.Distance(right.position, rightTarget.position)},{Vector3.Distance(left.position, leftTarget.position)}"));
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
                            using var writer = new BinaryWriter(File.Create(Path.Combine(Evidence, name + ".tga")));
                            writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); writer.Write((short)rt.width); writer.Write((short)rt.height);
                            writer.Write((byte)24); writer.Write((byte)(SystemInfo.graphicsUVStartsAtTop ? 0x20 : 0));
                            foreach (Color32 p in image.GetPixels32()) { writer.Write(p.b); writer.Write(p.g); writer.Write(p.r); }
                            File.WriteAllText(Path.Combine(Evidence, "game-view-provenance.txt"), "Actual Unity Editor " + type.FullName + "." + field.Name + ", " + rt.width + "x" + rt.height + ", includes HUD. Top-origin=" + SystemInfo.graphicsUVStartsAtTop + ".\n");
                        }
                        finally { RenderTexture.active = previous; Object.Destroy(image); }
                        return;
                    }
        }
    }
}
#endif
