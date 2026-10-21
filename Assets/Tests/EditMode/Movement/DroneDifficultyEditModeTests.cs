using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class DroneDifficultyEditModeTests
    {
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Settings(Object profile) => profile.GetType().GetProperty("Settings").GetValue(profile);
        private static float Value(object settings, string field) => (float)settings.GetType().GetField(field).GetValue(settings);
        private static void Set(object settings, string field, float value) => settings.GetType().GetField(field).SetValue(settings, value);
        private static object Validate(object settings) => settings.GetType().GetMethod("Validated").Invoke(settings, null);
        private static Object Profile(string name) => AssetDatabase.LoadAssetAtPath(
            "Assets/Resources/DroneDifficulty/" + name + ".asset", T("DroneDifficultyProfile"));
        private static object Default => T("DroneDifficultySettings").GetProperty("Default").GetValue(null);

        [TestCase("Scout", 100f, 2.8f, 3.6f, 4.4f)]
        [TestCase("Guard", 150f, 2.4f, 3f, 3.6f)]
        [TestCase("Interceptor", 125f, 3f, 3.75f, 4.5f)]
        [TestCase("Flanker", 150f, 3.4f, 4.2f, 5f)]
        public void FourAuthoredDefaults_AreValidAndLoadThroughRuntimePath(string name, float hp,
            float patrol, float investigation, float chase)
        {
            Object profile = Profile(name);
            Assert.That(profile, Is.Not.Null);
            object archetype = Enum.Parse(T("DroneArchetype"), name);
            Assert.That(T("DroneDifficultyProfile").GetMethod("LoadDefault").Invoke(null, new[] { archetype }), Is.SameAs(profile));
            object data = Settings(profile);
            Assert.That(Value(data, "maximumHealth"), Is.EqualTo(hp));
            Assert.That(Value(data, "patrolSpeed"), Is.EqualTo(patrol));
            Assert.That(Value(data, "investigationSpeed"), Is.EqualTo(investigation));
            Assert.That(Value(data, "chaseSpeed"), Is.EqualTo(chase));
            Assert.That(Value(data, "acceleration"), Is.EqualTo(16f));
            Assert.That(Value(data, "angularSpeed"), Is.EqualTo(240f));
            Assert.That(Value(data, "hearingSensitivity"), Is.EqualTo(2f));
            Assert.That(Value(data, "visionRange"), Is.EqualTo(15f));
            Assert.That(Value(data, "suspiciousThreshold"), Is.EqualTo(.25f));
            Assert.That(Value(data, "investigateThreshold"), Is.EqualTo(.6f));
            Assert.That(Value(data, "combatThreshold"), Is.EqualTo(.85f));
            Assert.That(Value(data, "searchDuration"), Is.EqualTo(5f));
            object raw = T("DroneDifficultyProfile").GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(profile);
            Assert.That(JsonUtility.ToJson(raw), Is.EqualTo(JsonUtility.ToJson(data)), "Authored assets must already be valid");
        }

        [TestCase(-1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidNumbers_AreFiniteBoundedAndIdempotent(float invalid)
        {
            object data = Default;
            foreach (var field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) field.SetValue(data, invalid);
            AssertValid(Validate(data));
        }

        [TestCase(4f, 2f, 1f)] [TestCase(1f, 8f, 3f)] [TestCase(1f, -8f, 3f)]
        public void Speeds_AreOrderedWithInvestigationBetweenPatrolAndChase(float patrol, float investigation, float chase)
        {
            object data = Default;
            Set(data, "patrolSpeed", patrol); Set(data, "investigationSpeed", investigation); Set(data, "chaseSpeed", chase);
            object valid = Validate(data);
            Assert.That(Value(valid, "patrolSpeed"), Is.EqualTo(patrol));
            Assert.That(Value(valid, "chaseSpeed"), Is.GreaterThanOrEqualTo(patrol));
            AssertValid(valid);
        }

        [TestCase(2f, -2f, -2f)] [TestCase(.8f, .1f, .2f)]
        public void SuspicionThresholds_KeepStrictOrdering(float suspicious, float investigation, float combat)
        {
            object data = Default;
            Set(data, "suspiciousThreshold", suspicious); Set(data, "investigateThreshold", investigation); Set(data, "combatThreshold", combat);
            AssertValid(Validate(data));
        }

        [Test] public void ZeroHearing_IsAnIntentionalSupportedSetting()
        {
            object data = Default; Set(data, "hearingSensitivity", 0f);
            Assert.That(Value(Validate(data), "hearingSensitivity"), Is.Zero);
        }

        [Test] public void RuntimeSettings_AreACopyAndDoNotMutateSharedAsset()
        {
            Object profile = Profile("Guard"); string before = JsonUtility.ToJson(profile);
            object copy = Settings(profile); Set(copy, "maximumHealth", 999f);
            Assert.That(Value(Settings(profile), "maximumHealth"), Is.EqualTo(150f));
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(before));
        }

        [Test] public void InspectorOnValidate_NormalizesStoredData_RuntimeGetterIsAlsoDefensive()
        {
            Object profile = Object.Instantiate(Profile("Scout"));
            try
            {
                var stored = T("DroneDifficultyProfile").GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
                object invalid = Default;
                Set(invalid, "maximumHealth", -50f); Set(invalid, "chaseSpeed", -5f); Set(invalid, "searchDuration", float.NaN);
                stored.SetValue(profile, invalid);
                AssertValid(Settings(profile)); // Scripted invalid input is safe before OnValidate runs.
                T("DroneDifficultyProfile").GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(profile, null);
                AssertValid(stored.GetValue(profile));
                Assert.That(JsonUtility.ToJson(stored.GetValue(profile)), Is.EqualTo(JsonUtility.ToJson(Settings(profile))));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test] public void NewlyCreatedProfile_HasUsableConfiguration()
        {
            Object profile = ScriptableObject.CreateInstance(T("DroneDifficultyProfile"));
            try { AssertValid(Settings(profile)); }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test] public void UnknownArchetype_DoesNotSilentlyBindScout()
            => Assert.That(T("DroneDifficultyProfile").GetMethod("LoadDefault").Invoke(null,
                new[] { Enum.ToObject(T("DroneArchetype"), 99) }), Is.Null);

        private static void AssertValid(object data)
        {
            foreach (var field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                float value = (float)field.GetValue(data);
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False, field.Name);
            }
            Assert.That(Value(data, "maximumHealth"), Is.GreaterThanOrEqualTo(1f));
            Assert.That(Value(data, "patrolSpeed"), Is.GreaterThanOrEqualTo(.1f));
            Assert.That(Value(data, "investigationSpeed"), Is.InRange(Value(data, "patrolSpeed"), Value(data, "chaseSpeed")));
            Assert.That(Value(data, "acceleration"), Is.GreaterThanOrEqualTo(.1f));
            Assert.That(Value(data, "angularSpeed"), Is.GreaterThanOrEqualTo(1f));
            Assert.That(Value(data, "hearingSensitivity"), Is.InRange(0f, 4f));
            Assert.That(Value(data, "visionRange"), Is.GreaterThanOrEqualTo(.1f));
            Assert.That(Value(data, "suspiciousThreshold"), Is.InRange(.01f, .98f));
            Assert.That(Value(data, "investigateThreshold"), Is.GreaterThan(Value(data, "suspiciousThreshold")));
            Assert.That(Value(data, "combatThreshold"), Is.GreaterThan(Value(data, "investigateThreshold")).And.LessThanOrEqualTo(1f));
            Assert.That(Value(data, "searchDuration"), Is.GreaterThanOrEqualTo(.1f));
            Assert.That(JsonUtility.ToJson(Validate(data)), Is.EqualTo(JsonUtility.ToJson(data)), "Validation must be idempotent");
        }
    }

    public sealed class DroneDifficultyHealthEditModeTests
    {
        private GameObject root;
        private Component health;
        private int deaths;
        private static Type HealthType => Type.GetType("ShiftFour.Health, Assembly-CSharp", true);
        private object Call(string method, params object[] args) => HealthType.GetMethod(method).Invoke(health, args);
        private float Value(string property) => (float)HealthType.GetProperty(property).GetValue(health);
        private bool Initialize(float maximum) => (bool)Call("TryInitializeMaximum", maximum);

        [SetUp] public void Setup()
        {
            root = new GameObject("Difficulty health test"); health = root.AddComponent(HealthType);
            Call("Configure", 75f, false); deaths = 0;
            var died = HealthType.GetEvent("Died");
            died.AddEventHandler(health, Delegate.CreateDelegate(died.EventHandlerType, this,
                GetType().GetMethod(nameof(OnDied), BindingFlags.NonPublic | BindingFlags.Instance)));
        }
        private void OnDied(Component source) { deaths++; Assert.That(Initialize(999f), Is.False, "Reentrant binding cannot revive"); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [Test] public void FreshLife_InitializesMaximumOnlyOnce()
        {
            Assert.That(Initialize(150f), Is.True);
            Assert.That(Value("Maximum"), Is.EqualTo(150f)); Assert.That(Value("Current"), Is.EqualTo(150f));
            Assert.That(Initialize(300f), Is.False);
            Assert.That(Value("Maximum"), Is.EqualTo(150f)); Assert.That(Value("Current"), Is.EqualTo(150f));
            Assert.That(deaths, Is.Zero);
        }

        [Test] public void DamageAndReapplication_PreserveCurrentMaximumAndDamageRecord()
        {
            Initialize(150f); Call("TakeDamage", 25f);
            object hit = HealthType.GetProperty("LastDamage").GetValue(health);
            Assert.That(Initialize(300f), Is.False);
            Assert.That(Value("Current"), Is.EqualTo(125f)); Assert.That(Value("Maximum"), Is.EqualTo(150f));
            Assert.That(HealthType.GetProperty("LastDamage").GetValue(health), Is.EqualTo(hit));
        }

        [Test] public void DamageBeforeFirstBinding_IsNeverErased()
        {
            Call("TakeDamage", 25f); Assert.That(Initialize(150f), Is.False);
            Assert.That(Value("Current"), Is.EqualTo(50f)); Assert.That(Value("Maximum"), Is.EqualTo(75f));
        }

        [Test] public void FatalDamage_FiresDiedExactlyOnceAndCannotBeRevivedByProfile()
        {
            Initialize(125f); Call("TakeDamage", 125f); Call("TakeDamage", 100f);
            Assert.That(Initialize(150f), Is.False); Call("TakeDamage", 100f);
            Assert.That(Value("Current"), Is.Zero); Assert.That(Value("Maximum"), Is.EqualTo(125f));
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test] public void ExplicitLegacyConfigure_StartsFreshLifeAndKeepsRecoveryOption()
        {
            Initialize(100f); Call("TakeDamage", 100f);
            Call("Configure", 75f, true);
            Assert.That(Initialize(150f), Is.True); Assert.That(Value("Current"), Is.EqualTo(150f));
            Assert.That(HealthType.GetField("regenerate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(health), Is.True);
            Call("TakeDamage", 150f); Assert.That(deaths, Is.EqualTo(2), "One fatal notification per explicitly reset life");
        }

        [TestCase(-1f)] [TestCase(0f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidMaximum_IsRejectedWithoutConsumingInitialization(float invalid)
        {
            Assert.That(Initialize(invalid), Is.False);
            Assert.That(Value("Current"), Is.EqualTo(75f)); Assert.That(Value("Maximum"), Is.EqualTo(75f));
            Assert.That(Initialize(125f), Is.True);
        }

        [Test] public void IgnoredDamage_DoesNotPreventFreshInitialization()
        {
            Call("TakeDamage", 0f); Call("TakeDamage", -10f);
            Assert.That(Initialize(125f), Is.True); Assert.That(Value("Current"), Is.EqualTo(125f));
        }
    }
}
