#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class FirstPersonEquipPlayModeTests
    {
        private Component player, weapons, mount, model, presenter, rig, grip;
        private Camera camera;
        private Vector3 cameraRest, rootRest;
        private Quaternion cameraRotation, rootRotation;
        private float timeScale, capture;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<string> events = new List<string>();
        private Delegate transitionListener;
        private ScriptableObject equipOnHidden;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;
        private object State => Get(weapons, "ActiveWeapon");
        private string Phase => Get(mount, "TransitionState").ToString();
        private bool Ready => (bool)Get(mount, "IsReady");
        private float Visibility => (float)Get(mount, "EquipVisibility");
        private bool Fire() => (bool)Call(weapons, "TryFire");
        private static string Evidence
        {
            get { string p = Environment.GetEnvironmentVariable("SHIFTFOUR_EQUIP_EVIDENCE") ?? Path.Combine(Application.temporaryCachePath, "ArmsEquip"); Directory.CreateDirectory(p); return p; }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            timeScale = Time.timeScale; capture = Time.captureDeltaTime; cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            // Pause scaled time before loading so the real bootstrap equip can be inspected at t=0.
            Time.timeScale = 0; Time.captureDeltaTime = 1f / 120f;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            player = Object.FindAnyObjectByType(T("PlayerController")) as Component; weapons = player.GetComponent(T("WeaponController"));
            mount = player.GetComponent(T("FirstPersonWeaponMount")); camera = (Camera)Get(player, "ViewCamera");
            foreach (Component brain in Object.FindObjectsByType(T("DroneBrain")))
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>(); if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            Resolve(); cameraRest = camera.transform.localPosition; cameraRotation = camera.transform.localRotation;
            var info = mount.GetType().GetEvent("TransitionChanged");
            transitionListener = Delegate.CreateDelegate(info.EventHandlerType, this,
                GetType().GetMethod(nameof(OnTransition), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(info.EventHandlerType.GetGenericArguments()[0]));
            info.AddEventHandler(mount, transitionListener);
            Assert.That(Phase, Is.EqualTo("Equipping")); Assert.That(Visibility, Is.Zero);
            Time.timeScale = 1;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mount != null && transitionListener != null) mount.GetType().GetEvent("TransitionChanged").RemoveEventHandler(mount, transitionListener);
            foreach (var o in owned) if (o != null) Object.Destroy(o); owned.Clear(); events.Clear(); equipOnHidden = null;
            Time.timeScale = timeScale; Time.captureDeltaTime = capture;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After equip fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old); Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }
        private void Resolve()
        {
            model = (Component)Get(mount, "Instance"); Assert.That(model, Is.Not.Null);
            presenter = model.GetComponent(T("PistolPresentation")); rig = model.GetComponentInChildren(T("FirstPersonArmsRig"), true);
            grip = rig.GetComponent(T("FirstPersonPistolGrip"));
            var prefab = (Component)Get(Get(State, "Definition"), "ViewmodelPrefab"); rootRest = prefab.transform.localPosition; rootRotation = prefab.transform.localRotation;
        }
        private void OnTransition<TState>(TState state)
        {
            events.Add(state.ToString());
            if (state.ToString() == "Hidden" && equipOnHidden != null) { var data = equipOnHidden; equipOnHidden = null; Assert.That(Call(weapons, "Equip", data), Is.EqualTo(true)); }
        }
        private object[] Snapshot(object state) => new[] { Get(state, "RoundsInMagazine"), Get(state, "ReserveRounds"), Get(state, "NextShotTime") };
        private void Unchanged(object state, object[] snapshot) => CollectionAssert.AreEqual(snapshot, Snapshot(state));
        private void AssertAttached()
        {
            foreach (var pair in new[] { ("RightHand", "RightHandTarget"), ("LeftHand", "SupportHandTarget") })
            {
                var hand = (Transform)Get(rig, pair.Item1); var target = (Transform)Get(grip, pair.Item2);
                Assert.That(Vector3.Distance(hand.position, target.position), Is.LessThan(.0002f));
                Assert.That(Degrees(Quaternion.Inverse(target.rotation) * hand.rotation), Is.LessThan(.05));
            }
            Assert.That(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
            Assert.That(player.GetComponentsInChildren(T("FirstPersonViewmodel"), true).Length, Is.EqualTo(1));
            Assert.That(Vector3.Distance(camera.transform.localPosition, cameraRest), Is.LessThan(.00002f));
            Assert.That(Degrees(Quaternion.Inverse(cameraRotation) * camera.transform.localRotation), Is.LessThan(.001));
        }
        private IEnumerator WaitReady()
        {
            float start = Time.time;
            while (!Ready) { Assert.That(Time.time - start, Is.LessThan(2f)); yield return null; }
        }
        private IEnumerator WaitHidden()
        {
            float start = Time.time;
            while (Phase != "Hidden") { Assert.That(Time.time - start, Is.LessThan(1f)); yield return null; }
        }
        private ScriptableObject Definition(float interval = .9f)
        {
            var data = ScriptableObject.CreateInstance(T("WeaponDefinition")); owned.Add(data);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var v in new[] { ("damage", 37f), ("fireInterval", interval) }) data.GetType().GetField(v.Item1, flags).SetValue(data, v.Item2);
            data.GetType().GetField("viewmodelPrefab", flags).SetValue(data, Get(Get(State, "Definition"), "ViewmodelPrefab")); return data;
        }

        [UnityTest] public IEnumerator InitialEquip_AllowsFiringDuringRaiseAndPreservesCameraBonesAmmoAndDamage()
        {
            object state = State; object[] snapshot = Snapshot(state); string definition = JsonUtility.ToJson((Object)Get(state, "Definition"));
            var skin = (SkinnedMeshRenderer)Get(rig, "Skin"); var offsets = skin.bones.Select(b => b.localPosition).ToArray(); var scales = skin.bones.Select(b => b.localScale).ToArray();
            var fingers = skin.bones.Where(b => b.name.StartsWith("f_") || b.name.StartsWith("thumb.") || b.name.StartsWith("palm_")).ToArray(); var rotations = fingers.Select(b => b.localRotation).ToArray();
            var probe = new GameObject("Equip event probe").AddComponent<WeaponEventProbe>(); probe.Bind(weapons);
            Call(mount, "Refresh"); Vector3 raisePose = model.transform.localPosition;
            Assert.That(Fire(), Is.True); Assert.That(model.transform.localPosition, Is.EqualTo(raisePose));
            snapshot = Snapshot(state);
            float began = Time.time; int frames = 0;
            while (!Ready)
            {
                if (Time.time < (float)Get(state, "NextShotTime")) Assert.That(Fire(), Is.False);
                Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(false)); Unchanged(state, snapshot); AssertAttached();
                CollectionAssert.AreEqual(offsets, skin.bones.Select(b => b.localPosition).ToArray()); CollectionAssert.AreEqual(scales, skin.bones.Select(b => b.localScale).ToArray());
                CollectionAssert.AreEqual(rotations, fingers.Select(b => b.localRotation).ToArray());
                Assert.That(++frames, Is.LessThan(55)); yield return null;
            }
            Assert.That(Time.time - began, Is.EqualTo(.38f).Within(2f / 120f)); Assert.That(probe.Shots, Is.EqualTo(1));
            Assert.That(Vector3.Distance(model.transform.localPosition, rootRest), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(rootRotation) * model.transform.localRotation), Is.LessThan(.001)); AssertAttached();
            Assert.That(events.Count(s => s == "Ready"), Is.EqualTo(1));
            // The .38-second raise finishes before the weapon's .4-second shot cooldown.
            while (Time.time < (float)Get(state, "NextShotTime")) { Assert.That(Fire(), Is.False); yield return null; }
            Assert.That(Fire(), Is.True); Assert.That(probe.Shots, Is.EqualTo(2));
            Assert.That(Get(state, "RoundsInMagazine"), Is.EqualTo((int)snapshot[0] - 1)); Assert.That(Get(Get(state, "Definition"), "Damage"), Is.EqualTo(22f));
            Assert.That(JsonUtility.ToJson((Object)Get(state, "Definition")), Is.EqualTo(definition));
        }

        [UnityTest] public IEnumerator UnequipAndReequip_ReuseOneModelRetainWeaponStateAndRestoreExactGrip()
        {
            yield return WaitReady(); Assert.That(Fire(), Is.True); object state = State; object[] snapshot = Snapshot(state); Component original = model;
            Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true)); float began = Time.time;
            while (Phase != "Hidden") { Assert.That(Fire(), Is.False); Unchanged(state, snapshot); AssertAttached(); yield return null; }
            Assert.That(Time.time - began, Is.EqualTo(.26f).Within(2f / 120f)); Assert.That(model.gameObject.activeInHierarchy, Is.False);
            for (int i = 0; i < 12; i++) { Assert.That(Fire(), Is.False); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(false)); yield return null; }
            Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(true)); yield return WaitReady();
            Assert.That(Get(mount, "Instance"), Is.SameAs(original)); Assert.That(State, Is.SameAs(state)); Unchanged(state, snapshot); AssertAttached();
            Assert.That(model.transform.localPosition, Is.EqualTo(rootRest)); Assert.That(Degrees(Quaternion.Inverse(rootRotation) * model.transform.localRotation), Is.LessThan(.001));
            Assert.That(Fire(), Is.True); Assert.That(Get(state, "RoundsInMagazine"), Is.EqualTo((int)snapshot[0] - 1));
        }

        [UnityTest] public IEnumerator RepeatedRequestsPauseAndReversals_DoNotRestartSnapOrCreateModels()
        {
            object definition = Get(State, "Definition"); Component original = model; float began = Time.time;
            for (int i = 0; i < 18; i++)
            {
                Assert.That(Call(weapons, "Equip", definition), Is.EqualTo(false)); Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(false)); yield return null;
            }
            Time.timeScale = 0; Call(mount, "Refresh"); Vector3 paused = model.transform.localPosition; float visible = Visibility;
            for (int i = 0; i < 12; i++) { yield return null; Assert.That(model.transform.localPosition, Is.EqualTo(paused)); Assert.That(Visibility, Is.EqualTo(visible)); }
            Time.timeScale = 1; yield return WaitReady(); Assert.That(Time.time - began, Is.EqualTo(.38f).Within(2f / 120f));
            Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true));
            for (int i = 0; i < 10; i++) { Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(false)); yield return null; }
            Call(mount, "Refresh"); Vector3 position = model.transform.localPosition; Quaternion rotation = model.transform.localRotation;
            Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(true)); Assert.That(Vector3.Distance(position, model.transform.localPosition), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(rotation) * model.transform.localRotation), Is.LessThan(.001));
            for (int i = 0; i < 10; i++) { AssertAttached(); yield return null; }
            Call(mount, "Refresh"); position = model.transform.localPosition; rotation = model.transform.localRotation;
            Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true)); Assert.That(Vector3.Distance(position, model.transform.localPosition), Is.LessThan(.000001f));
            Assert.That(Degrees(Quaternion.Inverse(rotation) * model.transform.localRotation), Is.LessThan(.001)); yield return WaitHidden();
            Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(true)); yield return WaitReady(); AssertAttached(); Assert.That(Get(mount, "Instance"), Is.SameAs(original));
        }

        private IEnumerator DieDuringTransition(bool lower)
        {
            if (lower) { yield return WaitReady(); Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true)); }
            for (int i = 0; i < 10; i++) yield return null;
            object state = State; object[] snapshot = Snapshot(state); Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            Assert.That(Fire(), Is.False); yield return null; yield return null;
            Assert.That(Phase, Is.EqualTo("Hidden")); Assert.That(model.gameObject.activeInHierarchy, Is.False); Unchanged(state, snapshot);
            Assert.That(((Renderer)Get(presenter, "MuzzleFlash")).enabled, Is.False);
            Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(false)); Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(false));
            Assert.That(Call(weapons, "Equip", Definition()), Is.EqualTo(false)); Assert.That(Get(state, "ReloadState").ToString(), Is.EqualTo("Idle"));
        }
        [UnityTest] public IEnumerator DeathWhileEquipping_HidesImmediatelyAndCannotResumeOrFire() { yield return DieDuringTransition(false); }
        [UnityTest] public IEnumerator DeathWhileUnequipping_HidesImmediatelyAndCannotResumeOrFire() { yield return DieDuringTransition(true); }

        [UnityTest] public IEnumerator UnequipDuringReload_CancelsGameplayWithoutGrantingAmmoOrLeavingAMagazineOffset()
        {
            yield return WaitReady(); Assert.That(Fire(), Is.True); Assert.That(Call(weapons, "RequestReload"), Is.EqualTo(true));
            while ((float)Get(model, "ReloadProgress") < .45f) yield return null;
            object state = State; object[] snapshot = Snapshot(state); var magazine = (Transform)Get(presenter, "MagazinePart");
            Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true)); Assert.That(Get(state, "ReloadState").ToString(), Is.EqualTo("Idle"));
            Assert.That(Call(weapons, "CompleteReload"), Is.EqualTo(false)); yield return WaitHidden();
            for (int i = 0; i < 210; i++) { Assert.That(Fire(), Is.False); yield return null; }
            Unchanged(state, snapshot); Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(true)); yield return WaitReady(); AssertAttached();
            Assert.That((float)Get(model.GetComponent(T("FirstPersonReloadMotion")), "ContactWeight"), Is.Zero);
            Assert.That(magazine.localPosition, Is.EqualTo((Vector3)presenter.GetType().GetField("magazineRestPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter)));
            Assert.That(Fire(), Is.True);
        }

        [UnityTest] public IEnumerator EquipDuringTransitions_KeepsLatestRequestAndEachWeaponsAmmoAndCooldown()
        {
            yield return WaitReady(); Assert.That(Fire(), Is.True); object a = State, definitionA = Get(a, "Definition"); object[] savedA = Snapshot(a); Component first = model;
            var definitionB = Definition(); Assert.That(Call(weapons, "Equip", definitionB), Is.EqualTo(true)); object b = State; object[] savedB = Snapshot(b);
            Assert.That(Phase, Is.EqualTo("Unequipping")); Assert.That(Fire(), Is.False);
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(Call(weapons, "Equip", definitionA), Is.EqualTo(true)); Assert.That(Phase, Is.EqualTo("Equipping")); yield return WaitReady();
            Assert.That(Get(mount, "Instance"), Is.SameAs(first)); Assert.That(State, Is.SameAs(a)); Unchanged(a, savedA); Unchanged(b, savedB);
            Assert.That(Call(weapons, "Equip", definitionB), Is.EqualTo(true));
            while (!Ready) { if (!(bool)Get(mount, "AllowsFiring")) Assert.That(Fire(), Is.False); Assert.That(player.GetComponentsInChildren(T("FirstPersonViewmodel"), true).Length, Is.EqualTo(1)); yield return null; }
            Resolve(); Assert.That(model, Is.Not.SameAs(first)); Assert.That(State, Is.SameAs(b)); Unchanged(a, savedA); Unchanged(b, savedB); AssertAttached();
            Assert.That(Fire(), Is.True); savedB = Snapshot(b);
            Assert.That(Call(weapons, "Equip", definitionA), Is.EqualTo(true)); Assert.That(Call(weapons, "Equip", definitionB), Is.EqualTo(true)); yield return WaitReady();
            Assert.That(State, Is.SameAs(b)); Unchanged(b, savedB); Unchanged(a, savedA);
            Assert.That(Time.time, Is.LessThan((float)savedB[2])); Assert.That(Fire(), Is.False, "The saved weapon-specific cooldown still applies after the raise finishes.");
        }

        [UnityTest] public IEnumerator DisableAndCameraReplacement_ResetSafelyAndReequipWithoutADuplicate()
        {
            for (int i = 0; i < 12; i++) yield return null;
            object state = State; object[] snapshot = Snapshot(state); Component original = model;
            ((Behaviour)mount).enabled = false; Assert.That(Phase, Is.EqualTo("Hidden")); Assert.That(Fire(), Is.False);
            var replacement = new GameObject("Transition replacement camera").AddComponent<Camera>(); replacement.enabled = false;
            replacement.transform.SetParent(player.transform, false); Call(player, "Configure", replacement);
            camera = replacement; cameraRest = camera.transform.localPosition; cameraRotation = camera.transform.localRotation;
            ((Behaviour)mount).enabled = true; Assert.That(Phase, Is.EqualTo("Equipping")); yield return WaitReady();
            Assert.That(((Transform)Get(mount, "Socket")).parent, Is.SameAs(replacement.transform)); Assert.That(Get(mount, "Instance"), Is.SameAs(original));
            Unchanged(state, snapshot); AssertAttached();
        }

        [UnityTest] public IEnumerator HiddenTransitionHook_CanRequestANewWeaponWithoutReentryOrDuplicateModels()
        {
            yield return WaitReady(); var data = Definition(); equipOnHidden = data;
            Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true));
            float began = Time.time;
            while (Get(State, "Definition") != data || !Ready) { Assert.That(Time.time - began, Is.LessThan(2f)); yield return null; }
            Resolve(); Assert.That(Get(State, "Definition"), Is.SameAs(data)); AssertAttached();
            Assert.That(events.Count(s => s == "Hidden"), Is.EqualTo(1)); Assert.That(events.Count(s => s == "Unequipping"), Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator GameViewEvidence_CapturesInitialRaiseLowerAndReequipInMainLab()
        {
            Time.captureDeltaTime = 1f / 60f; Time.timeScale = 0;
            var type = TypeCache.GetTypesDerivedFrom<EditorWindow>().FirstOrDefault(t => t.FullName == "UnityEditor.GameView");
            var view = Application.isBatchMode || type == null ? null : EditorWindow.GetWindow(type);
            if (view != null) { view.Show(); view.Focus(); }
            for (int i = 0; i < 8; i++) yield return null;
            Capture(view, "initial-lowered"); Time.timeScale = 1;
            bool raiseMid = false, equipped = false, lowerMid = false, hidden = false, reMid = false, reReady = false;
            var trace = new System.Text.StringBuilder("frame,state,visibility,root_x,root_y,root_z,ammo,reserve\n");
            for (int frame = 0; frame < 150; frame++)
            {
                if (frame == 40) { Capture(view, "unequip-start"); Assert.That(Call(mount, "RequestUnequip"), Is.EqualTo(true)); }
                if (frame == 70) { Capture(view, "reequip-start"); Assert.That(Call(mount, "RequestEquip"), Is.EqualTo(true)); }
                yield return null; AssertAttached();
                if (!raiseMid && frame < 40 && Visibility >= .5f) { Capture(view, "equip-midpoint"); raiseMid = true; }
                if (!equipped && frame < 40 && Ready) { Capture(view, "equipped"); equipped = true; }
                if (!lowerMid && frame >= 40 && frame < 70 && Visibility <= .5f) { Capture(view, "unequip-midpoint"); lowerMid = true; }
                if (!hidden && frame >= 40 && Phase == "Hidden") { Capture(view, "holstered"); hidden = true; }
                if (!reMid && frame >= 70 && Visibility >= .5f) { Capture(view, "reequip-midpoint"); reMid = true; }
                if (!reReady && frame >= 70 && Ready) { Capture(view, "reequipped"); reReady = true; }
                Vector3 p = model.transform.localPosition;
                trace.AppendLine(FormattableString.Invariant($"{frame},{Phase},{Visibility},{p.x},{p.y},{p.z},{Get(State, "RoundsInMagazine")},{Get(State, "ReserveRounds")}"));
                if (frame % 2 == 0) Capture(view, "sequence/frame-" + (frame / 2).ToString("D4"));
            }
            Assert.That(raiseMid && equipped && lowerMid && hidden && reMid && reReady, Is.True);
            Assert.That(Get(State, "RoundsInMagazine"), Is.EqualTo(12)); Assert.That(Get(State, "ReserveRounds"), Is.EqualTo(80));
            File.WriteAllText(Path.Combine(Evidence, "equip-sequence.csv"), trace.ToString());
        }
        private static void Capture(EditorWindow view, string name)
        {
            string old = Environment.GetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE");
            try
            {
                Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE", Evidence);
                typeof(FirstPersonReloadPlayModeTests).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { view, name });
            }
            finally { Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE", old); }
        }
    }
}
#endif
