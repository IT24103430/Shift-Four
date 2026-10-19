using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    public enum InterceptorState
    {
        WatchHome, ObserveTarget, PredictMovement, InterceptAhead,
        PursueLastKnown, SearchLastSeenArea, Replan, ReturnHome
    }

    [Serializable]
    public sealed class InterceptorSettings
    {
        [Range(3, 32)] public int historyCapacity = 12;
        [Min(.1f)] public float historySeconds = 3f;
        [Min(.1f)] public float observationExpiry = 7f;
        [Min(.1f)] public float predictionHorizon = 4f;
        [Min(.1f)] public float maxPlayerSpeed = 6f;
        [Min(.1f)] public float maxAcceleration = 14f;
        [Range(.01f, 1f)] public float velocityBlend = .55f;
        [Min(0f)] public float stoppedSpeed = .35f;
        [Range(1f, 180f)] public float sharpTurnDegrees = 55f;
        [Range(0f, 1f)] public float minimumConfidence = .38f;
        [Min(.1f)] public float uncertaintyGrowth = .7f;
        [Min(0f)] public float minimumCommitment = 1.2f;
        [Min(0f)] public float switchingThreshold = 8f;
        [Min(0f)] public float arrivalSlack = .6f;
        [Min(.1f)] public float arrivalRadius = 1.1f;
        [Min(.1f)] public float failedCandidateCooldown = 3f;
        [Min(.1f)] public float pursueSeconds = 2f;
        [Min(.1f)] public float searchSeconds = 3f;
        [Min(.1f)] public float searchRadius = 4f;
        [Min(0f)] public float immediateThreatRange = 3.5f;
        [Header("Normalized score weights")]
        [Min(0f)] public float timingWeight = 30f;
        [Min(0f)] public float forwardWeight = 20f;
        [Min(0f)] public float positioningWeight = 20f;
        [Min(0f)] public float confidenceWeight = 25f;
        [Min(0f)] public float stabilityWeight = 8f;
    }

    public struct InterceptorObservation
    {
        public float Time, Speed;
        public Vector3 Position, Direction;
    }

    public sealed class InterceptorCandidate
    {
        public int Node;
        public Vector3 Position;
        public float Score, DroneArrival, PlayerArrival, Confidence;
        public float Timing, Forward, Positioning, Stability;
        public Vector3[] PlayerRoute, DroneRoute;
    }

    // This core knows only explicitly supplied sightings, world routes and the drone's own state.
    // The invisible-position argument is deliberately ignored, even if callers supply live data.
    public sealed class InterceptorPolicy
    {
        public readonly InterceptorSettings Settings;
        private readonly List<InterceptorObservation> history = new List<InterceptorObservation>();
        private readonly List<InterceptorCandidate> candidates = new List<InterceptorCandidate>();
        private readonly Dictionary<int, Vector2> failed = new Dictionary<int, Vector2>();
        private readonly HashSet<int> searched = new HashSet<int>();
        private int searchNode = -1;
        private readonly List<Vector3> searchPoints = new List<Vector3>();
        private Vector3 velocity;
        private float quality, lossAt = float.PositiveInfinity, committedAt, searchUntil = float.PositiveInfinity;
        private float predictionDeadline, predictionObservedAt = float.NegativeInfinity;
        private int version = -1;
        private bool wasVisible;
        private InterceptorCandidate selected;
        private Func<Vector3, Vector3, Vector3[]> playerRoutes;
        public IReadOnlyList<InterceptorObservation> History => history;
        public IReadOnlyList<InterceptorCandidate> Candidates => candidates;
        public InterceptorState State { get; private set; } = InterceptorState.WatchHome;
        public Vector3 LastObserved { get; private set; }
        public float LastObservedAt { get; private set; } = float.NegativeInfinity;
        public float ObservationAge(float now) => Mathf.Max(0f, now - LastObservedAt);
        public float Confidence { get; private set; }
        public Vector3 PredictedPosition { get; private set; }
        public Vector3[] PlannedRoute { get; private set; } = Array.Empty<Vector3>();
        public Vector3[] PredictedTrajectory { get; private set; } = Array.Empty<Vector3>();
        public int SelectedNode => selected == null ? -1 : selected.Node;
        public float DroneArrival => selected == null ? 0f : selected.DroneArrival;
        public float PlayerArrival => selected == null ? 0f : selected.PlayerArrival;
        public string Reason { get; private set; } = "No successful observation";
        public int Replans { get; private set; }
        public int Switches { get; private set; }
        public int FailedAttempts { get; private set; }

        public InterceptorPolicy(InterceptorSettings settings) { Settings = settings; }
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude);

        private string Observe(float now, Vector3 position)
        {
            if (!Finite(position) || now <= LastObservedAt) return null;
            string failure = null;
            float gap = now - LastObservedAt;
            if (gap > Settings.historySeconds)
            { history.Clear(); velocity = Vector3.zero; quality = 0f; }
            if (history.Count > 0)
            {
                Vector3 raw = Flat(position - LastObserved) / gap;
                float speed = raw.magnitude;
                float turn = velocity.magnitude > Settings.stoppedSpeed && speed > Settings.stoppedSpeed
                    ? Vector3.Angle(velocity, raw) : 0f;
                float acceleration = (raw - velocity).magnitude / gap;
                if (speed > Settings.maxPlayerSpeed * 1.25f) failure = "Implausible observation displacement";
                else if (speed <= Settings.stoppedSpeed) failure = "Player stopped";
                else if (turn > 140f) failure = "Player reversed";
                else if (turn > Settings.sharpTurnDegrees) failure = "Sharp direction change";
                else if (acceleration > Settings.maxAcceleration && history.Count > 2) failure = "Sudden acceleration";
                raw = Vector3.ClampMagnitude(raw, Mathf.Max(.1f, Settings.maxPlayerSpeed));
                // Do not smooth stale forward momentum through a reversal or a stop.
                velocity = failure != null ? raw : Vector3.Lerp(velocity, raw, Settings.velocityBlend);
                quality = failure != null ? .12f : Mathf.MoveTowards(quality, 1f, .26f);
            }
            LastObserved = position; LastObservedAt = now;
            history.Add(new InterceptorObservation { Time = now, Position = position,
                Speed = velocity.magnitude, Direction = velocity.normalized });
            while (history.Count > Mathf.Max(3, Settings.historyCapacity) ||
                (history.Count > 1 && now - history[0].Time > Settings.historySeconds)) history.RemoveAt(0);
            return failure;
        }

        // The local tactical layer may need a wider collision envelope than player hypotheses.
        public DecisionOption StepWithRoutes(float now, bool visible, Vector3 observedPosition, Vector3 drone,
            Vector3 home, float droneSpeed, int graphVersion, IReadOnlyList<Vector3> junctions,
            Func<Vector3, Vector3, Vector3[]> droneRoutes, Func<Vector3, Vector3, Vector3[]> playerRoute)
        {
            playerRoutes = playerRoute;
            try { return Step(now, visible, observedPosition, drone, home, droneSpeed, graphVersion, junctions, droneRoutes); }
            finally { playerRoutes = null; }
        }

        public DecisionOption Step(float now, bool visible, Vector3 observedPosition, Vector3 drone,
            Vector3 home, float droneSpeed, int graphVersion, IReadOnlyList<Vector3> junctions,
            Func<Vector3, Vector3, Vector3[]> findRoute)
        {
            plannedOrigin = drone;
            string invalid = visible ? Observe(now, observedPosition) : null;
            if (!visible && wasVisible)
            {
                lossAt = now;
                searchPoints.Clear();
                foreach (Vector3 node in junctions)
                    if (Flat(node - LastObserved).magnitude <= Settings.searchRadius) searchPoints.Add(node);
                float radius = Settings.searchRadius * .5f;
                searchPoints.Add(LastObserved + Vector3.right * radius);
                searchPoints.Add(LastObserved + Vector3.left * radius);
                searchPoints.Add(LastObserved + Vector3.forward * radius);
                searchPoints.Add(LastObserved + Vector3.back * radius);
                searchUntil = Mathf.Min(LastObservedAt + Settings.observationExpiry,
                    now + Settings.pursueSeconds + Settings.searchSeconds);
            }
            if (visible)
            {
                lossAt = float.PositiveInfinity; searchUntil = float.PositiveInfinity;
                searchNode = -1; searched.Clear();
            }
            wasVisible = visible;
            float age = ObservationAge(now);
            while (history.Count > 0 && now - history[0].Time > Settings.historySeconds) history.RemoveAt(0);
            Confidence = age >= Settings.observationExpiry ? 0f : quality * Mathf.Exp(-age * Settings.uncertaintyGrowth);
            candidates.Clear();
            bool graphChanged = version >= 0 && graphVersion != version;
            if (graphChanged)
            {
                invalid = "Gate/graph version changed";
                if (selected == null) Replans++;
            }
            version = graphVersion;
            if (selected != null)
            {
                Vector3[] route = findRoute(drone, selected.Position);
                if (!ValidRoute(route)) invalid = "Selected junction unreachable";
                else if (!visible && Flat(drone - selected.Position).magnitude <= Settings.arrivalRadius &&
                    now >= predictionDeadline) invalid = "Interception reached but player absent";
                else if (now > predictionDeadline + Settings.arrivalSlack) invalid = "Predicted arrival window missed";
                if (invalid != null)
                {
                    if (invalid.Contains("absent") || invalid.Contains("missed"))
                    {
                        failed[selected.Node] = new Vector2(now + Settings.failedCandidateCooldown, LastObservedAt);
                        FailedAttempts++;
                    }
                    selected = null; Replans++; State = InterceptorState.Replan; Reason = invalid;
                }
            }
            if (visible && Flat(observedPosition - drone).magnitude <= Settings.immediateThreatRange)
            {
                selected = null;
                return Choose(InterceptorState.ObserveTarget, observedPosition, "Visible immediate threat", findRoute(drone, observedPosition));
            }
            if (float.IsNegativeInfinity(LastObservedAt))
                return Choose(InterceptorState.WatchHome, home, "No successful observation", findRoute(drone, home));
            if (age >= Settings.observationExpiry || (!visible && now >= searchUntil))
            {
                selected = null; Confidence = 0f;
                return Choose(Flat(drone - home).magnitude <= Settings.arrivalRadius ? InterceptorState.WatchHome : InterceptorState.ReturnHome,
                    home, "Observation/search window expired", findRoute(drone, home));
            }

            InterceptorCandidate best = null, incumbent = null;
            if (Confidence >= Settings.minimumConfidence && velocity.magnitude > Settings.stoppedSpeed && history.Count >= 3)
            {
                State = selected == null ? InterceptorState.PredictMovement : State;
                for (int i = 0; i < junctions.Count; i++)
                {
                    if (failed.TryGetValue(i, out Vector2 block) && (now < block.x || LastObservedAt <= block.y)) continue;
                    InterceptorCandidate candidate = Evaluate(i, junctions[i], drone, droneSpeed, age, findRoute);
                    if (candidate == null) continue;
                    candidates.Add(candidate);
                    if (selected != null && candidate.Node == selected.Node) incumbent = candidate;
                    if (best == null || candidate.Score > best.Score) best = candidate;
                }
            }
            if (best != null)
            {
                if (incumbent != null && (now - committedAt < Settings.minimumCommitment ||
                    best.Score < incumbent.Score + Settings.switchingThreshold)) best = incumbent;
                if (selected == null || selected.Node != best.Node)
                {
                    if (selected != null) Switches++;
                    committedAt = now;
                    Reason = invalid ?? "Reachable forward junction with feasible arrival time";
                }
                // Only a genuinely newer observation may revise the arrival window.
                // Invisible/repeated ticks cannot indefinitely extend a failed prediction.
                if (selected == null || selected.Node != best.Node || LastObservedAt > predictionObservedAt)
                {
                    predictionDeadline = now + best.PlayerArrival;
                    predictionObservedAt = LastObservedAt;
                }
                if (!visible) Reason = "Visual contact lost; continuing justified interception";
                selected = best;
                PredictedTrajectory = best.PlayerRoute;
                PredictedPosition = Along(best.PlayerRoute, velocity.magnitude * Mathf.Min(Settings.predictionHorizon, age + 1f));
                return Choose(InterceptorState.InterceptAhead, best.Position, Reason, best.DroneRoute);
            }
            if (selected != null) { selected = null; Replans++; }
            PredictedTrajectory = Array.Empty<Vector3>(); PredictedPosition = LastObserved;
            string fallback = invalid ?? (Confidence < Settings.minimumConfidence ? "Prediction confidence insufficient" : "No feasible interception candidate");
            Vector3[] lastRoute = findRoute(drone, LastObserved);
            bool reached = Flat(drone - LastObserved).magnitude <= Settings.arrivalRadius;
            if (visible || (!reached && now - lossAt < Settings.pursueSeconds))
            {
                if (ValidRoute(lastRoute)) return Choose(visible && history.Count < 3 ? InterceptorState.ObserveTarget :
                    InterceptorState.PursueLastKnown, LastObserved, fallback, lastRoute);
            }
            // Bounded local search, ordered deterministically. No target transform is consulted.
            if (!visible && now < searchUntil)
            {
                if (searchNode >= 0 && searchNode < searchPoints.Count)
                {
                    Vector3 target = searchPoints[searchNode];
                    Vector3[] searchRoute = findRoute(drone, target);
                    if (Flat(target - drone).magnitude > Settings.arrivalRadius && ValidRoute(searchRoute))
                        return Choose(InterceptorState.SearchLastSeenArea, target, fallback + "; continue local search", searchRoute);
                    searched.Add(searchNode); searchNode = -1;
                }
                for (int i = 0; i < searchPoints.Count; i++)
                {
                    if (searched.Contains(i)) continue;
                    Vector3 node = searchPoints[i];
                    float radius = Flat(node - LastObserved).magnitude;
                    if (radius < Settings.arrivalRadius || radius > Settings.searchRadius ||
                        Flat(node - drone).magnitude <= Settings.arrivalRadius) continue;
                    Vector3[] route = findRoute(drone, node);
                    if (ValidRoute(route))
                    {
                        searchNode = i;
                        return Choose(InterceptorState.SearchLastSeenArea, node, fallback + "; local search", route);
                    }
                }
                if (ValidRoute(lastRoute)) return Choose(InterceptorState.SearchLastSeenArea, LastObserved, fallback + "; hold last seen area", lastRoute);
                if (Flat(drone - LastObserved).magnitude <= Settings.searchRadius)
                    return Choose(InterceptorState.SearchLastSeenArea, drone, fallback + "; observe nearby cover", findRoute(drone, drone));
            }
            return Choose(InterceptorState.ReturnHome, home, fallback + "; no reachable local search", findRoute(drone, home));
        }

        private InterceptorCandidate Evaluate(int node, Vector3 point, Vector3 drone, float speed, float age,
            Func<Vector3, Vector3, Vector3[]> findRoute)
        {
            Vector3[] playerPath = (playerRoutes ?? findRoute)(LastObserved, point), dronePath = findRoute(drone, point);
            if (!ValidRoute(playerPath) || !ValidRoute(dronePath) || speed <= 0f) return null;
            float length = Length(playerPath), droneLength = Length(dronePath);
            float playerTime = length / Mathf.Max(.1f, velocity.magnitude) - age;
            float droneTime = droneLength / speed;
            // A route can curve around cover, but its initial leg must agree with observed motion.
            Vector3 first = Vector3.zero;
            for (int i = 1; i < playerPath.Length && first.sqrMagnitude < .01f; i++) first = Flat(playerPath[i] - playerPath[0]);
            float alignment = Vector3.Dot(first.normalized, velocity.normalized);
            float forward = Vector3.Dot(Flat(point - LastObserved), velocity.normalized);
            if (alignment < .35f || forward < 1f || playerTime <= 0f ||
                age + playerTime > Settings.predictionHorizon || droneTime > playerTime + Settings.arrivalSlack) return null;
            float direct = Flat(point - LastObserved).magnitude;
            float positioning = Mathf.Clamp01(direct / Mathf.Max(.1f, length)) * alignment;
            float confidence = Confidence * positioning / (1f + .12f * playerTime * Settings.uncertaintyGrowth);
            if (confidence < Settings.minimumConfidence) return null;
            var c = new InterceptorCandidate { Node = node, Position = point, DroneArrival = droneTime,
                PlayerArrival = playerTime, Confidence = confidence, PlayerRoute = playerPath, DroneRoute = dronePath,
                Timing = Mathf.Clamp01((playerTime - droneTime + Settings.arrivalSlack) / Mathf.Max(.1f, playerTime)),
                Forward = Mathf.Clamp01(forward / (Settings.maxPlayerSpeed * Settings.predictionHorizon)),
                Positioning = positioning, Stability = selected != null && selected.Node == node ? 1f : 0f };
            c.Score = Settings.timingWeight * c.Timing + Settings.forwardWeight * c.Forward +
                Settings.positioningWeight * c.Positioning + Settings.confidenceWeight * c.Confidence + Settings.stabilityWeight * c.Stability;
            return c;
        }

        public static bool ValidRoute(Vector3[] route)
        {
            if (route == null || route.Length == 0) return false;
            foreach (Vector3 point in route) if (!Finite(point)) return false;
            return true;
        }
        public static float Length(Vector3[] route)
        {
            float distance = 0f;
            for (int i = 1; i < route.Length; i++) distance += Vector3.Distance(route[i - 1], route[i]);
            return distance;
        }
        private static Vector3 Along(Vector3[] route, float distance)
        {
            for (int i = 1; i < route.Length; i++)
            {
                float leg = Vector3.Distance(route[i - 1], route[i]);
                if (distance <= leg) return Vector3.Lerp(route[i - 1], route[i], leg > 0f ? distance / leg : 0f);
                distance -= leg;
            }
            return route[route.Length - 1];
        }
        private DecisionOption Choose(InterceptorState state, Vector3 point, string reason, Vector3[] route)
        {
            State = state; Reason = reason;
            if (state != InterceptorState.InterceptAhead)
            {
                PredictedTrajectory = Array.Empty<Vector3>();
                PredictedPosition = LastObserved;
            }
            PlannedRoute = ValidRoute(route) ? route : Array.Empty<Vector3>();
            // A fallback with no valid route holds safely; the next tick can recover after carving.
            if (!ValidRoute(route)) point = plannedOrigin;
            return new DecisionOption(state + ": " + reason, point, selected == null ? 34f : selected.Score);
        }
        private Vector3 plannedOrigin;
    }

}
