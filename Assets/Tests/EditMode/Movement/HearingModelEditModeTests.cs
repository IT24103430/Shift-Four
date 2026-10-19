using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class HearingModelEditModeTests
    {
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static float Strength(float distance, float intensity = 1f, float sensitivity = 1f, float transmission = 1f)
            => (float)T("HearingModel").GetMethod("Strength").Invoke(null, new object[] { distance, intensity, sensitivity, 26f, transmission });
        [TestCase(.2f, 4.94f)] [TestCase(.45f, 11.44f)] [TestCase(1f, 25.74f)] [TestCase(.4f, 10.14f)]
        public void InitialIntensities_ReachRequestedOpenAirDistanceTargets(float intensity, float radius)
        {
            Assert.That(Strength(radius - .02f, intensity), Is.GreaterThan(.01f));
            Assert.That(Strength(radius + .02f, intensity), Is.LessThan(.01f));
        }
        [Test] public void DistanceWallsAndSensitivity_AttenuateIndependently()
        {
            Assert.That(Strength(2f), Is.GreaterThan(Strength(10f)));
            Assert.That(Strength(2f, transmission: .25f), Is.LessThan(Strength(2f)));
            Assert.That(Strength(2f, .2f, transmission: .25f), Is.Zero);
            Assert.That(Strength(10f, .2f, 2f), Is.GreaterThan(Strength(10f, .2f)));
            Assert.That(Strength(0f, sensitivity: 0f), Is.Zero);
            Assert.That(Strength(0f, transmission: 0f), Is.Zero);
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDistance_CannotBeDetected(float distance) => Assert.That(Strength(distance), Is.Zero);
        [Test] public void Uncertainty_IsDeterministicBoundedAndDoesNotChangeGlobalRandom()
        {
            var estimate = T("HearingModel").GetMethod("Estimate"); Vector3 source = new Vector3(3, 2, 7);
            var before = UnityEngine.Random.state;
            Vector3 first = (Vector3)estimate.Invoke(null, new object[] { source, 2f, 123 });
            Assert.That((Vector3)estimate.Invoke(null, new object[] { source, 2f, 123 }), Is.EqualTo(first));
            Assert.That(Vector3.Distance(first, source), Is.InRange(1f, 1.9f)); Assert.That(first.y, Is.EqualTo(source.y));
            Assert.That((Vector3)estimate.Invoke(null, new object[] { source, 2f, 456 }), Is.Not.EqualTo(first));
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(before)));
        }
        [Test] public void Observation_ContainsNoSourceVisualAuthorizationOrLiveTarget()
        {
            Type type = T("SoundObservation"); Assert.That(type.IsValueType, Is.True);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(field.IsInitOnly, Is.True);
                Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType), Is.False);
                Assert.That(field.Name, Is.Not.EqualTo("Source"));
            }
            var observation = Activator.CreateInstance(type, Vector3.one, Enum.Parse(T("NoiseCategory"), "Gunshot"), .4f, 2f, 10f, 15f);
            foreach (var sample in new[] { (9f, false), (10f, true), (14.99f, true), (15f, false), (float.NaN, false) })
                Assert.That(type.GetMethod("IsFresh").Invoke(observation, new object[] { sample.Item1 }), Is.EqualTo(sample.Item2));
        }
        [TestCase(-100f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidSettings_AreFiniteAndBounded(float value)
        {
            var config = Activator.CreateInstance(T("HearingSettings"));
            foreach (var field in config.GetType().GetFields()) if (field.FieldType == typeof(float)) field.SetValue(config, value);
            config.GetType().GetMethod("Validate").Invoke(config, null);
            foreach (var field in config.GetType().GetFields()) if (field.FieldType == typeof(float))
            {
                float actual = (float)field.GetValue(config);
                Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False); Assert.That(actual, Is.GreaterThanOrEqualTo(0f));
            }
        }
    }
}
