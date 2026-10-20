#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class PlayerJumpPlayModeTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private CursorLockMode cursor;
        private bool visible;
        private static readonly Vector3 Origin = new Vector3(500, 0, 500);
        private const float Dt = 1f / 60f;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        private static float Speed(Component player) => (float)T("PlayerController").GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
        private static bool Jump(Component player) => (bool)Call(player, "TryJump");
        private static void Move(Component player, Vector3 direction, float dt = Dt) => Call(player, "MovePlayer", direction, dt);
        private static void ResetBus() => T("SoundEvents").GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; visible = Cursor.visible; ResetBus();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Jump fixture " + Guid.NewGuid()));
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in owned) if (go != null) Object.Destroy(go);
            owned.Clear(); ResetBus();
            Scene old = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("After jump fixture " + Guid.NewGuid());
            SceneManager.SetActiveScene(empty);
            if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        private GameObject Own(string name) { var go = new GameObject(name); owned.Add(go); return go; }
        private GameObject Box(string name, Vector3 position, Vector3 size, bool trigger = false)
        {
            var go = Own(name); go.transform.position = position;
            var collider = go.AddComponent<BoxCollider>(); collider.size = size; collider.isTrigger = trigger; return go;
        }

        private Component Player(bool floor = true, float elevation = 0f)
        {
            if (floor) Box("Floor", Origin - Vector3.up * .5f, new Vector3(200, 1, 200));
            var root = Own("Jump player"); root.SetActive(false); root.transform.position = Origin + Vector3.up * elevation;
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f;
            cc.stepOffset = .3f; cc.slopeLimit = 50f; cc.skinWidth = .05f; cc.minMoveDistance = 0f;
            var camera = Own("Jump camera").AddComponent<Camera>(); camera.enabled = false;
            camera.transform.SetParent(root.transform, false);
            var player = root.AddComponent(T("PlayerController"));
            root.SetActive(true); ((Behaviour)player).enabled = false;
            Call(player, "Configure", camera); Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) Move(player, Vector3.zero);
            return player;
        }

        private static float CompleteJump(Component player, Vector3 direction, out float airborneSeconds)
        {
            var cc = player.GetComponent<CharacterController>();
            float start = player.transform.position.y, apex = start;
            Assert.That(Jump(player), Is.True);
            Assert.That(Speed(player), Is.EqualTo(Mathf.Sqrt(44f)).Within(.0001f));
            airborneSeconds = 0f;
            bool airborne = false;
            for (int i = 0; i < 180; i++)
            {
                Move(player, direction); airborneSeconds += Dt;
                apex = Mathf.Max(apex, player.transform.position.y);
                airborne |= !cc.isGrounded;
                if (airborne && cc.isGrounded) return apex - start;
            }
            Assert.Fail("Jump did not land in three seconds."); return 0f;
        }

        [UnityTest] public IEnumerator StandingJump_ReachesConfiguredHeightAndLands()
        {
            var player = Player(); Vector3 start = player.transform.position;
            float apex = CompleteJump(player, Vector3.zero, out float duration);
            Assert.That(apex, Is.EqualTo(1.1f).Within(.04f));
            Assert.That(duration, Is.InRange(.60f, .75f));
            Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(.06f));
            Assert.That(new Vector2(player.transform.position.x - start.x, player.transform.position.z - start.z).magnitude, Is.LessThan(.001f));
            Assert.That(Speed(player), Is.LessThanOrEqualTo(0f));
            Assert.That(Jump(player), Is.True, "Landing restores exactly one subsequent jump.");
            Write("standing-jump.csv", $"height_m,airborne_seconds\n{F(apex)},{F(duration)}\n");
            yield return null;
        }

        [UnityTest] public IEnumerator MovingJump_PreservesFiveMetresPerSecondAndHeight()
        {
            var player = Player(); Vector3 start = player.transform.position;
            float apex = CompleteJump(player, Vector3.forward, out float duration);
            Assert.That(apex, Is.EqualTo(1.1f).Within(.04f));
            Assert.That(player.transform.position.z - start.z, Is.EqualTo(5f * duration).Within(.025f));
            Assert.That(player.transform.position.x, Is.EqualTo(start.x).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator ConfiguredHeightAndGravity_ChangeActualArcAndLaunchSpeed()
        {
            var player = Player();
            T("PlayerController").GetField("jumpHeight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, .7f);
            T("PlayerController").GetField("gravity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 12f);
            Call(player, "OnValidate");
            float start = player.transform.position.y, peak = start, duration = 0f;
            Assert.That(Jump(player), Is.True);
            Assert.That(Speed(player), Is.EqualTo(Mathf.Sqrt(16.8f)).Within(.0001f));
            for (int i = 0; i < 120; i++)
            {
                Move(player, Vector3.zero); duration += Dt;
                peak = Mathf.Max(peak, player.transform.position.y);
                if (i > 2 && player.GetComponent<CharacterController>().isGrounded) break;
            }
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(peak - start, Is.EqualTo(.7f).Within(.035f));
            Assert.That(duration, Is.EqualTo(2f * Mathf.Sqrt(16.8f) / 12f).Within(.05f));
            yield return null;
        }

        [UnityTest] public IEnumerator RepeatedRequests_AreRejectedOnTakeoffAscentAndDescent()
        {
            var player = Player(); var cc = player.GetComponent<CharacterController>();
            Assert.That(Jump(player), Is.True);
            Assert.That(Jump(player), Is.False, "Previous Move can still report ground on the takeoff frame.");
            bool ascending = false, descending = false;
            for (int i = 0; i < 180; i++)
            {
                Move(player, Vector3.zero);
                if (cc.isGrounded) break;
                ascending |= Speed(player) > 0f; descending |= Speed(player) < 0f;
                Assert.That(Jump(player), Is.False);
            }
            Assert.That(ascending && descending, Is.True); Assert.That(cc.isGrounded, Is.True);
            Assert.That(Jump(player), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator LowCeiling_CancelsUpwardSpeedAndAllowsLanding()
        {
            var player = Player(); var cc = player.GetComponent<CharacterController>();
            Box("Low ceiling", Origin + Vector3.up * 2.25f, new Vector3(8, .4f, 8)); Physics.SyncTransforms();
            float initial = player.transform.position.y, apex = initial;
            Assert.That(Jump(player), Is.True); bool hit = false, landed = false;
            for (int i = 0; i < 90; i++)
            {
                Move(player, Vector3.zero); apex = Mathf.Max(apex, player.transform.position.y);
                if ((cc.collisionFlags & CollisionFlags.Above) != 0)
                {
                    hit = true; Assert.That(Speed(player), Is.LessThanOrEqualTo(0f));
                    Assert.That(Jump(player), Is.False);
                }
                if (hit && cc.isGrounded) { landed = true; break; }
            }
            Assert.That(hit && landed, Is.True);
            Assert.That(apex - initial, Is.InRange(.1f, .34f));
            yield return null;
        }

        [UnityTest] public IEnumerator RaisedSurface_LandsOnTopAndCanJumpAgain()
        {
            var player = Player(); var cc = player.GetComponent<CharacterController>();
            Box("Raised platform", Origin + new Vector3(0, .3f, 3.5f), new Vector3(4, .6f, 4)); Physics.SyncTransforms();
            Assert.That(Jump(player), Is.True); bool landed = false;
            for (int i = 0; i < 90; i++)
            {
                Move(player, Vector3.forward);
                if (i > 2 && cc.isGrounded) { landed = true; break; }
            }
            Assert.That(landed, Is.True);
            Assert.That(player.transform.position.y, Is.EqualTo(.6f).Within(.07f));
            Assert.That(player.transform.position.z - Origin.z, Is.GreaterThan(1.5f));
            Assert.That(Jump(player), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator AirborneAndLandingSamples_EmitNoWalkingOrCatchupNoise()
        {
            var player = Player(); var cc = player.GetComponent<CharacterController>();
            var probe = Own("Noise observer").AddComponent<NoiseListenerProbe>();
            for (int i = 0; i < 10; i++) Move(player, Vector3.forward);
            Assert.That(probe.Noises, Is.Zero); Assert.That(Jump(player), Is.True);
            for (int i = 0; i < 120; i++)
            {
                Move(player, Vector3.forward);
                Assert.That(probe.Noises, Is.Zero, "No ordinary footsteps on takeoff, flight or landing.");
                if (i > 2 && cc.isGrounded) break;
            }
            Assert.That(cc.isGrounded, Is.True);
            for (int i = 0; i < 10; i++) Move(player, Vector3.forward);
            Assert.That(probe.Noises, Is.Zero, "Airborne distance must not be retained.");
            for (int i = 0; i < 12; i++) Move(player, Vector3.forward);
            Assert.That(probe.Noises, Is.EqualTo(1)); Assert.That(probe.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator JumpAndLanding_PreserveCapsuleAndPitchRelativeCameraPose()
        {
            var player = Player(); var cc = player.GetComponent<CharacterController>();
            var camera = player.GetComponentInChildren<Camera>();
            Quaternion rotation = Quaternion.Euler(-65f, 0, 0); camera.transform.localRotation = rotation;
            Assert.That(Jump(player), Is.True);
            for (int i = 0; i < 80; i++)
            {
                Move(player, Vector3.forward); Call(player, "LateUpdate");
                Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.3f), Is.LessThan(.0001f));
                Assert.That(camera.transform.position.y - player.transform.position.y, Is.EqualTo(1.3f).Within(.0001f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(.01f));
                Assert.That(camera.nearClipPlane, Is.EqualTo(.05f).Within(.0001f));
                Assert.That(cc.height, Is.EqualTo(1.8f)); Assert.That(cc.radius, Is.EqualTo(.35f));
                Assert.That(cc.center, Is.EqualTo(Vector3.up * .9f));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator WalkOffEdgeAndTriggerSupport_CannotStartJump()
        {
            var player = Player(false, 3f);
            Box("Trigger floor", Origin + Vector3.up, new Vector3(20, 1, 20), true); Physics.SyncTransforms();
            Assert.That(Jump(player), Is.False);
            for (int i = 0; i < 20; i++) { Move(player, Vector3.forward); Assert.That(Jump(player), Is.False); }
            player.gameObject.SetActive(false);
            Box("Short ledge", Origin + Vector3.down * .5f, new Vector3(6, 1, 1));
            player = Player(false);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            for (int i = 0; i < 30; i++) Move(player, Vector3.forward);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.False);
            Assert.That(Jump(player), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator DeadPlayerAndEndedMission_RejectJump()
        {
            var dead = Player(); Call(dead.GetComponent(T("Health")), "TakeDamage", 1000f);
            Assert.That(Jump(dead), Is.False); dead.gameObject.SetActive(false);
            var player = Player(false);
            var session = Own("Ended mission").AddComponent(T("GameSession")); ((Behaviour)session).enabled = false;
            Call(session, "Finish", "Jump test mission ended");
            Assert.That(Jump(player), Is.False); Assert.That(Speed(player), Is.LessThanOrEqualTo(0f));
            yield return null;
        }

        [UnityTest] public IEnumerator FrameRatesAndHitches_PreserveApexTravelAndLanding()
        {
            Box("Shared floor", Origin - Vector3.up * .5f, new Vector3(200, 1, 200));
            float lowest = float.PositiveInfinity, highest = 0f;
            var csv = new StringBuilder("sample_hz,peak_m,first_landing_seconds,one_second_distance_m\n");
            foreach (float hz in new[] { 20f, 30f, 60f, 120f, 144f, 1f / .12f })
            {
                var player = Player(false); Vector3 start = player.transform.position;
                var cc = player.GetComponent<CharacterController>();
                Assert.That(Jump(player), Is.True);
                float elapsed = 0f, apex = 0f, landing = -1f;
                while (elapsed < 1f - .00001f)
                {
                    float dt = Mathf.Min(1f / hz, 1f - elapsed);
                    Move(player, Vector3.forward, dt); elapsed += dt;
                    apex = Mathf.Max(apex, player.transform.position.y - start.y);
                    if (cc.isGrounded && landing < 0f) landing = elapsed;
                }
                Assert.That(apex, Is.EqualTo(1.1f).Within(.045f), "Hz=" + hz);
                Assert.That(landing, Is.InRange(.60f, .80f), "Hz=" + hz);
                Assert.That(player.transform.position.z - start.z, Is.EqualTo(5f).Within(.03f), "Hz=" + hz);
                Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(.06f));
                lowest = Mathf.Min(lowest, apex); highest = Mathf.Max(highest, apex);
                csv.AppendLine($"{F(hz)},{F(apex)},{F(landing)},{F(player.transform.position.z - start.z)}");
                player.gameObject.SetActive(false);
            }
            Assert.That(highest - lowest, Is.LessThan(.04f));
            Write("frame-rates.csv", csv.ToString());
            yield return null;
        }

        [UnityTest] public IEnumerator PausedOrInvalidTime_DoesNotMoveOrConsumeLaunchVelocity()
        {
            var player = Player(); Assert.That(Jump(player), Is.True);
            Vector3 position = player.transform.position; float launch = Speed(player);
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Move(player, Vector3.forward, dt);
                Assert.That(player.transform.position, Is.EqualTo(position)); Assert.That(Speed(player), Is.EqualTo(launch));
            }
            yield return null;
        }

        private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        internal static void Write(string name, string content)
        {
            string output = Environment.GetEnvironmentVariable("SHIFTFOUR_JUMP_EVIDENCE") ?? "Logs/JumpEvidence";
            Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, name), content);
        }
    }

    public sealed class PlayerJumpMainLabPlayModeTests
    {
        private CursorLockMode cursor;
        private bool visible;
        private Component player;
        private Scene mainLab;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
        private static Component[] Find(string type) => Object.FindObjectsByType(T(type), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static bool Jump(Component player) => (bool)Call(player, "TryJump");
        private static void Move(Component player, Vector3 direction, float dt = 1f / 60f) => Call(player, "MovePlayer", direction, dt);

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; visible = Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            mainLab = SceneManager.GetActiveScene(); player = Find("PlayerController").Single();
            ((Behaviour)player).enabled = false;
            foreach (var brain in Find("DroneBrain")) ((Behaviour)brain).enabled = false;
            foreach (var motor in Find("DroneMotor")) ((Behaviour)motor).enabled = false;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After MainLab jump " + Guid.NewGuid()));
            if (old.path == "Assets/Scenes/MainLab.unity") yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        private void Place(Vector3 position)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = position; cc.enabled = true; Physics.SyncTransforms();
            T("PlayerController").GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 0f);
            for (int i = 0; i < 15; i++) Move(player, Vector3.zero);
        }

        [UnityTest] public IEnumerator LockedGates_RejectRepeatedAndDiagonalJumpsFromReachableApproaches()
        {
            var csv = new StringBuilder("gate,side,lateral_offset,minimum_signed_distance,maximum_feet_height\n");
            var trace = new StringBuilder("gate,side,offset,sample,x,y,z,signed_distance,lateral,grounded\n");
            var violations = new List<string>();
            var gates = Find("LabDoor"); Assert.That(gates.Length, Is.EqualTo(4));
            foreach (var gate in gates)
            {
                var barrier = gate.GetComponent<BoxCollider>();
                Vector3 normal = gate.transform.forward; normal.y = 0f; normal.Normalize();
                Vector3 tangent = gate.transform.right; tangent.y = 0f; tangent.Normalize();
                float halfWidth = barrier.size.x * Mathf.Abs(gate.transform.lossyScale.x) * .5f;
                // Gate 3 faces out of the authored level. Its exterior corners have no floor
                // and cannot be reached while locked; both sides of all interior gates are tested.
                int id = (int)gate.GetType().GetProperty("GateId").GetValue(gate);
                foreach (int side in id == 3 ? new[] { -1 } : new[] { -1, 1 })
                foreach (float offset in new[] { 0f, halfWidth - .4f, -halfWidth + .4f, halfWidth + .2f, -halfWidth - .2f })
                {
                    Vector3 start = barrier.bounds.center + normal * (side * 1.7f) + tangent * offset; start.y = .03f;
                    Place(start);
                    Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True, gate.name + " start must have floor support");
                    float closest = float.PositiveInfinity, peak = player.transform.position.y;
                    for (int i = 0; i < 150; i++)
                    {
                        Jump(player);
                        Move(player, (-normal * side + tangent * Mathf.Sign(offset) * .15f).normalized);
                        float signed = Vector3.Dot(player.transform.position - barrier.bounds.center, normal) * side;
                        closest = Mathf.Min(closest, signed); peak = Mathf.Max(peak, player.transform.position.y);
                        trace.AppendLine($"{gate.name},{side},{offset},{i},{player.transform.position.x},{player.transform.position.y},{player.transform.position.z},{signed},{Vector3.Dot(player.transform.position - barrier.bounds.center, tangent)},{player.GetComponent<CharacterController>().isGrounded}");
                    }
                    Assert.That(barrier.enabled, Is.True);
                    Assert.That((bool)gate.GetType().GetProperty("IsOpen").GetValue(gate), Is.False);
                    csv.AppendLine($"{gate.name},{side},{offset},{closest},{peak}");
                    if (closest <= .05f) violations.Add(gate.name + " side=" + side + " offset=" + offset + " closest=" + closest);
                }
            }
            PlayerJumpPlayModeTests.Write("mainlab-gate-jumps.csv", csv.ToString());
            PlayerJumpPlayModeTests.Write("mainlab-gate-trace.csv", trace.ToString());
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
            yield return null;
        }

        [UnityTest] public IEnumerator MainLabWallsAndCeiling_RemainBeyondJumpFromFloor()
        {
            var walls = mainLab.GetRootGameObjects().Single(g => g.name.StartsWith("Fixed walls")).GetComponentsInChildren<BoxCollider>();
            Assert.That(walls.Length, Is.EqualTo(12));
            Place(new Vector3(-15, .03f, -15));
            Assert.That(Jump(player), Is.True);
            float top = 0f;
            for (int i = 0; i < 80; i++)
            {
                Move(player, Vector3.zero);
                var cc = player.GetComponent<CharacterController>();
                top = Mathf.Max(top, player.transform.position.y + cc.center.y + cc.height * .5f);
            }
            foreach (var wall in walls) Assert.That(wall.bounds.max.y, Is.GreaterThan(top + .5f), wall.name);
            Assert.That(player.transform.position.y, Is.EqualTo(0f).Within(.07f));
            PlayerJumpPlayModeTests.Write("mainlab-clearance.txt", "Maximum capsule top from floor: " + top + " m; all 12 fixed walls remain higher.\n");
            yield return null;
        }

        [UnityTest] public IEnumerator DeathDuringJump_StopsInputAndRestartCreatesFreshGroundedPlayer()
        {
            Place(new Vector3(-15, .03f, -15)); Assert.That(Jump(player), Is.True);
            for (int i = 0; i < 8; i++) Move(player, Vector3.zero);
            var health = player.GetComponent(T("Health")); Call(health, "TakeDamage", 10000f);
            var session = Find("GameSession").Single();
            Assert.That((bool)session.GetType().GetProperty("Ended").GetValue(session), Is.True);
            Assert.That(Jump(player), Is.False);
            Vector3 position = player.transform.position;
            Call(player, "Update"); Assert.That(player.transform.position, Is.EqualTo(position));
            var oldPlayer = player;
            Call(session, "Restart");
            for (int i = 0; i < 120 && (oldPlayer != null || Find("PlayerController").Length == 0); i++) yield return null;
            yield return null;
            player = Find("PlayerController").Single(); ((Behaviour)player).enabled = false;
            foreach (var brain in Find("DroneBrain")) ((Behaviour)brain).enabled = false;
            Assert.That(oldPlayer == null, Is.True);
            Assert.That((float)T("Health").GetProperty("Current").GetValue(player.GetComponent(T("Health"))), Is.EqualTo(100f));
            Assert.That((bool)Find("GameSession").Single().GetType().GetProperty("Ended").GetValue(Find("GameSession").Single()), Is.False);
            for (int i = 0; i < 15; i++) Move(player, Vector3.zero);
            Assert.That(Jump(player), Is.True);
            var camera = player.GetComponentInChildren<Camera>(); Call(player, "ApplyCameraPose");
            Assert.That(camera.transform.localPosition, Is.EqualTo(Vector3.up * 1.3f));
        }
    }
}
#endif
