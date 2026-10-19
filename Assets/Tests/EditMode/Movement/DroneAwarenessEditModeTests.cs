using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class DroneAwarenessEditModeTests
    {
        private object awareness;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string p) => o.GetType().GetProperty(p).GetValue(o);
        private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n).Invoke(o, args);
        private string State => Get(awareness, "State").ToString();
        private float Suspicion => (float)Get(awareness, "Suspicion");
        private static object Noise(float emitted = 0f, float strength = .9f, float? expires = null, Vector3? position = null)
            => Activator.CreateInstance(T("SoundObservation"), position ?? new Vector3(7, 0, 3), Enum.Parse(T("NoiseCategory"), "Gunshot"),
                strength, 1f, emitted, expires ?? emitted + 5f);
        private bool Step(float now, object sound = null, bool visible = false, Vector3? seen = null, bool atPost = false)
            => (bool)Call(awareness, "Step", now, visible, seen ?? Vector3.one * 99999f,
                sound != null, sound ?? Activator.CreateInstance(T("SoundObservation")), atPost);
        private bool Memory(string method, float now, out object value)
        {
            object[] args = { now, null }; bool valid = (bool)awareness.GetType().GetMethod(method).Invoke(awareness, args); value = args[1]; return valid;
        }
        private static object DefaultDifficulty => T("DroneDifficultySettings").GetProperty("Default").GetValue(null);
        [SetUp] public void Setup() => awareness = Activator.CreateInstance(T("DroneAwareness"), Activator.CreateInstance(T("AwarenessSettings")), DefaultDifficulty);
        private void Investigate()
        {
            Step(0f, Noise()); Assert.That(State, Is.EqualTo("Suspicious"));
            Step(.4f, Noise()); Assert.That(State, Is.EqualTo("Investigate"));
        }
        private void Search()
        {
            Investigate(); Step(2.6f, Noise()); Assert.That(State, Is.EqualTo("Search"));
        }

        [Test] public void PatrolToSuspicious_UsesNewNoiseEvidence()
        {
            Assert.That(State, Is.EqualTo("Patrol")); Step(0f, Noise(strength: .3f));
            Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Suspicion, Is.EqualTo(.375f).Within(.0001f));
        }
        [Test] public void SuspiciousToInvestigate_AccumulatesDistinctReliableNoise()
        {
            Step(0f, Noise(strength: .3f)); Step(.5f, Noise(.5f, .3f));
            Assert.That(State, Is.EqualTo("Investigate")); Assert.That(Suspicion, Is.EqualTo(.675f).Within(.0001f));
        }
        [Test] public void InvestigateToSearch_UsesEmissionAnchoredDeadline()
        {
            Investigate(); Step(2.6f, Noise()); Assert.That(State, Is.EqualTo("Search"));
            Assert.That(Get(awareness, "LastHeardAt"), Is.EqualTo(0f));
        }
        [Test] public void SearchToReturnToPost_ThenPatrolAtPost()
        {
            Search(); Step(5f); Assert.That(State, Is.EqualTo("ReturnToPost"));
            Step(5.5f, atPost: true); Assert.That(State, Is.EqualTo("Patrol")); Assert.That(Suspicion, Is.Zero);
        }
        [Test] public void ReturnWindow_IsBoundedWhenExistingTacticsDoNotReturnHome()
        {
            Search(); Step(5f); Step(9.1f); Assert.That(State, Is.EqualTo("Patrol"));
        }
        [Test] public void VisualConfirmation_ImmediatelyInterruptsSearchDwellWithCombat()
        {
            Search(); Step(2.61f, visible: true, seen: Vector3.right * 4f);
            Assert.That(State, Is.EqualTo("Combat")); Assert.That(Get(awareness, "HasVisualConfirmation"), Is.True);
        }
        [Test] public void VisualLoss_ImmediatelyClearsConfirmationThenSearchesAfterGrace()
        {
            Step(0f, visible: true, seen: Vector3.one); Step(.1f);
            Assert.That(State, Is.EqualTo("Combat")); Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
            Step(.6f); Assert.That(State, Is.EqualTo("Search"));
        }
        [Test] public void NewReliableObservation_InterruptsSearch()
        {
            Search(); Step(3.1f, Noise(3.1f)); Assert.That(State, Is.EqualTo("Investigate"));
        }
        [Test] public void ObservationDuringMinimumDwell_IsRetainedForNextEligibleTick()
        {
            Search(); object newer = Noise(2.7f); Step(2.7f, newer); Assert.That(State, Is.EqualTo("Search"));
            Step(3.05f, newer); Assert.That(State, Is.EqualTo("Investigate"));
        }
        [Test] public void RepeatedPolling_DoesNotAccumulateSuspicionOrRefreshEvidence()
        {
            Step(0f, Noise(strength: .3f));
            for (int i = 1; i <= 10; i++) Step(i * .05f, Noise(strength: .3f));
            Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Suspicion, Is.EqualTo(.3f).Within(.0001f));
            Assert.That(Get(awareness, "LastHeardAt"), Is.EqualTo(0f));
        }
        [Test] public void RepeatedLoudNoise_CannotCreateCombatOrUncontrolledSwitching()
        {
            for (int i = 0; i < 100; i++) Step(i * .1f, Noise(i * .1f));
            Assert.That(State, Is.EqualTo("Investigate")); Assert.That(Get(awareness, "Transitions"), Is.EqualTo(2));
            Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
            Assert.That(Memory("TryGetLastSeen", 9.9f, out _), Is.False);
        }
        [Test] public void WeakRepeatedNoise_CanRaiseSuspicionButCannotAuthorizeInvestigation()
        {
            for (int i = 0; i < 50; i++) Step(i * .1f, Noise(i * .1f, .05f));
            Assert.That(State, Is.EqualTo("Suspicious")); Assert.That(Get(awareness, "Transitions"), Is.EqualTo(1));
        }
        [Test] public void ExitHysteresis_PreventsThresholdChatter()
        {
            Step(0f, Noise(strength: .3f)); Step(1f, Noise(strength: .3f));
            Assert.That(Suspicion, Is.LessThan(.25f)); Assert.That(State, Is.EqualTo("Suspicious"));
            Step(1.6f, Noise(strength: .3f)); Assert.That(State, Is.EqualTo("Search"));
        }
        [Test] public void VisualAndAuditoryMemory_RemainDistinctAndHiddenInputIsIgnored()
        {
            Vector3 seen = new Vector3(4, 0, 4); object sound = Noise();
            Step(0f, sound, true, seen); Step(.6f, sound, false, Vector3.one * -99999f);
            Assert.That(Memory("TryGetLastSeen", .6f, out var visual), Is.True); Assert.That(visual, Is.EqualTo(seen));
            Assert.That(Memory("TryGetLastHeard", .6f, out var heard), Is.True);
            Assert.That(heard.GetType().GetField("Position").GetValue(heard), Is.EqualTo(new Vector3(7, 0, 3)));
            Assert.That(Memory("TryGetLastSeen", 6f, out _), Is.False);
            Assert.That(Memory("TryGetLastHeard", 5f, out _), Is.False);
        }
        [Test] public void ExpiredEvidenceAndLongClockGap_CannotExtendSearchKnowledge()
        {
            Investigate(); Step(100f); Assert.That(State, Is.EqualTo("ReturnToPost"));
            Assert.That(Suspicion, Is.Zero); Assert.That(Memory("TryGetLastHeard", 100f, out _), Is.False);
            Step(104.1f); Assert.That(State, Is.EqualTo("Patrol"));
        }
        [Test] public void UnavailableHearing_ClearsGoalAndMovesToSearch()
        {
            Investigate(); Step(.9f); Assert.That(State, Is.EqualTo("Search"));
            Assert.That(Memory("TryGetLastHeard", .9f, out _), Is.False);
        }
        [Test] public void StaleFutureAndAlteredSameTimestampSound_CannotRefreshOrReplaceMemory()
        {
            Step(0f, Noise()); float before = Suspicion;
            Step(.1f, Noise(position: Vector3.one * 500f));
            Assert.That(Get(awareness, "LastHeardAt"), Is.EqualTo(0f)); Assert.That(Suspicion, Is.LessThan(before));
            Step(.2f, Noise()); Assert.That(Memory("TryGetLastHeard", .2f, out var original), Is.True);
            Assert.That(original.GetType().GetField("Position").GetValue(original), Is.EqualTo(new Vector3(7, 0, 3)));
            Setup(); Step(.6f, Noise()); Assert.That(Suspicion, Is.Zero);
            Step(.7f, Noise(1f)); Assert.That(Suspicion, Is.Zero);
        }
        [Test] public void Death_DisablesProcessingImmediatelyRegardlessOfDwell()
        {
            Step(0f, Noise(), true, Vector3.one); int transitions = (int)Get(awareness, "Transitions");
            Call(awareness, "Disable"); Assert.That(Get(awareness, "IsEnabled"), Is.False);
            Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False); Assert.That(Suspicion, Is.Zero);
            Assert.That(Step(.01f, Noise(.01f), true, Vector3.one), Is.False);
            Assert.That(Get(awareness, "Transitions"), Is.EqualTo(transitions));
            Assert.That(Memory("TryGetLastSeen", .01f, out _), Is.False); Assert.That(Memory("TryGetLastHeard", .01f, out _), Is.False);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidTime_DoesNotProcess(float time)
        {
            Assert.That(Step(time, Noise()), Is.False); Assert.That(State, Is.EqualTo("Patrol"));
        }
        [Test] public void ReversedClockAndInvalidVisualPosition_AreRejected()
        {
            Step(1f); Assert.That(Step(.5f, Noise(.5f)), Is.False);
            Step(1.1f, visible: true, seen: new Vector3(float.NaN, 0, 0));
            Assert.That(State, Is.EqualTo("Patrol")); Assert.That(Get(awareness, "HasVisualConfirmation"), Is.False);
        }
        [TestCase(-100f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void ConfigurationValidation_IsFiniteBoundedAndDoesNotMutateInput(float invalid)
        {
            object settings = Activator.CreateInstance(T("AwarenessSettings"));
            foreach (var f in settings.GetType().GetFields()) f.SetValue(settings, invalid);
            var validated = Call(settings, "Validated"); Assert.That(ReferenceEquals(settings, validated), Is.False);
            foreach (var f in settings.GetType().GetFields())
            {
                float value = (float)f.GetValue(validated);
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False); Assert.That(value, Is.GreaterThanOrEqualTo(0f));
            }
        }
        [Test] public void DifficultyThresholdsAndSearchDuration_AreCopiedWithoutAssetMutation()
        {
            object difficulty = DefaultDifficulty;
            difficulty.GetType().GetField("suspiciousThreshold").SetValue(difficulty, .3f);
            difficulty.GetType().GetField("investigateThreshold").SetValue(difficulty, .7f);
            difficulty.GetType().GetField("searchDuration").SetValue(difficulty, 2f);
            awareness = Activator.CreateInstance(T("DroneAwareness"), Activator.CreateInstance(T("AwarenessSettings")), difficulty);
            Assert.That(Get(awareness, "SuspiciousThreshold"), Is.EqualTo(.3f));
            Assert.That(Get(awareness, "InvestigateThreshold"), Is.EqualTo(.7f)); Assert.That(Get(awareness, "SearchDuration"), Is.EqualTo(2f));
        }
    }
}
