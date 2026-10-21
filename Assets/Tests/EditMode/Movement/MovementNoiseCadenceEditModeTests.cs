using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class MovementNoiseCadenceEditModeTests
    {
        private static Type Cadence => Type.GetType("ShiftFour.MovementNoiseCadence, Assembly-CSharp", true);
        private object cadence;
        private object Call(string method, params object[] args) => Cadence.GetMethod(method).Invoke(cadence, args);
        private bool Step(Vector3 displacement, bool ground = true, bool moving = true, bool sprint = false, float dt = .02f)
            => (bool)Call("Advance", displacement, ground, moving, sprint, dt);
        [SetUp] public void Setup() => cadence = Activator.CreateInstance(Cadence);

        [Test] public void WalkingAndFutureSprint_HaveDistinctQuietAndLoudDefaults()
        {
            Assert.That(Call("Intensity", false), Is.EqualTo(.2f));
            Assert.That(Call("Intensity", true), Is.EqualTo(.45f));
        }

        [TestCase(false, 31)] [TestCase(true, 23)]
        public void Cadence_IsDistanceBasedAndNeverEmitsEveryFrame(bool sprint, int expected)
        {
            int events = 0;
            for (int i = 0; i < 500; i++) if (Step(Vector3.forward * .1f, sprint: sprint)) events++;
            Assert.That(events, Is.InRange(expected - 1, expected + 1));
        }

        [Test] public void FastMovement_IsCappedByMinimumIntervalAndDiscardsCatchup()
        {
            int events = 0;
            for (int i = 0; i < 100; i++) if (Step(Vector3.forward * .4f)) events++;
            Assert.That(events, Is.InRange(7, 9));
            Assert.That(Step(Vector3.zero), Is.False);
        }

        [TestCase(false, true)] [TestCase(true, false)]
        public void AirborneOrPassiveMotion_DoesNotAccumulateSteps(bool ground, bool moving)
        {
            for (int i = 0; i < 500; i++) Assert.That(Step(Vector3.forward * .1f, ground, moving), Is.False);
            Assert.That(Step(Vector3.forward * .1f), Is.False);
        }

        [Test] public void IdleAndReset_ClearPartialStepProgress()
        {
            for (int i = 0; i < 15; i++) Assert.That(Step(Vector3.forward * .1f), Is.False);
            Step(Vector3.zero);
            Assert.That(Step(Vector3.forward * .1f), Is.False);
            Call("Reset");
            for (int i = 0; i < 15; i++) Assert.That(Step(Vector3.forward * .1f), Is.False);
        }

        [Test] public void VerticalMovementAndContactJitter_AreSilent()
        {
            for (int i = 0; i < 500; i++)
            {
                Assert.That(Step(Vector3.up), Is.False);
                Assert.That(Step(Vector3.right * .0001f), Is.False);
            }
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(.5f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidOrStalledTime_DoesNotAccumulateOrBurst(float dt)
        {
            Assert.That(Step(Vector3.forward * 50f, dt: dt), Is.False);
            Assert.That(Step(Vector3.forward * .1f), Is.False);
        }

        [Test] public void WarpsAndInvalidDisplacement_DoNotCreateSteps()
        {
            Assert.That(Step(Vector3.forward * 50f), Is.False);
            Assert.That(Step(new Vector3(float.NaN, 0, 0)), Is.False);
            Assert.That(Step(new Vector3(0, 0, float.PositiveInfinity)), Is.False);
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidInspectorConfiguration_IsFiniteAndBounded(float value)
        {
            foreach (string name in new[] { "walkStepDistance", "sprintStepDistance", "minimumInterval", "walkingIntensity", "sprintingIntensity" })
                Cadence.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cadence, value);
            Call("Validate");
            foreach (var field in Cadence.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                float actual = (float)field.GetValue(cadence);
                Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False);
                Assert.That(actual, Is.GreaterThanOrEqualTo(0f));
            }
            Assert.That((float)Call("Intensity", false), Is.InRange(0f, 1f));
            Assert.That((float)Call("Intensity", true), Is.InRange(0f, 1f));
        }
    }
}
