#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class DroneMovementSpeedsEditModeTests
    {
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Mode(string name) => Enum.Parse(T("DroneMovementMode"), name);
        private static object Settings(string name) => T("DroneDifficultyProfile").GetProperty("Settings")
            .GetValue(Resources.Load("DroneDifficulty/" + name));
        private static object Controller(string name = "Flanker") => Activator.CreateInstance(T("DroneMovementSpeeds"), Settings(name));
        private static object Call(object c, string name, params object[] args) => c.GetType().GetMethod(name).Invoke(c, args);
        private static float Value(object c, string name) => (float)c.GetType().GetProperty(name).GetValue(c);

        [TestCase("Scout", 1.8f, 2.2f, 2.6f)]
        [TestCase("Guard", 2f, 2.5f, 3f)]
        [TestCase("Interceptor", 2.5f, 3.1f, 3.7f)]
        [TestCase("Flanker", 2.8f, 3.5f, 4.2f)]
        public void EveryProfile_HasCorrectTargetsAndStartsAtPatrol(string name, float patrol, float investigation, float chase)
        {
            object c = Controller(name);
            Assert.That(Value(c, "Current"), Is.EqualTo(patrol));
            foreach (var pair in new[] { ("Patrol", patrol), ("Investigate", investigation), ("Chase", chase) })
            {
                Call(c, "SetMode", Mode(pair.Item1));
                Assert.That(Value(c, "Target"), Is.EqualTo(pair.Item2));
            }
            Assert.That(investigation, Is.EqualTo((patrol + chase) * .5f).Within(.00001f));
            Assert.That(Value(c, "AnimationReference"), Is.EqualTo(chase));
        }

        [TestCase("Patrol", "Patrol")]
        [TestCase("Suspicious", "Patrol")]
        [TestCase("Investigate", "Investigate")]
        [TestCase("Search", "Investigate")]
        [TestCase("Combat", "Chase")]
        [TestCase("ReturnToPost", "Patrol")]
        public void EveryAwarenessState_SelectsMovementOnly(string awareness, string movement)
            => Assert.That(T("DroneMovementSpeeds").GetMethod("FromAwareness").Invoke(null,
                new[] { Enum.Parse(T("DroneAwarenessState"), awareness) }).ToString(), Is.EqualTo(movement));

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void AccelerationAndDeceleration_AreBoundedAndFrameRateIndependent(int hz)
        {
            object c = Controller();
            foreach (string mode in new[] { "Chase", "Patrol", "Investigate", "Chase" })
            {
                float initial = Value(c, "Current");
                Call(c, "SetMode", Mode(mode));
                Assert.That(Value(c, "Current"), Is.EqualTo(initial), "Mode selection must not jump the limit");
                float target = Value(c, "Target");
                for (int i = 0; i < hz; i++)
                {
                    float before = Value(c, "Current");
                    float next = (float)Call(c, "Step", 1f / hz, .8f);
                    Assert.That(Mathf.Abs(next - before), Is.LessThanOrEqualTo(.8f / hz + .000001f));
                    Assert.That(next, Is.InRange(Mathf.Min(initial, target), Mathf.Max(initial, target)));
                }
                Assert.That(Value(c, "Current"), Is.EqualTo(Mathf.MoveTowards(initial, target, .8f)).Within(.00002f));
            }
        }

        [TestCase(-1f)] [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTimeOrAcceleration_DoesNotCorruptLimit(float invalid)
        {
            object c = Controller(); Call(c, "SetMode", Mode("Chase"));
            Assert.That(Call(c, "Step", invalid, 8f), Is.EqualTo(2.8f));
            Assert.That(Call(c, "Step", .1f, invalid), Is.EqualTo(2.8f));
        }

        [Test] public void InvalidProfile_IsValidatedAndDoesNotMutateInput()
        {
            object settings = Settings("Scout");
            settings.GetType().GetField("patrolSpeed").SetValue(settings, float.NaN);
            settings.GetType().GetField("chaseSpeed").SetValue(settings, -5f);
            object c = Activator.CreateInstance(T("DroneMovementSpeeds"), settings);
            Call(c, "SetMode", Mode("Chase")); Call(c, "Step", 1f, 8f);
            Assert.That(Value(c, "Current"), Is.EqualTo(1.8f));
            Assert.That((float)settings.GetType().GetField("patrolSpeed").GetValue(settings), Is.NaN);
        }
    }
}
#endif
