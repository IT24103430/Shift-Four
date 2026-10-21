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

        private static readonly ProfilerMarker DecisionProfile=new ProfilerMarker("ShiftFour.Decision");
        protected override DecisionOption Decide(bool visible, Vector3 playerPosition, float distance)
        {
            using var profileSample=DecisionProfile.Auto();
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
                DecisionOption choice = policy.StepWithRoutes(Time.time, visible, visible ? playerPosition : Vector3.zero,
                    transform.position, Home, agent != null ? agent.speed : 0f, graph != null ? graph.Version : -1,
                    junctions, ValidatedRoute, PlayerRoute);
                if (graph != null && policy.SelectedNode >= graph.NodeCount &&
                    localPlans.TryGetValue(policy.SelectedNode - graph.NodeCount, out var selected))
                {
                    // Preserve the selected arc across ticks. The shared motor receives only its next safe stop.
                    selected.Score = choice.Score; selected.DroneEta = policy.DroneArrival; selected.PlayerEta = policy.PlayerArrival;
                    Vector3 next = tactical.Follow(selected, transform.position, agent.stoppingDistance);
                    return new DecisionOption(choice.Name + "; Control " + selected.Side, next, choice.Score);
                }
                tactical.Clear(policy.Reason);
                return choice;
            }
            finally { validator?.EndDecision(); }
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
                    $"{policy.State} confidence {policy.Confidence:F2}; observation age {policy.ObservationAge(Time.time):F1}s\n{policy.Reason}");
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
