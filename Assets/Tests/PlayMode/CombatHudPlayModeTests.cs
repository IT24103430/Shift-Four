#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class CombatHudPlayModeTests
    {
        private Component session, player, hud, scout, playerHealth, scoutHealth;
        private Component[] brains;
        private Camera camera;
        private GameObject wall;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private float captureDelta;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name, params object[] arguments) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, arguments);
        private static Component Find(string name) => Object.FindAnyObjectByType(T(name)) as Component;
        private static Component[] FindAll(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private float Value(string name) => (float)Get(hud, name);
        private bool Visible => (bool)Call(hud, "IsDroneBarVisible", scout);
        private bool Fire() => (bool)Call(Get(player, "Weapons"), "TryFire");

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible; captureDelta = Time.captureDeltaTime;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            // UI feedback uses unscaled real time; do not accelerate its gameplay waits with capture stepping.
            Time.captureDeltaTime = 0;
            session = Find("GameSession"); player = Find("PlayerController"); hud = Find("CombatHud");
            playerHealth = player.GetComponent(T("Health")); camera = (Camera)Get(player, "ViewCamera");
            brains = FindAll("DroneBrain"); scout = brains.Single(b => b.GetType().Name == "ScoutBrain");
            scoutHealth = scout.GetComponent(T("Health"));
            foreach (var brain in brains)
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            Cursor.lockState = CursorLockMode.None;
            AimAtScout(); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (wall != null) Object.Destroy(wall);
            Time.captureDeltaTime = captureDelta;
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After HUD fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(previous);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }
        private Vector3 TargetCenter => scout.transform.TransformPoint((Vector3)Get(scout.GetComponent(T("DroneBody")), "EnvelopeCenter"));
        private void AimAtScout()
        {
            var capsule = player.GetComponent<CharacterController>(); capsule.enabled = false;
            player.transform.position = scout.transform.position + Vector3.back * 5;
            capsule.enabled = true; Call(player, "ApplyCameraPose"); camera.transform.LookAt(TargetCenter);
            Physics.SyncTransforms();
        }
        private void BlockScout()
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "HUD visibility and hitscan blocker";
            wall.transform.position = Vector3.Lerp(camera.transform.position, TargetCenter, .5f);
            wall.transform.localScale = new Vector3(3, 4, .35f); Physics.SyncTransforms();
        }
        private void Damage(Component health, float amount, Object attacker = null) => Call(health, "ApplyDamage", amount, attacker, "HUD integration fixture");
        private static Delegate[] DamageObservers(Component health)
            => (health.GetType().GetField("Damaged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(health) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();

        [UnityTest] public IEnumerator MainLab_HasOneHud_ActualDamageAndRecoveryDriveVitals()
        {
            Assert.That(FindAll("CombatHud").Length, Is.EqualTo(1));
            Damage(playerHealth, 25, scout);
            Assert.That(Value("DamageFlashStrength"), Is.GreaterThan(.9f));
            Assert.That((bool)Get(hud, "DamageDirectionKnown"), Is.True);
            yield return null;
            Assert.That((float)Get(playerHealth, "Current"), Is.EqualTo(75));
            Assert.That(Value("PlayerHealthFraction"), Is.EqualTo(.75f).Within(.001f));
            Assert.That(Value("DamageTrailFraction"), Is.GreaterThan(Value("PlayerHealthFraction")));
            yield return new WaitForSeconds(3.15f);
            Assert.That((float)Get(playerHealth, "Current"), Is.GreaterThan(75));
            Assert.That((bool)Get(hud, "IsRecovering"), Is.True);
            Assert.That(Value("HitMarkerStrength"), Is.Zero);
        }
        [UnityTest] public IEnumerator ActualHitscan_ConfirmsHitsAndDefeat_ButRejectsWallsMissesAndDeadTargets()
        {
            float before = (float)Get(scoutHealth, "Current");
            Assert.That(Fire(), Is.True);
            Assert.That((float)Get(scoutHealth, "Current"), Is.EqualTo(before - 22));
            Assert.That(Value("HitMarkerStrength"), Is.GreaterThan(.9f));
            yield return new WaitForSeconds(.3f);
            BlockScout(); Assert.That(Fire(), Is.True);
            Assert.That((float)Get(scoutHealth, "Current"), Is.EqualTo(before - 22));
            Assert.That(Value("HitMarkerStrength"), Is.Zero, "Firing into cover cannot confirm a hit.");
            Object.Destroy(wall); yield return null; yield return new WaitForSeconds(.3f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up);
            Assert.That(Fire(), Is.True); Assert.That(Value("HitMarkerStrength"), Is.Zero);
            yield return new WaitForSeconds(.3f); AimAtScout();
            int remainingShots = Mathf.CeilToInt((before - 22f) / 22f);
            for (int i = 0; i < remainingShots; i++)
            {
                Assert.That(Fire(), Is.True);
                if (i < remainingShots - 1) yield return new WaitForSeconds(.3f);
            }
            Assert.That((bool)Get(scoutHealth, "IsAlive"), Is.False);
            Assert.That(Value("DefeatMarkerStrength"), Is.GreaterThan(.9f));
            yield return null; Assert.That(Visible, Is.False);
            yield return new WaitForSeconds(.75f);
            Assert.That(Fire(), Is.True); Assert.That(Value("HitMarkerStrength"), Is.Zero);
            Assert.That(Value("DefeatMarkerStrength"), Is.Zero);
        }
        [UnityTest] public IEnumerator DroneBars_RevealOnAim_FadeAfterLookAway_AndRespectSolidOcclusion()
        {
            yield return null;
            Assert.That(Visible, Is.True);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return new WaitForSeconds(1.1f);
            Assert.That(Visible, Is.False);
            AimAtScout(); yield return null; Assert.That(Visible, Is.True);
            BlockScout(); yield return null;
            Assert.That(Visible, Is.False, "A recently revealed drone must never show through a wall.");
            Damage(scoutHealth, 1, player); yield return null;
            Assert.That(Visible, Is.False, "A damage reveal does not override physical occlusion.");
            Object.Destroy(wall); yield return null; AimAtScout(); yield return null;
            Assert.That(Visible, Is.True);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back); yield return null;
            Assert.That(Visible, Is.False, "Labels behind the camera must not project onto the screen.");
        }
        [UnityTest] public IEnumerator UnattributedAndOtherAttackerDamage_CannotCreatePlayerHitMarkers()
        {
            Damage(scoutHealth, 10); Damage(scoutHealth, 10, brains.First(b => b != scout));
            Assert.That(Value("HitMarkerStrength"), Is.Zero); Assert.That(Value("DefeatMarkerStrength"), Is.Zero);
            Damage(playerHealth, 10);
            Assert.That(Value("DamageFlashStrength"), Is.GreaterThan(.9f));
            Assert.That((bool)Get(hud, "DamageDirectionKnown"), Is.False, "Unknown attackers do not invent a direction.");
            yield return null;
        }
        [UnityTest] public IEnumerator DisableReenableAndRepeatedInitialize_DoNotDuplicateDamageSubscriptions()
        {
            int targetSubscribers = DamageObservers(scoutHealth).Length;
            Assert.That(DamageObservers(playerHealth).Count(d => ReferenceEquals(d.Target, hud)), Is.EqualTo(1));
            ((Behaviour)hud).enabled = false;
            Assert.That(DamageObservers(playerHealth).Count(d => ReferenceEquals(d.Target, hud)), Is.Zero);
            Assert.That(DamageObservers(scoutHealth).Length, Is.EqualTo(targetSubscribers - 1));
            Damage(scoutHealth, 1, player); Assert.That(Value("HitMarkerStrength"), Is.Zero);
            ((Behaviour)hud).enabled = true;
            var targets = Array.CreateInstance(T("DroneBrain"), brains.Length);
            for (int i = 0; i < brains.Length; i++) targets.SetValue(brains[i], i);
            Call(hud, "Initialize", session, player, targets); Call(hud, "Initialize", session, player, targets);
            Assert.That(DamageObservers(playerHealth).Count(d => ReferenceEquals(d.Target, hud)), Is.EqualTo(1));
            Assert.That(DamageObservers(scoutHealth).Length, Is.EqualTo(targetSubscribers));
            Damage(scoutHealth, 1, player); Assert.That(Value("HitMarkerStrength"), Is.GreaterThan(.9f));
            yield return null;
        }
        [UnityTest] public IEnumerator DeathAndActualRestart_ClearBarsTransientFeedbackAndOldObservers()
        {
            Assert.That(Fire(), Is.True);
            Damage(scoutHealth, 1, player); Damage(playerHealth, 10000, scout); yield return null;
            Assert.That((bool)Get(session, "Ended"), Is.True);
            Assert.That((int)Get(hud, "VisibleDroneBarCount"), Is.Zero);
            Component previousHud = hud;
            Call(session, "Restart"); yield return null; yield return null;
            session = Find("GameSession"); player = Find("PlayerController"); hud = Find("CombatHud");
            playerHealth = player.GetComponent(T("Health")); Cursor.lockState = CursorLockMode.None;
            Assert.That(previousHud == null, Is.True);
            Assert.That(FindAll("CombatHud").Length, Is.EqualTo(1));
            Assert.That((float)Get(playerHealth, "Current"), Is.EqualTo(100));
            object weaponState = Get(Get(player, "Weapons"), "ActiveWeapon");
            Assert.That(Get(weaponState, "RoundsInMagazine"), Is.EqualTo(20));
            Assert.That(Get(weaponState, "ReserveRounds"), Is.EqualTo(80));
            Assert.That(Get(weaponState, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Value("HitMarkerStrength"), Is.Zero); Assert.That(Value("DefeatMarkerStrength"), Is.Zero);
            Assert.That(Value("DamageFlashStrength"), Is.Zero);
            Assert.That(DamageObservers(playerHealth).Count(d => ReferenceEquals(d.Target, hud)), Is.EqualTo(1));
        }
    }
}
#endif
