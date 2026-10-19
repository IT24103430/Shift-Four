using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class DroneAnimationEditModeTests
    {
        private static readonly Type PoseType = Type.GetType("ShiftFour.DroneAnimationPose, Assembly-CSharp", true);
        private static object New() => Activator.CreateInstance(PoseType);
        private static void Call(object pose, string method, params object[] args) => PoseType.GetMethod(method).Invoke(pose, args);
        private static float Value(object pose, string property) => (float)PoseType.GetProperty(property).GetValue(pose);
        private static string State(object pose) => PoseType.GetProperty("State").GetValue(pose).ToString();
        private static void Step(object pose, float dt, Vector3 velocity, float yaw = 0f, bool alive = true)
            => Call(pose, "Step", dt, velocity, yaw, alive);

        [Test]
        public void IdleHover_IsBoundedAndHasNoTilt()
        {
            object pose = New();
            for (int i = 0; i < 600; i++)
            {
                Step(pose, 1f / 60f, Vector3.zero);
                Assert.That(Mathf.Abs(Value(pose, "Hover")), Is.LessThanOrEqualTo(.035001f));
                Assert.That(State(pose), Is.EqualTo("Idle"));
            }
            Assert.That(Value(pose, "Pitch"), Is.Zero);
            Assert.That(Mathf.Abs(Value(pose, "Bank")), Is.LessThanOrEqualTo(.12f));
        }

        [Test]
        public void MovementTurnBrakingArrival_FollowMotion()
        {
            object pose = New();
            Step(pose, .1f, Vector3.forward);
            Assert.That(State(pose), Is.EqualTo("Moving"));
            Assert.That(Value(pose, "Pitch"), Is.LessThan(0f));
            Step(pose, .1f, Vector3.forward, 90f);
            Assert.That(State(pose), Is.EqualTo("Turning"));
            Assert.That(Value(pose, "Bank"), Is.LessThan(0f));
            Step(pose, .1f, Vector3.zero);
            Assert.That(State(pose), Is.EqualTo("Braking"));
            for (int i = 0; i < 120; i++) Step(pose, 1f / 60f, Vector3.zero);
            Assert.That(State(pose), Is.EqualTo("Idle"));
            Assert.That(Mathf.Abs(Value(pose, "Pitch")), Is.LessThan(.001f));
        }

        [Test]
        public void SmallSymmetricSpeedNoise_DoesNotBecomePerpetualBraking()
        {
            object pose = New();
            for (int i = 0; i < 960; i++)
            {
                Step(pose, 1f / 240f, Vector3.forward * (i % 2 == 0 ? 1.003f : .997f));
                if (i > 720) Assert.That(State(pose), Is.EqualTo("Moving"));
            }
        }

        [Test]
        public void DamageOverridesFire_DeathOverridesEverythingAndCannotRetrigger()
        {
            object pose = New();
            Call(pose, "Fire"); Step(pose, .01f, Vector3.forward);
            Assert.That(State(pose), Is.EqualTo("Firing"));
            Call(pose, "Hit", false); Step(pose, .01f, Vector3.forward);
            Assert.That(State(pose), Is.EqualTo("Damaged"));
            Call(pose, "Hit", true);
            Assert.That(State(pose), Is.EqualTo("Shutdown"));
            Call(pose, "Fire"); Call(pose, "Hit", false);
            Step(pose, .01f, Vector3.forward, 120f, false);
            Assert.That(State(pose), Is.EqualTo("Shutdown"));
            Assert.That(Value(pose, "FireFlash"), Is.Zero);
            Assert.That(Value(pose, "HitFlash"), Is.Zero);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void SteadyFlight_DampingAndRotorTravelAgreeAcrossFrameRates(int fps)
        {
            object pose = New(), reference = New();
            float travel = 0f, expectedTravel = 0f;
            for (int i = 0; i < fps * 2; i++)
            {
                Step(pose, 1f / fps, Vector3.forward, 75f);
                travel += Value(pose, "RotorTravel");
            }
            for (int i = 0; i < 480; i++)
            {
                Step(reference, 1f / 240f, Vector3.forward, 75f);
                expectedTravel += Value(reference, "RotorTravel");
            }
            foreach (string p in new[] { "Pitch", "Bank", "Hover", "RotorSpeed" })
                Assert.That(Value(pose, p), Is.EqualTo(Value(reference, p)).Within(.0001f), p);
            Assert.That(travel, Is.EqualTo(expectedTravel).Within(.0001f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void Shutdown_SettlesAndSpinsDownConsistently(int fps)
        {
            object pose = New();
            float travel = 0f;
            for (int i = 0; i < fps * 3; i++)
            {
                Step(pose, 1f / fps, Vector3.forward, 120f, false);
                travel += Value(pose, "RotorTravel");
            }
            Assert.That(State(pose), Is.EqualTo("Shutdown"));
            Assert.That(Value(pose, "RotorSpeed"), Is.Zero);
            Assert.That(Value(pose, "ShutdownBlend"), Is.GreaterThan(.999f));
            Assert.That(Mathf.Abs(Value(pose, "Hover")), Is.LessThan(.0001f));
            Assert.That(travel, Is.EqualTo(.8f / 4f).Within(.0003f));
        }

        [Test]
        public void PausedTime_DoesNotAdvancePoseOrFeedback()
        {
            object pose = New(); Call(pose, "Fire");
            Step(pose, 0f, Vector3.forward, 120f);
            Assert.That(Value(pose, "FireFlash"), Is.EqualTo(1f));
            Assert.That(Value(pose, "Pitch"), Is.Zero);
            Assert.That(Value(pose, "RotorTravel"), Is.Zero);
        }
    }
}
