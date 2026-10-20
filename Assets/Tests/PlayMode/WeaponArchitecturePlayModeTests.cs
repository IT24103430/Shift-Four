#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class WeaponArchitecturePlayModeTests
    {
        private readonly List<Object> owned = new List<Object>();
        private Component player, weapons, target;
        private Camera camera;
        private NoiseListenerProbe noise;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private static readonly Vector3 Origin = new Vector3(500, 0, 500);
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Call(object instance, string name, params object[] args)
            => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(instance, args);
        private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private static object Field(object instance, string name) => instance.GetType().GetField(name).GetValue(instance);
        private static void ResetBus() => T("SoundEvents").GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private bool Fire() => (bool)Call(weapons, "TryFire");
        private object State => Get(weapons, "ActiveWeapon");
        private float HP => (float)Get(target, "Current");
        private GameObject Own(string name) { var go = new GameObject(name); owned.Add(go); return go; }
        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = Own(name); go.transform.position = position; go.AddComponent<BoxCollider>().size = size; return go;
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible; ResetBus();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Weapon fixture " + Guid.NewGuid()));
            Box("Floor", Origin - Vector3.up * .5f, new Vector3(100, 1, 100));
            player = Actor(Origin); weapons = PlayerWeaponTestActions.Controller(player);
            camera = (Camera)Get(player, "ViewCamera");
            target = Target(camera.transform.position + Vector3.forward * 3f);
            noise = Own("Noise observer").AddComponent<NoiseListenerProbe>();
            Cursor.lockState = CursorLockMode.Locked; Physics.SyncTransforms();
            yield return null;
            Assert.That(Get(player, "CanAct"), Is.True, "Fixture player must be alive, enabled, configured and in a live mission.");
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var item in owned) if (item != null) Object.Destroy(item);
            owned.Clear(); ResetBus();
            Scene old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After weapon fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        private Component Actor(Vector3 position)
        {
            var root = Own("Weapon player"); root.SetActive(false); root.transform.position = position;
            var cc = root.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .35f;
            cc.center = Vector3.up * .9f; cc.skinWidth = .05f; cc.minMoveDistance = 0f;
            var view = Own("Weapon camera").AddComponent<Camera>(); view.enabled = false; view.transform.SetParent(root.transform, false);
            var actor = root.AddComponent(T("PlayerController")); root.SetActive(true);
            Call(actor, "Configure", view);
            return actor;
        }

        private Component Target(Vector3 position)
        {
            var go = Box("Damage target", position, Vector3.one);
            var health = go.AddComponent(T("Health")); Call(health, "Configure", 100f, false); return health;
        }

        private WeaponEventProbe Probe(Component victim = null)
        {
            var probe = Own("Weapon observer").AddComponent<WeaponEventProbe>(); probe.Bind(weapons, victim); return probe;
        }

        private ScriptableObject Definition(float damage = 37f, float range = 18f, float interval = .4f)
        {
            var data = ScriptableObject.CreateInstance(T("WeaponDefinition")); owned.Add(data);
            foreach (var entry in new[] { ("damage", damage), ("range", range), ("fireInterval", interval) })
                data.GetType().GetField(entry.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, entry.Item2);
            return data;
        }

        private Component Viewmodel()
        {
            var root = Own("Presentation root"); root.transform.SetParent(camera.transform, false);
            return root.AddComponent(T("FirstPersonViewmodel"));
        }

        [UnityTest] public IEnumerator RuntimeBootstrap_IsIdempotentAndPreservesFallback()
        {
            Assert.That(player.GetComponents(T("WeaponController")).Length, Is.EqualTo(1));
            Assert.That(Get(player, "Weapons"), Is.SameAs(weapons));
            Assert.That(PlayerWeaponTestActions.Data(player, "Damage"), Is.EqualTo(25f));
            Assert.That(PlayerWeaponTestActions.Data(player, "Range"), Is.EqualTo(30f));
            Assert.That(PlayerWeaponTestActions.Data(player, "FireInterval"), Is.EqualTo(.24f));
            Assert.That(Fire(), Is.True); object state = State; float next = (float)Get(state, "NextShotTime");
            Call(weapons, "Initialize", player);
            Assert.That(State, Is.SameAs(state)); Assert.That((float)Get(State, "NextShotTime"), Is.EqualTo(next));
            Assert.That(Fire(), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator AcceptedShot_AppliesDamageOnceWithOriginalPlayerAttributionAndSound()
        {
            var probe = Probe(target); probe.ReenterOnShot = true;
            Assert.That(Fire(), Is.True);
            Assert.That(HP, Is.EqualTo(75f)); Assert.That(probe.Damages, Is.EqualTo(1));
            Assert.That(probe.Shots, Is.EqualTo(1)); Assert.That(probe.ReentryAccepted, Is.False);
            Assert.That(Field(probe.LastDamage, "Attacker"), Is.SameAs(player));
            Assert.That(Field(probe.LastDamage, "Reason"), Is.EqualTo("Player hitscan shot"));
            Assert.That(Field(probe.LastDamage, "Amount"), Is.EqualTo(25f));
            Assert.That(noise.Noises, Is.EqualTo(1)); Assert.That(noise.Shots, Is.EqualTo(1));
            Assert.That(Field(noise.LastNoise, "Source"), Is.Null);
            Assert.That(Field(noise.LastNoise, "Position"), Is.EqualTo(player.transform.position));
            Assert.That(Field(probe.LastShot, "Attacker"), Is.SameAs(player));
            yield return null;
        }

        [UnityTest] public IEnumerator Cooldown_BlocksRepeatedRequestsAndAllowsNextScheduledShot()
        {
            var probe = Probe(target); float before = Time.time;
            Assert.That(Fire(), Is.True); Assert.That(Fire(), Is.False);
            Call(weapons, "ProcessInput");
            Assert.That((float)Get(State, "NextShotTime"), Is.EqualTo(before + .24f).Within(.00001f));
            Assert.That(HP, Is.EqualTo(75f)); Assert.That(noise.Shots, Is.EqualTo(1));
            yield return new WaitForSeconds(.26f);
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(50f));
            Assert.That(probe.Shots, Is.EqualTo(2)); Assert.That(probe.Damages, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator Cover_IsStillTheFirstHitAndPreventsDamage()
        {
            var cover = Box("Solid cover", camera.transform.position + Vector3.forward * 1.5f, new Vector3(2, 2, .2f));
            Physics.SyncTransforms(); var probe = Probe(target);
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(100f)); Assert.That(probe.Damages, Is.Zero);
            Assert.That((bool)Field(probe.LastShot, "HasHit"), Is.True);
            Assert.That(((RaycastHit)Field(probe.LastShot, "Hit")).collider, Is.SameAs(cover.GetComponent<BoxCollider>()));
            Assert.That(noise.Shots, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator OutOfRangeMiss_ConsumesCooldownAndEmitsOneAcceptedShot()
        {
            target.transform.position = camera.transform.position + Vector3.forward * 40f; Physics.SyncTransforms();
            var probe = Probe(target); Assert.That(Fire(), Is.True); Assert.That(Fire(), Is.False);
            Assert.That(HP, Is.EqualTo(100f)); Assert.That((bool)Field(probe.LastShot, "HasHit"), Is.False);
            Assert.That(probe.Shots, Is.EqualTo(1)); Assert.That(noise.Noises, Is.EqualTo(1)); Assert.That(noise.Shots, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator TriggerCover_IsIgnoredAsBefore()
        {
            var trigger = Box("Trigger cover", camera.transform.position + Vector3.forward, Vector3.one);
            trigger.GetComponent<BoxCollider>().isTrigger = true; Physics.SyncTransforms();
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(75f));
            yield return null;
        }

        [UnityTest] public IEnumerator CameraDirectionAndReconfiguration_DetermineTargetInsteadOfPlayerForward()
        {
            var right = Target(camera.transform.position + Vector3.right * 3f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.right); Physics.SyncTransforms();
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(100f)); Assert.That(Get(right, "Current"), Is.EqualTo(75f));
            yield return new WaitForSeconds(.26f);
            var replacement = Own("Replacement view").AddComponent<Camera>(); replacement.enabled = false;
            replacement.transform.SetParent(player.transform, false); Call(player, "Configure", replacement);
            replacement.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(75f));
        }

        [UnityTest] public IEnumerator Death_CancelsReloadAndPreventsDamageAndSound()
        {
            var probe = Probe(target); Assert.That(Call(weapons, "RequestReload"), Is.True);
            Call(player.GetComponent(T("Health")), "TakeDamage", 1000f);
            Assert.That(Get(State, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Fire(), Is.False); Assert.That(HP, Is.EqualTo(100f));
            Assert.That(probe.Shots, Is.Zero); Assert.That(noise.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator EndedMission_RejectsFireAndCancelsReloadWithoutChangingHealth()
        {
            Assert.That(Call(weapons, "RequestReload"), Is.True);
            var session = Own("Ended mission").AddComponent(T("GameSession")); ((Behaviour)session).enabled = false;
            Call(session, "Finish", "Weapon fixture ended");
            Assert.That(Fire(), Is.False); Call(weapons, "Update");
            Assert.That(Get(State, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(HP, Is.EqualTo(100f)); Assert.That(noise.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator UnlockedCursorInputAndDisabledComponents_RejectFire()
        {
            Cursor.lockState = CursorLockMode.None;
            Assert.That(Call(weapons, "ProcessFireInput", true), Is.False);
            Assert.That(Call(weapons, "ProcessFireInput", false), Is.False);
            ((Behaviour)player).enabled = false; Assert.That(Fire(), Is.False);
            ((Behaviour)player).enabled = true; ((Behaviour)weapons).enabled = false; Assert.That(Fire(), Is.False);
            Assert.That(HP, Is.EqualTo(100f)); Assert.That(noise.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator Equip_UsesWeaponDataAndPreservesCooldownWhenSwitchingBack()
        {
            var probe = Probe(target); object original = Get(State, "Definition");
            Assert.That(Fire(), Is.True); object originalState = State;
            var definition = Definition(); string before = JsonUtility.ToJson(definition);
            Assert.That(Call(weapons, "Equip", definition), Is.True); Assert.That(probe.Equips, Is.EqualTo(1));
            Assert.That(Call(weapons, "Equip", definition), Is.False); Assert.That(probe.Equips, Is.EqualTo(1));
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(38f));
            Assert.That((float)Get(State, "NextShotTime"), Is.EqualTo(Time.time + .4f).Within(.00001f));
            Assert.That(Call(weapons, "Equip", original), Is.True); Assert.That(State, Is.SameAs(originalState));
            Assert.That(Fire(), Is.False); Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
            yield return null;
        }

        [UnityTest] public IEnumerator SharedDefinition_HasIndependentPlayerRuntimeState()
        {
            var definition = Definition(25f); Assert.That(Call(weapons, "Equip", definition), Is.True);
            var other = Actor(Origin + Vector3.right * 5f); var otherWeapons = PlayerWeaponTestActions.Controller(other);
            var otherCamera = (Camera)Get(other, "ViewCamera"); var otherTarget = Target(otherCamera.transform.position + Vector3.forward * 3f);
            Assert.That(Call(otherWeapons, "Equip", definition), Is.True); Physics.SyncTransforms();
            Assert.That(Fire(), Is.True); Assert.That(Call(otherWeapons, "TryFire"), Is.True);
            Assert.That(HP, Is.EqualTo(75f)); Assert.That(Get(otherTarget, "Current"), Is.EqualTo(75f));
            Assert.That(State, Is.Not.SameAs(Get(otherWeapons, "ActiveWeapon")));
            yield return null;
        }

        [UnityTest] public IEnumerator ReloadLifecycle_IsIdempotentBlocksFireAndCancelsOnSwitch()
        {
            var probe = Probe();
            Assert.That(Call(weapons, "RequestReload"), Is.True); Assert.That(Call(weapons, "RequestReload"), Is.False);
            Assert.That(probe.ReloadChanges, Is.EqualTo(1)); Assert.That(Fire(), Is.False);
            Assert.That(Call(weapons, "CompleteReload"), Is.True); Assert.That(Call(weapons, "CompleteReload"), Is.False);
            Assert.That(probe.ReloadChanges, Is.EqualTo(2)); Assert.That(Fire(), Is.True);
            Assert.That(Call(weapons, "RequestReload"), Is.True); object old = State;
            Assert.That(Call(weapons, "Equip", Definition()), Is.True);
            Assert.That(Get(old, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Get(State, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(probe.ReloadChanges, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest] public IEnumerator Viewmodel_CannotInterceptRayEvenWhenNestedColliderIsAddedAfterBinding()
        {
            var model = Viewmodel(); var nested = Own("Nested visual"); nested.transform.SetParent(model.transform, false);
            var late = Box("Late viewmodel collider", camera.transform.position + Vector3.forward, Vector3.one * .5f);
            late.transform.SetParent(nested.transform, true); Physics.SyncTransforms();
            var damage = late.AddComponent(T("Health"));
            Assert.That(late.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(75f)); Assert.That(Get(damage, "Current"), Is.EqualTo(100f));
            Assert.That(late.layer, Is.EqualTo(2)); Assert.That(late.GetComponent<BoxCollider>().enabled, Is.False);
            Assert.That(player.gameObject.layer, Is.EqualTo(0)); Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(Get(model, "EquippedWeapon"), Is.SameAs(Get(State, "Definition")));
            Assert.That((bool)Field(Get(model, "LastShot"), "HasHit"), Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator InvalidViewmodelRoot_DoesNotDisablePlayerCapsule()
        {
            var model = player.gameObject.AddComponent(T("FirstPersonViewmodel"));
            Assert.That(Call(model, "Bind", weapons), Is.False); Call(model, "PrepareForQueries");
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True); Assert.That(player.gameObject.layer, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest] public IEnumerator InteractionAndCamera_ContinueThroughViewmodelGeometry()
        {
            target.gameObject.SetActive(false);
            var session = Own("Mission").AddComponent(T("GameSession")); ((Behaviour)session).enabled = false;
            var console = Box("Console", camera.transform.position + Vector3.forward * 2f, Vector3.one).AddComponent(T("ConsoleSwitch"));
            Call(console, "Configure", "Checkpoint", null);
            var model = Viewmodel();
            var visual = Box("Presentation mesh collider", camera.transform.position + Vector3.forward, Vector3.one * .4f);
            visual.transform.SetParent(model.transform, true); Physics.SyncTransforms();
            Call(player, "UpdateInteraction");
            Assert.That(Get(player, "InteractionPrompt"), Is.EqualTo("E: activate Checkpoint"));
            Assert.That(Vector3.Distance(camera.transform.localPosition, Vector3.up * 1.3f), Is.LessThan(.0001f));
            Assert.That(camera.nearClipPlane, Is.EqualTo(.05f).Within(.0001f));
            Call(console, "Interact"); Assert.That(Get(console, "Activated"), Is.True);
            Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(1)); Assert.That(noise.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator FaultyPresentationObserver_DoesNotSuppressDamageOrLaterShotObservers()
        {
            var first = Probe(target); first.ThrowOnShot = true; var second = Probe();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Fixture shot observer"));
            Assert.That(Fire(), Is.True); Assert.That(HP, Is.EqualTo(75f));
            Assert.That(first.Damages, Is.EqualTo(1)); Assert.That(second.Shots, Is.EqualTo(1));
            Assert.That(noise.Shots, Is.EqualTo(1));
            yield return null;
        }
    }
}
#endif
