using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class InterceptorPolicyEditModeTests
    {
        private object policy, settings;
        private Type type;
        private Vector3 drone = new Vector3(9, 0, 2), home = new Vector3(15, 0, 2);
        private Vector3[] nodes = { new Vector3(10, 0, 0), new Vector3(12, 0, 0) };
        private Func<Vector3, Vector3, Vector3[]> routes;
        private int version;
        private Vector3 destination;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private object Get(string name) => type.GetProperty(name).GetValue(policy);
        private string State => Get("State").ToString();
        private float Confidence => (float)Get("Confidence");
        private int Selected => (int)Get("SelectedNode");
        private void Setting(string name, object value) => settings.GetType().GetField(name).SetValue(settings, value);
        private void Tick(float now, bool visible, Vector3 position)
        {
            object result = type.GetMethod("Step").Invoke(policy, new object[] {
                now, visible, position, drone, home, 3.5f, version, nodes, routes });
            destination = (Vector3)result.GetType().GetField("Destination").GetValue(result);
        }
        private void Straight()
        {
            for (int i = 0; i <= 6; i++) Tick(i * .3f, true, Vector3.right * (i * .6f));
        }
        [SetUp] public void SetUp()
        {
            type = T("InterceptorPolicy"); settings = Activator.CreateInstance(T("InterceptorSettings"));
            policy = Activator.CreateInstance(type, settings);
            routes = (a, b) => new[] { a, b };
            version = 0;
            drone = new Vector3(9, 0, 2); home = new Vector3(15, 0, 2);
            nodes = new[] { new Vector3(10, 0, 0), new Vector3(12, 0, 0) };
        }
        [Test] public void StraightMotion_SelectsAheadWithFeasibleRouteTiming()
        {
            Straight(); Assert.That(State, Is.EqualTo("InterceptAhead"));
            Assert.That(destination.x, Is.GreaterThan(3.6f));
            var candidates = (IList)Get("Candidates"); Assert.That(candidates.Count, Is.GreaterThan(0));
            foreach (var candidate in candidates)
            {
                var c = candidate.GetType();
                Assert.That((float)c.GetField("DroneArrival").GetValue(candidate), Is.LessThanOrEqualTo(
                    (float)c.GetField("PlayerArrival").GetValue(candidate) + .6f));
            }
        }
        [Test] public void Reversal_InvalidatesForwardCommitmentImmediately()
        {
            Straight(); Tick(2.1f, true, Vector3.right * 3f);
            Assert.That(Selected, Is.EqualTo(-1)); Assert.That(Confidence, Is.LessThan(.38f));
            Assert.That(Get("Reason").ToString(), Does.Contain("reversed"));
        }
        [Test] public void StationarySightings_NeverIntercept()
        {
            for (int i = 0; i < 12; i++) Tick(i * .3f, true, Vector3.zero);
            Assert.That(Selected, Is.EqualTo(-1)); Assert.That(destination, Is.EqualTo(Vector3.zero));
        }
        [Test] public void Stop_DiscardsSmoothedMomentum()
        { Straight(); Tick(2.1f, true, Vector3.right * 3.6f); Assert.That(Selected, Is.EqualTo(-1)); }
        [Test] public void SharpTurn_LowersConfidenceAndUsesActualObservation()
        {
            Straight(); Vector3 turn = new Vector3(3.6f, 0, 1f); Tick(2.1f, true, turn);
            Assert.That(Confidence, Is.LessThan(.38f)); Assert.That(destination, Is.EqualTo(turn));
        }
        [Test] public void SuddenAcceleration_IsNotTreatedAsStableEvidence()
        {
            Straight(); Tick(2.1f, true, Vector3.right * 5.7f);
            Assert.That(Selected, Is.EqualTo(-1)); Assert.That(Confidence, Is.LessThan(.38f));
        }
        [Test] public void HiddenPosition_NeverChangesMemoryOrDestination()
        {
            Straight(); Tick(2.4f, false, Vector3.one * 99999);
            Vector3 first = destination; int count = ((IList)Get("History")).Count;
            Tick(2.4f, false, new Vector3(float.NaN, float.PositiveInfinity, -999));
            Assert.That(destination, Is.EqualTo(first)); Assert.That((Vector3)Get("LastObserved"), Is.EqualTo(Vector3.right * 3.6f).Using(Vector3Comparer.Instance));
            Assert.That(((IList)Get("History")).Count, Is.EqualTo(count));
        }
        [Test] public void CoverLoss_DecreasesConfidenceThenSearchesThenReturnsHome()
        {
            Straight(); float seenConfidence = Confidence;
            Tick(2.1f, false, Vector3.zero); Assert.That(Confidence, Is.LessThan(seenConfidence));
            drone = Vector3.right * 3.6f;
            Tick(4.3f, false, Vector3.zero); Assert.That(State, Is.EqualTo("SearchLastSeenArea"));
            Tick(7.5f, false, Vector3.zero); Assert.That(State, Is.EqualTo("ReturnHome")); Assert.That(destination, Is.EqualTo(home));
        }
        [Test] public void ExpiredHistory_IsEmptyAndReturnsHome()
        {
            Straight(); Tick(10f, false, Vector3.one * 500);
            Assert.That(Confidence, Is.Zero); Assert.That(((IList)Get("History")).Count, Is.Zero);
            Assert.That(State, Is.EqualTo("ReturnHome"));
        }
        [Test] public void AtHomeWithoutSightings_WatchesHome()
        { drone = home; Tick(1f, false, Vector3.one); Assert.That(State, Is.EqualTo("WatchHome")); }
        [Test] public void History_IsBoundedAndOnlyVisibleTicksAddSamples()
        {
            Setting("historyCapacity", 5);
            for (int i = 0; i < 100; i++) Tick(i * .3f, true, Vector3.right * i * .6f);
            Assert.That(((IList)Get("History")).Count, Is.EqualTo(5));
            Tick(30f, false, Vector3.zero); Assert.That(((IList)Get("History")).Count, Is.EqualTo(5));
        }
        [Test] public void LockedGate_RejectsCandidatesAndOpeningTriggersReplan()
        {
            bool open = false;
            routes = (a, b) => b.x >= 10f && !open ? null : new[] { a, b };
            Straight(); Assert.That(Selected, Is.EqualTo(-1));
            open = true; version++; Tick(2.1f, true, Vector3.right * 4.2f);
            Assert.That(State, Is.EqualTo("InterceptAhead"));
            open = false; version++; Tick(2.4f, true, Vector3.right * 4.8f);
            Assert.That(Selected, Is.EqualTo(-1)); Assert.That((int)Get("Replans"), Is.GreaterThan(0));
        }
        [Test] public void NoReachableCandidate_PursuesLastGenuineSighting()
        {
            routes = (a, b) => b.x >= 10f ? null : new[] { a, b };
            Straight(); Assert.That(State, Is.EqualTo("PursueLastKnown"));
            Assert.That(destination.x, Is.EqualTo(3.6f).Within(.001f));
        }
        [Test] public void CompleteNavigationFailure_HoldsOwnPositionSafely()
        { routes = (a, b) => null; Straight(); Assert.That(destination, Is.EqualTo(drone)); }
        [Test] public void LongDetour_IsRejectedDespiteShortEuclideanDistance()
        {
            routes = (a, b) => a == drone ? new[] { a, a + Vector3.forward * 100, b } : new[] { a, b };
            Straight(); Assert.That(Selected, Is.EqualTo(-1));
        }
        [Test] public void CurvedTacticalRoute_IsPredictedAlongPathRatherThanThroughWall()
        {
            nodes = new[] { new Vector3(8.5f, 0, 2) }; drone = new Vector3(10, 0, 2);
            routes = (a, b) => a == drone ? new[] { a, b } : new[] { a, new Vector3(8.5f, 0, 0), b };
            Straight(); Assert.That(State, Is.EqualTo("InterceptAhead"));
            Assert.That(((Vector3[])Get("PredictedTrajectory")).Length, Is.EqualTo(3));
            Assert.That(((Vector3)Get("PredictedPosition")).z, Is.Zero);
        }
        [Test] public void FailedArrival_IsNotRepeatedUsingSameOldEvidence()
        {
            Straight(); drone = destination; Setting("uncertaintyGrowth", .01f);
            Tick(5.7f, false, Vector3.one * 100);
            Assert.That((int)Get("FailedAttempts"), Is.EqualTo(1));
            Tick(8.8f, false, Vector3.one * 200);
            Assert.That(Selected, Is.EqualTo(-1));
        }
        [Test] public void RepeatedUpdates_KeepCommitmentWithoutExtendingArrivalWindow()
        {
            Straight(); int selected = Selected;
            for (int i = 0; i < 30; i++) Tick(1.8f, true, Vector3.right * 3.6f);
            Assert.That(Selected, Is.EqualTo(selected)); Assert.That((int)Get("Switches"), Is.Zero);
            Assert.That(((IList)Get("History")).Count, Is.EqualTo(7));
        }
        [Test] public void Hysteresis_RetainsIncumbentWhenCompetingScoreImprovesSlightly()
        {
            Straight(); int selected = Selected;
            drone += Vector3.right * .1f; Tick(1.9f, false, Vector3.zero);
            Assert.That(Selected, Is.EqualTo(selected)); Assert.That((int)Get("Switches"), Is.Zero);
        }
        [Test] public void Uncertainty_DecaysWithAgeAndCannotCreateFurtherPredictions()
        {
            Straight(); float before = Confidence; Tick(3.5f, false, Vector3.zero);
            Assert.That(Confidence, Is.LessThan(before)); Assert.That(Selected, Is.EqualTo(-1));
        }
        [Test] public void ObservationGap_DoesNotInferVelocityAcrossUnseenMovement()
        {
            Straight(); Tick(6f, true, Vector3.right * 25f);
            Assert.That(Confidence, Is.LessThan(.38f)); Assert.That(((IList)Get("History")).Count, Is.EqualTo(1));
        }
        [Test] public void NonFiniteVisibleInput_CannotPoisonHistory()
        {
            Straight(); Tick(2.1f, true, new Vector3(float.NaN, 0, 0));
            Assert.That((float)Get("LastObservedAt"), Is.EqualTo(1.8f).Within(.001f));
        }
        [Test] public void SwitchingThreshold_BlocksMeasuredBetterAlternativeThenAllowsItWhenRemoved()
        {
            Setting("minimumCommitment", 0f); Setting("stabilityWeight", 0f); Setting("switchingThreshold", 10000f);
            Straight(); int incumbent = Selected;
            drone = new Vector3(12, 0, 1); Tick(2.1f, true, Vector3.right * 4.2f);
            Assert.That(Selected, Is.EqualTo(incumbent));
            var list = (IList)Get("Candidates");
            float own = float.NegativeInfinity, other = float.NegativeInfinity;
            foreach (var c in list)
            {
                float score = (float)c.GetType().GetField("Score").GetValue(c);
                if ((int)c.GetType().GetField("Node").GetValue(c) == incumbent) own = score;
                else other = Mathf.Max(other, score);
            }
            Assert.That(other, Is.GreaterThan(own), "Fixture must actually offer a better competing candidate");
            Setting("switchingThreshold", 0f); Tick(2.4f, true, Vector3.right * 4.8f);
            Assert.That(Selected, Is.Not.EqualTo(incumbent)); Assert.That((int)Get("Switches"), Is.EqualTo(1));
        }
        [Test] public void MinimumCommitment_RetainsRouteEvenWithZeroSwitchingThreshold()
        {
            Setting("minimumCommitment", 5f); Setting("stabilityWeight", 0f); Setting("switchingThreshold", 0f);
            Straight(); int incumbent = Selected;
            drone = new Vector3(12, 0, 1); Tick(2.1f, true, Vector3.right * 4.2f);
            Assert.That(Selected, Is.EqualTo(incumbent));
        }
        [Test] public void InvalidRoute_BypassesMinimumCommitment()
        {
            Setting("minimumCommitment", 100f); Straight();
            routes = (a, b) => b.x >= 10 ? null : new[] { a, b };
            Tick(2.1f, true, Vector3.right * 4.2f);
            Assert.That(Selected, Is.EqualTo(-1)); Assert.That(Get("Reason").ToString(), Does.Contain("unreachable"));
        }
        [Test] public void ImmediateVisibleThreat_BypassesCommittedPrediction()
        {
            Straight(); Vector3 threat = drone + Vector3.left;
            Tick(2.1f, true, threat); Assert.That(State, Is.EqualTo("ObserveTarget"));
            Assert.That(destination, Is.EqualTo(threat)); Assert.That(Selected, Is.EqualTo(-1));
        }
        [Test] public void ArrivalWindow_OnlyFreshObservationsMayReviseIt()
        {
            Straight();
            var deadline = type.GetField("predictionDeadline", BindingFlags.NonPublic | BindingFlags.Instance);
            float original = (float)deadline.GetValue(policy);
            Tick(2.1f, true, Vector3.right * 4.05f);
            Assert.That(Selected, Is.EqualTo(0));
            float revised = (float)deadline.GetValue(policy);
            Assert.That(revised, Is.GreaterThan(original), "A fresh slower observation should revise the ETA");
            Tick(2.2f, false, Vector3.one * 9999);
            Assert.That((float)deadline.GetValue(policy), Is.EqualTo(revised));
        }
        private sealed class Vector3Comparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public static readonly Vector3Comparer Instance = new Vector3Comparer();
            public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) < .001f;
            public int GetHashCode(Vector3 v) => v.GetHashCode();
        }
    }
}
