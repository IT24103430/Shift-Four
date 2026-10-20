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
            Assert.That(Get("Damage"), Is.EqualTo(25f));
            Assert.That(Get("Range"), Is.EqualTo(30f));
            Assert.That(Get("FireInterval"), Is.EqualTo(.24f));
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonfiniteData_UsesValidFallbacksEvenBeforeInspectorValidation(float value)
        {
            Set("damage", value); Set("range", value); Set("fireInterval", value);
            Assert.That(Get("Damage"), Is.EqualTo(25f)); Assert.That(Get("Range"), Is.EqualTo(30f));
            Assert.That(Get("FireInterval"), Is.EqualTo(.24f));
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
    }
}
