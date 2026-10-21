using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonEquipEditModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Driver() => Activator.CreateInstance(T("FirstPersonEquipTransition"));
        private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name).Invoke(o, args);
        private static object Get(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
        private static float Visibility(object o) => (float)Get(o, "Visibility");
        private static string State(object o) => Get(o, "State").ToString();

        [Test] public void InitialEquip_StartsHiddenAndReachesExactRestAtConfiguredDuration()
        {
            object d = Driver(); Assert.That(State(d), Is.EqualTo("Hidden"));
            Assert.That(Call(d, "RequestEquip", 10d, .38f), Is.True); Assert.That(Visibility(d), Is.Zero);
            Call(d, "Sample", 10.19d); Assert.That(Visibility(d), Is.EqualTo(.5f).Within(.00001f));
            Call(d, "Sample", 10.380001d); Assert.That(State(d), Is.EqualTo("Ready")); Assert.That(Visibility(d), Is.EqualTo(1));
            Call(d, "Sample", 100d); Assert.That(Visibility(d), Is.EqualTo(1));
        }
        [Test] public void Unequip_ReachesExactHiddenEndpointAtConfiguredDuration()
        {
            object d = Driver(); Call(d, "ResetReady", 0d); Assert.That(Call(d, "RequestUnequip", 1d, .26f), Is.True);
            Call(d, "Sample", 1.13d); Assert.That(Visibility(d), Is.EqualTo(.5f).Within(.00001f));
            Call(d, "Sample", 1.260001d); Assert.That(State(d), Is.EqualTo("Hidden")); Assert.That(Visibility(d), Is.Zero);
        }
        [Test] public void RepeatedRequests_DoNotRestartAnExistingTransition()
        {
            object d = Driver(); Call(d, "RequestEquip", 0d, .38f);
            for (int i = 1; i <= 30; i++) Assert.That(Call(d, "RequestEquip", i * .01d, .38f), Is.False);
            Call(d, "Sample", .380001d); Assert.That(State(d), Is.EqualTo("Ready"));
            Call(d, "RequestUnequip", 1d, .26f);
            for (int i = 1; i <= 20; i++) Assert.That(Call(d, "RequestUnequip", 1 + i * .01d, .26f), Is.False);
            Call(d, "Sample", 1.260001d); Assert.That(State(d), Is.EqualTo("Hidden"));
        }
        [Test] public void ReversingMidTransition_RetainsPoseAndVelocityThenSettles()
        {
            object d = Driver(); Call(d, "RequestEquip", 0d, .38f); const double dt = .00001;
            Call(d, "Sample", .16d - dt); float before = Visibility(d); Call(d, "Sample", .16d); float at = Visibility(d);
            Assert.That(Call(d, "RequestUnequip", .16d, .26f), Is.True); Assert.That(Visibility(d), Is.EqualTo(at));
            Call(d, "Sample", .16d + dt); float after = Visibility(d);
            Assert.That((after - at) / dt, Is.EqualTo((at - before) / dt).Within(.02));
            Call(d, "Sample", .420001d); Assert.That(State(d), Is.EqualTo("Hidden")); Assert.That(Visibility(d), Is.Zero);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SamplingAtDifferentFrameRates_ProducesTheSamePoseAndEndpoint(int fps)
        {
            object d = Driver(), reference = Driver(); Call(d, "RequestEquip", 0d, .38f); Call(reference, "RequestEquip", 0d, .38f);
            for (int i = 1; i <= fps; i++) Call(d, "Sample", i / (double)fps);
            Call(reference, "Sample", 1d); Assert.That(Visibility(d), Is.EqualTo(Visibility(reference))); Assert.That(State(d), Is.EqualTo("Ready"));
            Call(d, "RequestUnequip", 1d, .26f); Call(reference, "RequestUnequip", 1d, .26f);
            for (int i = 1; i <= fps / 10; i++) Call(d, "Sample", 1d + i / (double)fps);
            Call(reference, "Sample", 1.1d); Assert.That(Visibility(d), Is.EqualTo(Visibility(reference)).Within(.000001));
        }
        [Test] public void ResetAndInvalidTime_DoNotResumeAStaleTransition()
        {
            object d = Driver(); Call(d, "RequestEquip", 0d, .38f); Call(d, "Sample", .2d); float current = Visibility(d);
            foreach (double t in new[] { double.NaN, double.PositiveInfinity, -.5d })
            { Call(d, "Sample", t); Assert.That(Visibility(d), Is.EqualTo(current)); }
            Call(d, "ResetHidden", .2d); Call(d, "Sample", 9d); Assert.That(State(d), Is.EqualTo("Hidden")); Assert.That(Visibility(d), Is.Zero);
            Call(d, "RequestEquip", 9d, .38f); Call(d, "Sample", 9.380001d); Assert.That(State(d), Is.EqualTo("Ready"));
        }
        [Test] public void RapidReversals_StayFiniteBoundedAndContinuous()
        {
            object d = Driver(); Call(d, "RequestEquip", 0d, .38f);
            for (int i = 1; i <= 360; i++)
            {
                double now = i / 120d; Call(d, "Sample", now); float before = Visibility(d);
                if (i % 7 == 0) Call(d, i % 14 == 0 ? "RequestEquip" : "RequestUnequip", now, .26f);
                Assert.That(Visibility(d), Is.EqualTo(before)); Assert.That(float.IsFinite(before), Is.True); Assert.That(before, Is.InRange(0, 1));
            }
            Call(d, "RequestUnequip", 3d, .26f); Call(d, "Sample", 3.260001d); Assert.That(State(d), Is.EqualTo("Hidden"));
        }
        [Test] public void OwnedPistol_StoresTransitionSettingsWithoutChangingItsSourceTransform()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab");
            var motion = root.GetComponent(T("FirstPersonEquipMotion")); Assert.That(motion, Is.Not.Null);
            Assert.That(Get(motion, "EquipDuration"), Is.EqualTo(.38f)); Assert.That(Get(motion, "UnequipDuration"), Is.EqualTo(.26f));
            Assert.That(Get(motion, "BlockFiringDuringEquip"), Is.EqualTo(false));
            Assert.That(root.GetComponents(T("FirstPersonViewmodel")).Length, Is.EqualTo(1));
            Assert.That(root.GetComponent(T("FirstPersonReloadMotion")), Is.Not.Null);
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        }
        [Test] public void PresentationSampling_HasNoAmmunitionOrCooldownAuthority()
        {
            var data = ScriptableObject.CreateInstance(T("WeaponDefinition"));
            try
            {
                object state = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { data });
                state.GetType().GetMethod("TryAcceptShot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state, new object[] { 10f, .24f });
                object ammo = Get(state, "RoundsInMagazine"), reserve = Get(state, "ReserveRounds"), next = Get(state, "NextShotTime");
                object d = Driver(); Call(d, "RequestEquip", 10d, .38f); Call(d, "Sample", 10.4d); Call(d, "RequestUnequip", 10.4d, .26f); Call(d, "Sample", 11d);
                Assert.That(Get(state, "RoundsInMagazine"), Is.EqualTo(ammo)); Assert.That(Get(state, "ReserveRounds"), Is.EqualTo(reserve)); Assert.That(Get(state, "NextShotTime"), Is.EqualTo(next));
                Assert.That(Get(state, "ReloadState").ToString(), Is.EqualTo("Idle"));
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
