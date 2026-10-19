using System;
using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace ShiftFour
{
    // Prediction owns sightings and failure recovery; the private Control ring supplies routes.
    public sealed class InterceptorBrain : DroneBrain
    {
        [SerializeField] private InterceptorSettings prediction = new InterceptorSettings();
        [SerializeField] private ControlTacticalSettings controlTactics = new ControlTacticalSettings();
        [SerializeField, FormerlySerializedAs("developerDiagnostics")] private bool showControlTactics;
        private InterceptorPolicy policy;
        private ControlTacticalNavigation tactical;
        private ControlTacticalRouteValidator validator;
        private WaypointGraph tacticalGraph;
        private readonly List<Vector3> junctions = new List<Vector3>();
        private readonly Dictionary<int, ControlTacticalPlan> localPlans = new Dictionary<int, ControlTacticalPlan>();
        public InterceptorPolicy Policy => policy;
        public ControlTacticalNavigation TacticalNavigation => tactical;
        public ControlTacticalRouteValidator TacticalValidator => validator;
        private bool soundAction;
        public bool IsInvestigatingSound
        {
            get => soundAction && Alive && Awareness != null && Awareness.IsEnabled;
            private set => soundAction = value;
        }
        public Vector3 HeardDestination { get; private set; }
        public string SensorDecision { get; private set; } = "No sound action";
        private float heardAt = float.NegativeInfinity;
        private Vector3 heardEstimate;
        private Vector3[] heardRoute = Array.Empty<Vector3>();
        private int heardStep;
        private readonly Vector3[] soundDirections = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1,0,1).normalized, new Vector3(-1,0,1).normalized,
            new Vector3(1,0,-1).normalized, new Vector3(-1,0,-1).normalized };

        private static readonly ProfilerMarker DecisionProfile=new ProfilerMarker("ShiftFour.Decision");
        protected override DecisionOption Decide(bool visible, Vector3 playerPosition, float distance)
        {
            using var profileSample=DecisionProfile.Auto();
            IsInvestigatingSound = false;
            SensorDecision = "Visual prediction and its bounded fallbacks";
            if (policy == null) policy = new InterceptorPolicy(prediction);
            if (tactical == null) tactical = new ControlTacticalNavigation(controlTactics);
            var graph = GameSession.Instance != null ? GameSession.Instance.Graph : null;
            if (graph != null && (validator == null || tacticalGraph != graph))
            {
                tacticalGraph = graph;
                validator = new ControlTacticalRouteValidator(this, graph, controlTactics.clearanceMargin);
            }
            validator?.BeginDecision();
            try
            {
                tactical.SetAnchor(Home);
                junctions.Clear(); localPlans.Clear();
                if (graph != null) for (int i = 0; i < graph.NodeCount; i++) junctions.Add(graph.NodePosition(i));
                if (controlTactics.enabled && tactical.InRoom(transform.position, Home))
                    foreach (var point in tactical.Points) junctions.Add(point);
                var agent = GetComponent<NavMeshAgent>();
                DecisionOption choice = policy.StepWithMotion(Time.time, visible, visible ? playerPosition : Vector3.zero,
                    transform.position, Home, agent != null ? agent.speed : 0f,
                    Motor != null && Motor.isActiveAndEnabled ? Motor.ActualVelocity : Vector3.zero, agent != null ? agent.acceleration : 0f,
                    graph != null ? graph.Version : -1, junctions, ValidatedRoute, PlayerRoute);
                // Tick prediction first even during sound actions: hearing cannot refresh visual
                // history, confidence, failure cooldowns, commitment, or arrival deadlines.
                if (!visible && policy.State != InterceptorState.InterceptAhead &&
                    policy.State != InterceptorState.PursueLastKnown && policy.State != InterceptorState.ObserveTarget &&
                    TrySoundInvestigation(out DecisionOption investigation))
                {
                    tactical.Clear("Fresh uncertain sound; visual prediction memory remains separate");
                    return investigation;
                }
                if (policy.State == InterceptorState.InterceptAhead && Awareness != null && Awareness.IsEnabled)
                    Motor?.SetMovementMode(DroneMovementMode.Chase);
                if (graph != null && policy.SelectedNode >= graph.NodeCount &&
                    localPlans.TryGetValue(policy.SelectedNode - graph.NodeCount, out var selected))
                {
                    // Preserve the selected arc across ticks. The shared motor receives only its next safe stop.
                    selected.Score = choice.Score; selected.DroneEta = policy.DroneArrival; selected.PlayerEta = policy.PlayerArrival;
                    Vector3 next = tactical.Follow(selected, transform.position, agent.stoppingDistance);
                    return new DecisionOption(choice.Name + "; Control " + selected.Side, next, choice.Score);
                }
                tactical.Clear(policy.Reason);
                if (policy.State == InterceptorState.WatchHome || policy.State == InterceptorState.ReturnHome)
                {
                    var route = policy.PlannedRoute;
                    if (route.Length > 2)
                    {
                        int stop = 1;
                        while (stop < route.Length - 1 && Vector3.Distance(transform.position, route[stop]) <= agent.stoppingDistance + .1f &&
                            InterceptorPolicy.ValidRoute(validator.Route(transform.position, route[stop + 1]))) stop++;
                        return new DecisionOption(choice.Name + "; Control return route", route[stop], choice.Score);
                    }
                }
                return choice;
            }
            finally { validator?.EndDecision(); }
        }

        private bool TrySoundInvestigation(out DecisionOption decision)
        {
            decision = default;
            if (Awareness == null || !Awareness.IsEnabled || validator == null ||
                (Awareness.State != DroneAwarenessState.Investigate && Awareness.State != DroneAwarenessState.Search) ||
                !Awareness.TryGetLastHeard(Time.time, out SoundObservation sound) || sound.Strength < .1f ||
                sound.EmittedAt <= policy.LastObservedAt) return false;
            Vector3 goal = HeardDestination;
            bool sameSound = heardAt == sound.EmittedAt && heardEstimate == sound.Position;
            bool valid = sameSound && RemainingSoundRouteValid();
            if (!valid)
            {
                goal = sameSound ? HeardDestination : sound.Position;
                var route = ControlRoute(goal);
                if (!InterceptorPolicy.ValidRoute(route))
                    foreach (Vector3 direction in soundDirections)
                    {
                        goal = sound.Position + direction * Mathf.Min(sound.UncertaintyRadius, 1.75f);
                        route = ControlRoute(goal);
                        if (InterceptorPolicy.ValidRoute(route)) break;
                    }
                if (!InterceptorPolicy.ValidRoute(route)) return false;
                heardRoute = route; heardStep = route.Length > 1 ? 1 : 0;
            }
            var agent = GetComponent<NavMeshAgent>();
            float arrival = (agent != null ? agent.stoppingDistance : 1f) + .1f;
            while (heardStep < heardRoute.Length - 1 && Vector3.Distance(transform.position, heardRoute[heardStep]) <= arrival &&
                InterceptorPolicy.ValidRoute(validator.Route(transform.position, heardRoute[heardStep + 1]))) heardStep++;
            heardAt = sound.EmittedAt; heardEstimate = sound.Position; HeardDestination = goal;
            IsInvestigatingSound = true;
            SensorDecision = $"{sound.Category}; strength {sound.Strength:F2}; uncertainty {sound.UncertaintyRadius:F1}m; expires {sound.ExpiresAt:F1}; route stop {heardStep + 1}/{heardRoute.Length}";
            Motor?.SetMovementMode(DroneMovementMode.Investigate);
            decision = new DecisionOption(Awareness.State == DroneAwarenessState.Search ?
                "Search heard disturbance" : "Investigate heard disturbance", heardRoute[heardStep], 35f);
            return true;
        }

        private bool RemainingSoundRouteValid()
        {
            if (heardStep >= heardRoute.Length) return false;
            Vector3 previous = transform.position;
            for (int i = heardStep; i < heardRoute.Length; i++)
            {
                if (!InterceptorPolicy.ValidRoute(validator.Route(previous, heardRoute[i]))) return false;
                previous = heardRoute[i];
            }
            return true;
        }

        private Vector3[] ControlRoute(Vector3 goal)
        {
            var direct = validator.Route(transform.position, goal);
            if (InterceptorPolicy.ValidRoute(direct) || !controlTactics.enabled || !tactical.InRoom(transform.position, Home))
                return direct;
            // A raised obstacle can make the native shortest path unusable by this body.
            // Reuse the existing Control ring to connect to the uncertain destination.
            Vector3[] best = null; float length = float.PositiveInfinity;
            for (int i = 0; i < tactical.Points.Count; i++)
            {
                var prefix = tactical.RouteTo(i, transform.position, validator.Route);
                if (prefix == null) continue;
                var tail = validator.Route(tactical.Points[i], goal);
                if (!InterceptorPolicy.ValidRoute(tail)) continue;
                var combined = new List<Vector3>(prefix.Route);
                foreach (Vector3 point in tail)
                    if (combined.Count == 0 || Vector3.Distance(combined[combined.Count - 1], point) > .001f) combined.Add(point);
                var candidate = combined.ToArray(); float distance = InterceptorPolicy.Length(candidate);
                if (distance >= length) continue;
                best = candidate; length = distance;
            }
            return best;
        }

        private Vector3[] PlayerRoute(Vector3 from, Vector3 to) => validator != null ? validator.PlayerRoute(from, to) : null;
        private Vector3[] ValidatedRoute(Vector3 from, Vector3 to)
        {
            if (validator == null) return null;
            // A visible engagement targets the player semantically. DroneBrain passes it through
            // GoToCombat, which selects a clear standoff; never reject pursuit because the player's
            // own collider occupies that semantic endpoint. Hidden search/arc goals stay strict.
            if (policy!=null && policy.ObservationAge(Time.time)<.01f &&
                Vector3.Distance(to,policy.LastObserved)<.1f)
                return validator.PlayerRoute(from,to);
            if (controlTactics.enabled && tactical.InRoom(from, Home) &&
                Vector3.Distance(from, transform.position) < .1f)
            {
                for (int i = 0; i < tactical.Points.Count; i++)
                {
                    if (Vector3.Distance(to, tactical.Points[i]) > .01f) continue;
                    var route = tactical.RouteTo(i, from, validator.Route);
                    if (route == null) return null;
                    localPlans[i] = route;
                    return route.Route;
                }
            }
            if (Vector3.Distance(from, transform.position) < .1f && Vector3.Distance(to, Home) < .01f)
                return ControlRoute(to);
            return validator.Route(from, to);
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!showControlTactics) return;
            if (tactical == null) tactical = new ControlTacticalNavigation(controlTactics);
            tactical.SetAnchor(Home);
            var points = tactical.Points;
            for (int i=0;i<points.Count;i++)
            {
                Gizmos.color = i==1 || i==2 ? Color.cyan : i==4 || i==5 ? Color.magenta : Color.yellow;
                Gizmos.DrawWireSphere(points[i]+Vector3.up*.15f,.3f);
                Gizmos.DrawLine(points[i]+Vector3.up*.15f,points[(i+1)%points.Count]+Vector3.up*.15f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(points[i]+Vector3.up*.5f,ControlTacticalNavigation.Labels[i]);
#endif
            }
            if (IsInvestigatingSound)
            {
                Gizmos.color = new Color(1f,.55f,.1f);
                Gizmos.DrawWireSphere(HeardDestination + Vector3.up*.2f,.45f);
                Vector3 previous = transform.position;
                for (int i = heardStep; i < heardRoute.Length; i++)
                {
                    Gizmos.DrawLine(previous + Vector3.up*.2f, heardRoute[i] + Vector3.up*.2f);
                    previous = heardRoute[i];
                }
            }
            var plan=tactical.Plan;
            if (policy!=null)
            {
                Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(policy.LastObserved+Vector3.up*.15f,.4f);
                foreach(var candidate in policy.Candidates)
                {
                    Gizmos.color=candidate.Node==policy.SelectedNode?Color.green:Color.gray;
                    Gizmos.DrawWireSphere(candidate.Position+Vector3.up*.25f,.4f);
                    UnityEditor.Handles.Label(candidate.Position+Vector3.up*.8f,
                        $"J{candidate.Node}: {candidate.Score:F1}; ETA {candidate.DroneArrival:F1}/{candidate.PlayerArrival:F1}s; C={candidate.Confidence:F2}");
                }
                Gizmos.color=Color.cyan;
                var predicted=policy.PredictedTrajectory;
                for(int i=1;i<predicted.Length;i++) Gizmos.DrawLine(predicted[i-1]+Vector3.up*.25f,predicted[i]+Vector3.up*.25f);
                UnityEditor.Handles.Label(transform.position+Vector3.up*2.3f,
                    $"{policy.State} confidence {policy.Confidence:F2}; observation age {policy.ObservationAge(Time.time):F1}s\n{policy.Reason}\nETA cap {policy.TimingSpeedLimit:F1}m/s, acceleration {policy.TimingAcceleration:F1}m/s²; {SensorDecision}");
            }
            if (plan!=null)
            {
                Gizmos.color=Color.green;
                for (int i=1;i<plan.Route.Length;i++) Gizmos.DrawLine(plan.Route[i-1]+Vector3.up*.2f,plan.Route[i]+Vector3.up*.2f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position+Vector3.up*3f,
                    $"{plan.Side} -> {ControlTacticalNavigation.Labels[plan.Target]}: score {plan.Score:F1}, drone ETA {plan.DroneEta:F1}s\n{tactical.Reason}");
#endif
            }
#endif
        }
    }
}
