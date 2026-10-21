using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class WeaponArchitectureEditModeTests
    {
        private ScriptableObject definition;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private object Get(string name) => definition.GetType().GetProperty(name).GetValue(definition);
        private void Set(string name, object value) => definition.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, value);

        [SetUp] public void Setup() => definition = ScriptableObject.CreateInstance(T("WeaponDefinition"));
        [TearDown] public void Cleanup() => Object.DestroyImmediate(definition);

        [Test] public void FallbackDefinition_PreservesBaselineHitscanValues()
        {
            Assert.That(Get("Damage"), Is.EqualTo(22f));
            Assert.That(Get("Range"), Is.EqualTo(30f));
            Assert.That(Get("FireInterval"), Is.EqualTo(.4f));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonfiniteData_UsesValidFallbacksEvenBeforeInspectorValidation(float value)
        {
            Set("damage", value); Set("range", value); Set("fireInterval", value);
            Assert.That(Get("Damage"), Is.EqualTo(22f)); Assert.That(Get("Range"), Is.EqualTo(30f));
            Assert.That(Get("FireInterval"), Is.EqualTo(.4f));
        }

        [TestCase(0f)] [TestCase(-100f)] public void NonpositiveData_CannotCreateZeroCooldownOrNegativeDamage(float value)
        {
            Set("damage", value); Set("range", value); Set("fireInterval", value);
            Assert.That((float)Get("Damage"), Is.GreaterThan(0f)); Assert.That((float)Get("Range"), Is.GreaterThan(0f));
            Assert.That((float)Get("FireInterval"), Is.GreaterThan(0f));
        }

        [Test] public void Definition_ContainsAuthoredTuningAndRuntimeStateIsSeparate()
        {
            var data = new SerializedObject(definition);
            data.FindProperty("damage").floatValue = 37f; data.FindProperty("range").floatValue = 18f;
            data.FindProperty("fireInterval").floatValue = .4f; data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Get("Damage"), Is.EqualTo(37f)); Assert.That(Get("Range"), Is.EqualTo(18f));
            Assert.That(Get("FireInterval"), Is.EqualTo(.4f));
            object a = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            object b = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            string before = JsonUtility.ToJson(definition);
            var accept = a.GetType().GetMethod("TryAcceptShot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(accept.Invoke(a, new object[] { 10f, .4f }), Is.True);
            Assert.That(accept.Invoke(a, new object[] { 10.39f, .4f }), Is.False);
            Assert.That(accept.Invoke(a, new object[] { 10.4f, .4f }), Is.True);
            Assert.That(b.GetType().GetProperty("NextShotTime").GetValue(b), Is.EqualTo(0f));
            Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
        }

        [Test] public void Player_NoLongerOwnsShootMethodOrWeaponCooldown()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(T("PlayerController").GetMethod("Shoot", flags), Is.Null);
            Assert.That(T("PlayerController").GetField("nextShot", flags), Is.Null);
            Assert.That(T("PlayerController").GetField("gunDamage", flags), Is.Null);
            Assert.That(T("PlayerController").GetField("gunRange", flags), Is.Null);
            Assert.That(T("PlayerController").GetField("fireInterval", flags), Is.Null);
        }

        [Test] public void AmmoTuning_ClampsInvalidValuesAndUsesFiniteReloadTime()
        {
            Set("magazineCapacity", 0); Set("startingReserve", -10); Set("reloadDuration", float.NaN);
            Assert.That(Get("MagazineCapacity"), Is.EqualTo(1)); Assert.That(Get("StartingReserve"), Is.EqualTo(0));
            Assert.That(Get("ReloadDuration"), Is.EqualTo(1.6f));
            Set("reloadDuration", float.PositiveInfinity); Assert.That(Get("ReloadDuration"), Is.EqualTo(1.6f));
            Set("reloadDuration", -1f); Assert.That(Get("ReloadDuration"), Is.EqualTo(.1f));
        }

        [Test] public void Reload_ConservesRoundsAndTransfersOnlyAtDeadline()
        {
            Set("magazineCapacity", 3); Set("startingReserve", 2); Set("reloadDuration", 1f);
            object state = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            object Invoke(string method, params object[] args) => state.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(state, args);
            object Value(string property) => state.GetType().GetProperty(property).GetValue(state);
            string before = JsonUtility.ToJson(definition);
            Assert.That(Invoke("BeginReload", 0f), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(Invoke("TryAcceptShot", (float)i, .1f), Is.True);
            Assert.That(Invoke("TryAcceptShot", 3f, .1f), Is.False);
            Assert.That(Invoke("BeginReload", 3f), Is.True);
            Assert.That(Invoke("EndReload", 3.99f), Is.False); Assert.That(Value("ReserveRounds"), Is.EqualTo(2));
            Assert.That(Invoke("EndReload", 4f), Is.True); Assert.That(Invoke("EndReload", 4f), Is.False);
            Assert.That(Value("RoundsInMagazine"), Is.EqualTo(2)); Assert.That(Value("ReserveRounds"), Is.EqualTo(0));
            Assert.That(Invoke("BeginReload", 5f), Is.False);
            Assert.That(JsonUtility.ToJson(definition), Is.EqualTo(before));
        }

        [Test] public void AmmoRuntime_IsPerPlayer_AndCancelPreservesBothCounters()
        {
            object a = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            object b = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            a.GetType().GetMethod("TryAcceptShot", flags).Invoke(a, new object[] { 1f, .24f });
            a.GetType().GetMethod("BeginReload", flags).Invoke(a, new object[] { 1f });
            a.GetType().GetMethod("CancelReload", flags).Invoke(a, null);
            Assert.That(a.GetType().GetProperty("RoundsInMagazine").GetValue(a), Is.EqualTo(7));
            Assert.That(a.GetType().GetProperty("ReserveRounds").GetValue(a), Is.EqualTo(80));
            Assert.That(b.GetType().GetProperty("RoundsInMagazine").GetValue(b), Is.EqualTo(8));
        }
    }
}
