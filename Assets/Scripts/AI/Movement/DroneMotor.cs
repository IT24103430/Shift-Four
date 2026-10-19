using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace ShiftFour
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DroneMotor : MonoBehaviour
    {
        private static readonly ProfilerMarker DroneMotorProfile=new ProfilerMarker("ShiftFour.DroneMotor");

        private static readonly ProfilerMarker CombatGoalMarker = new ProfilerMarker("ShiftFour.GoToCombat");
        private enum MovementState { NoGoal, Pending, Following, Arrived, Blocked, Stopped }
        private enum BlockReason { None, NoGraphRoute, Navigation, Unavailable }

        private const float IntermediateDistance = 0.25f;
        private const float ArrivalSlack = 0.05f;
        private const float ArrivalSpeed = 0.1f;
        private const float GoalTolerance = 0.1f;
        private const float PlanInterval = 0.3f;
        private const float PendingTimeout = 3f;
        private const float ProgressTimeout = 2f;
        private const int MaxRetries = 2;
        private static readonly float[] ApproachAngles = { 0f, 45f, -45f, 90f, -90f };
        [SerializeField] private Transform visual;
        [SerializeField] private float turnSpeed = 7f;
        [SerializeField, Min(2f)] private float combatStandoff = 2.8f;
        private NavMeshAgent agent;
        private WaypointGraph graph;
        private List<Vector3> route = new List<Vector3>();
        private int step;
        private int graphVersion = -1;
        private Vector3 goal;
        private float bobClock;
        private MovementState state;
        private BlockReason blockedBy;
        private bool hasGoal;
        private bool replanRequested;
        private float finalStoppingDistance;
        private float nextPlanAt;
        private float pendingSince;
        private int requestedFrame;
        private int retryCount;
        private bool retryGraphFailure;
        private float retryAt = float.PositiveInfinity;
        private float progressRemaining;
        private float progressAt;
        private bool turning;
        private Transform cachedVisual;
        private Vector3 visualRestPosition;
        private DroneRotorVisuals visualOwner;
        private bool triedVisualRecovery;
        private DroneBody body;
        private bool hasCombatGoal;
        private Vector3 combatGoal;
        private Vector3 combatAnchor;

        public float DistanceToGoal => Vector3.Distance(transform.position, goal);
        public bool HasRoute => (state == MovementState.Pending || state == MovementState.Following)
            && step < route.Count;

        public void Configure(Transform model)
        {
            visual = model;
            CacheVisualPosition();
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<DroneBody>();
            agent.updateRotation = false;
            finalStoppingDistance = agent.stoppingDistance;
            CacheVisualPosition();
        }

        private void Start()
        {
            ResolveGraph();
        }

        public void GoTo(Vector3 destination)
        {
            if (state == MovementState.Stopped || !IsFinite(destination)) return;
            bool changed = !hasGoal || (goal - destination).sqrMagnitude > GoalTolerance * GoalTolerance;
            if (changed)
            {
                goal = destination;
                hasGoal = true;
                replanRequested = true;
                retryCount = 0;
                retryGraphFailure = false;
                retryAt = float.PositiveInfinity;
            }
            ResolveGraph();
            CheckInvalidation();
            TryPlan();
        }

        // Preserve policy destinations (cover/flanks/interception), offset only direct approaches.
        // Cache the approach side so small movements and policy ticks do not churn destinations.
        public void GoToCombat(Vector3 destination, PlayerController player, bool visible)
        {
            using var sample = CombatGoalMarker.Auto();
            Vector3 target = player.transform.position;
            Vector3 offset = destination - target;
            offset.y = 0f;
            if (!visible || offset.sqrMagnitude >= combatStandoff * combatStandoff)
            {
                hasCombatGoal = false;
                GoTo(destination);
                return;
            }
            ResolveGraph();
            if (!hasCombatGoal || (body != null && !body.CanOccupy(combatGoal)) ||
                (target - combatAnchor).sqrMagnitude > .5f * .5f ||
                (graph != null && graphVersion != graph.Version))
            {
                hasCombatGoal = false;
                Vector3 away = transform.position - target;
                away.y = 0f;
                if (away.sqrMagnitude < .01f) away = -player.transform.forward;
                away.Normalize();
                float radius = Mathf.Max(combatStandoff, body != null ? body.HorizontalRadius + .8f : 2f);
                foreach (float angle in ApproachAngles)
                {
                    Vector3 point = target + Quaternion.Euler(0f, angle, 0f) * away * radius;
                    if (!NavigationReady || !NavMesh.SamplePosition(point, out NavMeshHit hit, .35f, agent.areaMask) ||
                        (body != null && !body.CanOccupy(hit.position)) ||
                        graph == null || graph.FindRoute(transform.position, hit.position).Count == 0) continue;
                    combatGoal = hit.position;
                    combatAnchor = target;
                    hasCombatGoal = true;
                    break;
                }
            }
            if (hasCombatGoal) GoTo(combatGoal);
            else HoldCombatPosition();
        }

        // No collision-safe standoff exists. Hold without terminal Stop(), then reconsider on
        // the next policy tick. Falling back to the player root defeats the standoff contract.
        private void HoldCombatPosition()
        {
            if (state == MovementState.Stopped) return;
            hasGoal = false;
            replanRequested = false;
            route.Clear();
            step = 0;
            state = MovementState.NoGoal;
            blockedBy = BlockReason.None;
            retryAt = float.PositiveInfinity;
            if (!NavigationReady) return;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            agent.stoppingDistance = finalStoppingDistance;
        }

        private void Update()
        {
            using var profileSample=DroneMotorProfile.Auto();
            if (state == MovementState.Stopped) return;
            ResolveGraph();
            CheckInvalidation();
            TryPlan();
            if (HasRoute)
            {
                if (!NavigationReady) Block(BlockReason.Unavailable);
                else UpdateRoute();
            }
            if (!NavigationReady) return;
            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.15f * 0.15f) turning = false;
            else if (velocity.sqrMagnitude > 0.3f * 0.3f) turning = true;
            if (turning)
                transform.rotation = TurnTowards(transform.rotation, velocity, turnSpeed, agent.angularSpeed, Time.deltaTime);
            CacheVisualPosition();
            if (visual != null && visualOwner == null)
            {
                bobClock += Time.deltaTime * 3f;
                visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(bobClock) * 0.08f);
            }
        }

        private void CacheVisualPosition()
        {
            if (visual == null && !triedVisualRecovery)
            {
                triedVisualRecovery = true;
                // Older scene instances lost this reference. Recover only an unambiguous
                // existing visual component; never guess a renderer or modify scene assets.
                var candidates = GetComponentsInChildren<DroneRotorVisuals>(true);
                if (candidates.Length == 1 && candidates[0].transform != transform)
                    visual = candidates[0].transform;
            }
            if (cachedVisual == visual) return;
            cachedVisual = visual;
            if (visual != null)
            {
                visualRestPosition = visual.localPosition;
                visualOwner = visual.GetComponent<DroneRotorVisuals>();
            }
        }

        private static Quaternion TurnTowards(Quaternion current, Vector3 heading, float responsiveness,
            float degreesPerSecond, float deltaTime)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) return current;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, responsiveness) * deltaTime);
            Quaternion target = Quaternion.Slerp(current, Quaternion.LookRotation(heading), blend);
            return Quaternion.RotateTowards(current, target, Mathf.Max(0f, degreesPerSecond) * deltaTime);
        }

        private bool NavigationReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        private static bool IsFinite(Vector3 point)
            => !float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude);

        private void ResolveGraph()
        {
            if (graph == null && GameSession.Instance != null) graph = GameSession.Instance.Graph;
        }

        private void CheckInvalidation()
        {
            if (!hasGoal) return;
            if (!NavigationReady && blockedBy != BlockReason.Unavailable) Block(BlockReason.Unavailable);
            if (graph != null && graphVersion != graph.Version)
            {
                retryGraphFailure = graphVersion >= 0;
                graphVersion = graph.Version;
                retryCount = 0;
                if (state != MovementState.Arrived)
                {
                    replanRequested = true;
                    // Stop following an obsolete route even if a new request is cooling down.
                    if (HasRoute) Block(BlockReason.Navigation);
                }
            }
            if (state == MovementState.Arrived &&
                Vector3.Distance(transform.position, goal) > finalStoppingDistance + ArrivalSlack * 2f)
                replanRequested = true;
            if (state == MovementState.Blocked && blockedBy == BlockReason.Unavailable)
            {
                replanRequested = true;
                if (NavigationReady)
                {
                    retryCount = 0;
                    retryGraphFailure = true;
                }
            }
            if (state == MovementState.Blocked && blockedBy == BlockReason.Navigation &&
                retryCount < MaxRetries && Time.time >= retryAt)
            {
                retryCount++;
                retryAt = float.PositiveInfinity;
                replanRequested = true;
            }
        }

        private void TryPlan()
        {
            if (!hasGoal || !replanRequested || Time.time < nextPlanAt) return;
            nextPlanAt = Time.time + PlanInterval;
            if (graph == null || !NavigationReady)
            {
                Block(BlockReason.Unavailable);
                return;
            }
            replanRequested = false;
            graphVersion = graph.Version;
            route = graph.FindRoute(transform.position, goal);
            step = 0;
            // A gate version can arrive before carving settles. Newer graph implementations
            // may report this as an empty route; allow the same bounded recovery window.
            if (route.Count == 0)
            { Block(retryGraphFailure ? BlockReason.Navigation : BlockReason.NoGraphRoute); return; }
            foreach (Vector3 point in route)
                if (!IsFinite(point)) { Block(BlockReason.NoGraphRoute); return; }
            // Older graph revisions can omit a destination close to the final node.
            // Only extend a successful graph route; every leg still needs a valid NavMesh path.
            if (route[route.Count - 1] != goal) route.Add(goal);
            Advance();
        }

        private void UpdateRoute()
        {
            if (Time.frameCount <= requestedFrame) return;
            if (agent.pathPending)
            {
                if (Time.time - pendingSince > PendingTimeout) Block(BlockReason.Navigation);
                return;
            }
            float remaining = agent.remainingDistance;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            { Block(BlockReason.Navigation); return; }
            // Native path calculation can finish before remainingDistance becomes available.
            if (float.IsInfinity(remaining) || float.IsNaN(remaining))
            {
                state = MovementState.Pending;
                if (body != null && body.MotionBlocked && agent.hasPath) pendingSince = Time.time;
                if (Time.time - pendingSince > PendingTimeout) Block(BlockReason.Navigation);
                return;
            }
            state = MovementState.Following;
            bool final = step == route.Count - 1;
            float distance = final ? finalStoppingDistance : IntermediateDistance;
            if (!agent.hasPath && !NearbyPointIsClear(route[step], distance + ArrivalSlack))
            { Block(BlockReason.Navigation); return; }
            if (!CanArrive(false, agent.pathStatus, remaining, distance, agent.velocity.sqrMagnitude, final))
            {
                if (remaining < progressRemaining - 0.1f)
                {
                    progressRemaining = remaining;
                    progressAt = Time.time;
                }
                // Physics clearance can temporarily hold a valid route. Keep it resumable.
                else if (body != null && body.MotionBlocked) progressAt = Time.time;
                else if (Time.time - progressAt > ProgressTimeout) Block(BlockReason.Navigation);
                return;
            }
            step++;
            Advance();
        }

        private static bool CanArrive(bool pending, NavMeshPathStatus status, float remaining,
            float distance, float speedSquared, bool final)
            => !pending && status == NavMeshPathStatus.PathComplete &&
                !float.IsInfinity(remaining) && !float.IsNaN(remaining) && remaining >= 0f &&
                remaining <= distance + ArrivalSlack && (!final || speedSquared <= ArrivalSpeed * ArrivalSpeed);

        private bool NearbyPointIsClear(Vector3 point, float distance)
            => Vector3.Distance(transform.position, point) <= distance && !agent.Raycast(point, out _);

        private void DestinationRequested(bool accepted)
        {
            if (!accepted) { Block(BlockReason.Navigation); return; }
            state = MovementState.Pending;
            blockedBy = BlockReason.None;
            pendingSince = Time.time;
            requestedFrame = Time.frameCount;
            progressAt = Time.time;
            progressRemaining = float.PositiveInfinity;
        }

        private void Advance()
        {
            if (!NavigationReady) { Block(BlockReason.Unavailable); return; }
            if (step >= route.Count)
            {
                state = replanRequested ? MovementState.Pending : MovementState.Arrived;
                agent.ResetPath();
                agent.stoppingDistance = finalStoppingDistance;
                return;
            }
            agent.stoppingDistance = step == route.Count - 1 ? finalStoppingDistance : IntermediateDistance;
            agent.isStopped = false;
            DestinationRequested(agent.SetDestination(route[step]));
        }

        private void Block(BlockReason reason)
        {
            state = MovementState.Blocked;
            blockedBy = reason;
            if (reason == BlockReason.Navigation) retryGraphFailure = true;
            retryAt = reason == BlockReason.Navigation && retryCount < MaxRetries
                ? Time.time + 0.5f * (retryCount + 1) : float.PositiveInfinity;
            route.Clear();
            step = 0;
            if (NavigationReady)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
                agent.stoppingDistance = finalStoppingDistance;
            }
        }

        public void Stop()
        {
            state = MovementState.Stopped;
            hasGoal = false;
            replanRequested = false;
            retryAt = float.PositiveInfinity;
            route.Clear();
            step = 0;
            if (NavigationReady)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
                agent.stoppingDistance = finalStoppingDistance;
            }
            enabled = false;
        }
    }
}
