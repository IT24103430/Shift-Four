using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class SoundEventsEditModeTests
    {
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Type Bus => T("SoundEvents");
        private static Type NoiseType => T("NoiseEvent");
        private static EventInfo NoiseEvent => Bus.GetEvent("NoiseEmitted");
        private static EventInfo ShotEvent => Bus.GetEvent("ShotFired");
        private static object Category(string name) => Enum.Parse(T("NoiseCategory"), name);
        private static object Noise(string category = "Gunshot", float intensity = 1f, float timestamp = 12.5f,
            Vector3? position = null, Object source = null)
            => Activator.CreateInstance(NoiseType, position ?? new Vector3(1f, 2f, 3f), Category(category), intensity, timestamp, source);
        private static object Field(object noise, string name) => NoiseType.GetField(name).GetValue(noise);
        private static bool Report(object noise) => (bool)Bus.GetMethod("ReportNoise", new[] { NoiseType }).Invoke(null, new[] { noise });
        private static void Shot(Vector3 position) => Bus.GetMethod("ReportShot").Invoke(null, new object[] { position });
        private static Delegate Listen(Action<object> callback)
        {
            var value = Expression.Parameter(NoiseType, "noise");
            return Expression.Lambda(NoiseEvent.EventHandlerType,
                Expression.Invoke(Expression.Constant(callback), Expression.Convert(value, typeof(object))), value).Compile();
        }
        private static void Reset() => Bus.GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [SetUp] public void Setup() => Reset();
        [TearDown] public void Cleanup() => Reset();

        [TestCase("Footstep", .2f)] [TestCase("Sprint", .45f)] [TestCase("Gunshot", 1f)]
        [TestCase("ConsoleInteraction", .4f)] [TestCase("Impact", .3f)]
        public void Categories_DefaultStrengthAndPayloadAreCorrect(string category, float expected)
        {
            float intensity = (float)Bus.GetMethod("DefaultIntensity").Invoke(null, new[] { Category(category) });
            Assert.That(intensity, Is.EqualTo(expected));
            object received = null; int calls = 0;
            NoiseEvent.AddEventHandler(null, Listen(value => { received = value; calls++; }));
            object noise = Noise(category, intensity);
            Assert.That(Report(noise), Is.True);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Field(received, "Position"), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(Field(received, "Category").ToString(), Is.EqualTo(category));
            Assert.That(Field(received, "Intensity"), Is.EqualTo(expected));
            Assert.That(Field(received, "Timestamp"), Is.EqualTo(12.5f));
            Assert.That(Field(received, "Source"), Is.Null);
        }

        [Test] public void PayloadIsImmutableAndPositionIsAnEmissionSnapshot()
        {
            var source = new GameObject("Noise identity");
            try
            {
                source.transform.position = new Vector3(1, 2, 3);
                object noise = Noise(source: source, position: source.transform.position);
                source.transform.position = Vector3.one * 99f;
                Assert.That(NoiseType.IsValueType, Is.True);
                foreach (var field in NoiseType.GetFields(BindingFlags.Public | BindingFlags.Instance)) Assert.That(field.IsInitOnly, Is.True, field.Name);
                Assert.That(Field(noise, "Source"), Is.SameAs(source));
                Assert.That(Field(noise, "Position"), Is.EqualTo(new Vector3(1, 2, 3)));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test] public void LegacyReportShot_UsesOneModernAndOneLegacyNotification()
        {
            int noises = 0, shots = 0; object received = null;
            NoiseEvent.AddEventHandler(null, Listen(value => { noises++; received = value; }));
            ShotEvent.AddEventHandler(null, (Action<Vector3>)(position => { Assert.That(position, Is.EqualTo(Vector3.right)); shots++; }));
            Shot(Vector3.right);
            Assert.That(noises, Is.EqualTo(1)); Assert.That(shots, Is.EqualTo(1));
            Assert.That(Field(received, "Category").ToString(), Is.EqualTo("Gunshot"));
            Assert.That(Field(received, "Intensity"), Is.EqualTo(1f));
            Assert.That(Field(received, "Source"), Is.Null);
        }

        [TestCase("Footstep", 0)] [TestCase("Sprint", 0)] [TestCase("Gunshot", 1)]
        [TestCase("ConsoleInteraction", 0)] [TestCase("Impact", 0)]
        public void OnlyGunshotsBridgeToLegacyChannel(string category, int expectedShots)
        {
            int noises = 0, shots = 0;
            NoiseEvent.AddEventHandler(null, Listen(_ => noises++));
            ShotEvent.AddEventHandler(null, (Action<Vector3>)(_ => shots++));
            Assert.That(Report(Noise(category)), Is.True);
            Assert.That(noises, Is.EqualTo(1)); Assert.That(shots, Is.EqualTo(expectedShots));
        }

        [Test] public void MultipleListeners_AreUniqueOrderedAndCanUnsubscribeRepeatedly()
        {
            var order = new List<string>();
            var first = Listen(_ => order.Add("noise A")); var second = Listen(_ => order.Add("noise B"));
            Action<Vector3> shotA = _ => order.Add("shot A"), shotB = _ => order.Add("shot B");
            NoiseEvent.AddEventHandler(null, first); NoiseEvent.AddEventHandler(null, second); NoiseEvent.AddEventHandler(null, first);
            ShotEvent.AddEventHandler(null, shotA); ShotEvent.AddEventHandler(null, shotB); ShotEvent.AddEventHandler(null, shotA);
            Report(Noise());
            Assert.That(order, Is.EqualTo(new[] { "noise A", "noise B", "shot A", "shot B" }));
            order.Clear(); NoiseEvent.RemoveEventHandler(null, first); NoiseEvent.RemoveEventHandler(null, first);
            ShotEvent.RemoveEventHandler(null, shotA); ShotEvent.RemoveEventHandler(null, shotA);
            Report(Noise()); Assert.That(order, Is.EqualTo(new[] { "noise B", "shot B" }));
        }

        [Test] public void MulticastRegistrationAndNullHandlers_AreSafeAndDoNotDuplicateCallbacks()
        {
            int count = 0; var a = Listen(_ => count++); var b = Listen(_ => count++);
            var combined = Delegate.Combine(a, b);
            NoiseEvent.AddEventHandler(null, null); NoiseEvent.RemoveEventHandler(null, null);
            ShotEvent.AddEventHandler(null, null); ShotEvent.RemoveEventHandler(null, null);
            NoiseEvent.AddEventHandler(null, combined); NoiseEvent.AddEventHandler(null, a);
            Report(Noise()); Assert.That(count, Is.EqualTo(2));
            NoiseEvent.RemoveEventHandler(null, combined); Report(Noise()); Assert.That(count, Is.EqualTo(2));
        }

        [Test] public void ListenerChanges_ApplyToNextLogicalEventAcrossBothChannels()
        {
            var order = new List<string>();
            var b = Listen(_ => order.Add("B")); var c = Listen(_ => order.Add("C"));
            Action<Vector3> oldShot = _ => order.Add("old shot"), newShot = _ => order.Add("new shot");
            var a = Listen(_ =>
            {
                order.Add("A"); NoiseEvent.RemoveEventHandler(null, b); NoiseEvent.AddEventHandler(null, c);
                ShotEvent.RemoveEventHandler(null, oldShot); ShotEvent.AddEventHandler(null, newShot);
            });
            NoiseEvent.AddEventHandler(null, a); NoiseEvent.AddEventHandler(null, b); ShotEvent.AddEventHandler(null, oldShot);
            Report(Noise()); Assert.That(order, Is.EqualTo(new[] { "A", "B", "old shot" }));
            order.Clear(); Report(Noise()); Assert.That(order, Is.EqualTo(new[] { "A", "C", "new shot" }));
        }

        [Test] public void ReentrantReporting_IsSynchronousAndDepthFirst()
        {
            var order = new List<string>();
            NoiseEvent.AddEventHandler(null, Listen(noise =>
            {
                float time = (float)Field(noise, "Timestamp"); order.Add("A" + time);
                if (time == 1f) Report(Noise(timestamp: 2f, position: Vector3.right * 2f));
            }));
            NoiseEvent.AddEventHandler(null, Listen(noise => order.Add("B" + Field(noise, "Timestamp"))));
            ShotEvent.AddEventHandler(null, (Action<Vector3>)(position => order.Add("S" + position.x)));
            Report(Noise(timestamp: 1f, position: Vector3.right));
            Assert.That(order, Is.EqualTo(new[] { "A1", "A2", "B2", "S2", "B1", "S1" }));
        }

        [Test] public void TwoReportsInSameFrameRemainTwoDistinctDisturbances()
        {
            int noises = 0, shots = 0;
            NoiseEvent.AddEventHandler(null, Listen(_ => noises++)); ShotEvent.AddEventHandler(null, (Action<Vector3>)(_ => shots++));
            object same = Noise(); Report(same); Report(same);
            Assert.That(noises, Is.EqualTo(2)); Assert.That(shots, Is.EqualTo(2), "Do not drop simultaneous shots by position/time");
        }

        [TestCase(0f)] [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidOrSilentIntensity_IsRejectedWithoutEitherNotification(float intensity)
            => AssertRejected(Noise(intensity: intensity));

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidTimestamp_IsRejected(float timestamp) => AssertRejected(Noise(timestamp: timestamp));

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidPosition_IsRejected(float invalid)
        {
            foreach (Vector3 position in new[] { new Vector3(invalid, 0, 0), new Vector3(0, invalid, 0), new Vector3(0, 0, invalid) })
                AssertRejected(Noise(position: position));
        }

        [Test] public void InvalidCategoryAndDefaultStruct_AreRejected()
        {
            object invalid = Activator.CreateInstance(NoiseType, Vector3.zero, Enum.ToObject(T("NoiseCategory"), 99), 1f, 0f, null);
            AssertRejected(invalid); AssertRejected(Activator.CreateInstance(NoiseType));
            Assert.That(Bus.GetMethod("DefaultIntensity").Invoke(null, new[] { Enum.ToObject(T("NoiseCategory"), -1) }), Is.EqualTo(0f));
        }

        [Test] public void ZeroTimestamp_IsValidForDeterministicSessionStart()
            => Assert.That(Report(Noise(timestamp: 0f)), Is.True);

        private static void AssertRejected(object noise)
        {
            int calls = 0; var handler = Listen(_ => calls++); Action<Vector3> legacy = _ => calls++;
            NoiseEvent.AddEventHandler(null, handler); ShotEvent.AddEventHandler(null, legacy);
            Assert.That(Report(noise), Is.False); Assert.That(calls, Is.Zero);
            NoiseEvent.RemoveEventHandler(null, handler); ShotEvent.RemoveEventHandler(null, legacy);
        }

        [Test] public void FaultingModernListener_IsReportedOnceAndDoesNotBlockLaterOrLegacyListeners()
        {
            int failures = 0, healthy = 0, shots = 0;
            NoiseEvent.AddEventHandler(null, Listen(_ => { failures++; throw new InvalidOperationException("noise observer probe"); }));
            NoiseEvent.AddEventHandler(null, Listen(_ => healthy++)); ShotEvent.AddEventHandler(null, (Action<Vector3>)(_ => shots++));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: noise observer probe"));
            Report(Noise()); Report(Noise());
            Assert.That(failures, Is.EqualTo(1)); Assert.That(healthy, Is.EqualTo(2)); Assert.That(shots, Is.EqualTo(2));
        }

        [Test] public void FaultingLegacyListener_IsReportedOnceAndDoesNotBlockLaterLegacyListeners()
        {
            int failures = 0, healthy = 0;
            ShotEvent.AddEventHandler(null, (Action<Vector3>)(_ => { failures++; throw new InvalidOperationException("legacy observer probe"); }));
            ShotEvent.AddEventHandler(null, (Action<Vector3>)(_ => healthy++));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: legacy observer probe"));
            Shot(Vector3.zero); Shot(Vector3.zero);
            Assert.That(failures, Is.EqualTo(1)); Assert.That(healthy, Is.EqualTo(2));
        }

        [Test] public void PlaySessionReset_ClearsBothChannelsAndKeepsExactlyOneSceneCleanupHook()
        {
            int noises = 0, shots = 0;
            var listener = Listen(_ => noises++); Action<Vector3> legacy = _ => shots++;
            NoiseEvent.AddEventHandler(null, listener); ShotEvent.AddEventHandler(null, legacy);
            Reset(); Reset(); Reset(); Report(Noise());
            Assert.That(noises, Is.Zero); Assert.That(shots, Is.Zero);
            NoiseEvent.AddEventHandler(null, listener); ShotEvent.AddEventHandler(null, legacy); Report(Noise());
            Assert.That(noises, Is.EqualTo(1)); Assert.That(shots, Is.EqualTo(1));
            var hooks = (Delegate)typeof(SceneManager).GetField("sceneUnloaded", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            Assert.That(hooks.GetInvocationList().Count(d => d.Method.DeclaringType == Bus), Is.EqualTo(1));
        }
    }
}
