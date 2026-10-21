using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace ShiftFour
{
    // Navigation owns the route; this component accepts only physically clear simulation steps.
    // A CharacterController does not participate in NavMeshAgent avoidance by itself.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class DroneBody : MonoBehaviour
    {
        private static readonly ProfilerMarker BodyMarker = new ProfilerMarker("ShiftFour.DroneBody");
        private static readonly ProfilerMarker SyncMarker = new ProfilerMarker("ShiftFour.SyncTransforms");
        [SerializeField] private Vector3 envelopeCenter = new Vector3(0f, 1.7f, 0f);
        [SerializeField] private Vector3 envelopeExtents = new Vector3(1.4f, .65f, 1.4f);
        [SerializeField, Min(0.01f)] private float clearance = .04f;
        private NavMeshAgent agent;
        private DroneMotor motor;
        private Collider blocker;
        private readonly Collider[] overlaps = new Collider[32];
        private readonly RaycastHit[] sweepHits = new RaycastHit[32];
        private Vector3 avoidanceHeading;
        private float clearanceSpeed;
        private bool detouring;
        private Vector3 detourPoint;
        private static readonly float[] AvoidanceAngles = { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };
        public bool MotionBlocked { get; private set; }
        public float HorizontalRadius => Mathf.Max(envelopeExtents.x, envelopeExtents.z);
        public Vector3 EnvelopeCenter => envelopeCenter;
        public Vector3 EnvelopeExtents => envelopeExtents;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            motor = GetComponent<DroneMotor>();
            agent.updatePosition = false;
        }

        private bool External(Collider other)
            => other != null && !other.transform.IsChildOf(transform) &&
                !Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer);

        public bool CanOccupy(Vector3 position)
        {
            int count=Physics.OverlapBoxNonAlloc(position+envelopeCenter,envelopeExtents,overlaps,
                Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            // A full buffer may have omitted the blocker. Fall back to all hits rather than
            // silently accepting a truncated query in a crowded/dynamically extended scene.
            Collider[] hits=count==overlaps.Length ? Physics.OverlapBox(position+envelopeCenter,
                envelopeExtents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore) : overlaps;
            if(hits!=overlaps)count=hits.Length;
            for(int i=0;i<count;i++)if(External(hits[i]))return false;
            return true;
        }

        private Vector3 Sweep(Vector3 position, Vector3 displacement)
        {
            float distance = displacement.magnitude;
            if (distance < .00001f) return position;
            float allowed = distance;
            int count=Physics.BoxCastNonAlloc(position+envelopeCenter,envelopeExtents,displacement/distance,
                sweepHits,Quaternion.identity,distance+clearance,~0,QueryTriggerInteraction.Ignore);
            RaycastHit[] hits=count==sweepHits.Length ? Physics.BoxCastAll(position+envelopeCenter,
                envelopeExtents,displacement/distance,Quaternion.identity,distance+clearance,~0,
                QueryTriggerInteraction.Ignore) : sweepHits;
            if(hits!=sweepHits)count=hits.Length;
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=hits[i];
                if (External(hit.collider) && hit.distance-clearance<allowed)
                {
                    allowed=Mathf.Max(0f,hit.distance-clearance);
                    blocker=hit.collider;
                }
            }
            return position + displacement * (allowed / distance);
        }

        private Vector3 SteerAroundStaticGeometry(Vector3 start, Vector3 safe)
        {
            // Ground paths were baked for a smaller agent. Locally skirt props/walls that the
            // raised wings cannot clear, without changing the graph route or crossing NavMesh edges.
            // Players, other drones and security doors are temporary holds, never detour shortcuts.
            if (!CanDetour(blocker)) return safe;
            Vector3 forward = agent.steeringTarget - start;
            forward.y = 0f;
            if (forward.sqrMagnitude < .01f) return safe;
            forward.Normalize();
            clearanceSpeed = Mathf.MoveTowards(clearanceSpeed, agent.speed, agent.acceleration * Time.deltaTime);
            float travel = clearanceSpeed * Time.deltaTime;
            float probeDistance = Mathf.Max(.6f, travel);
            float bestScore = float.NegativeInfinity;
            Vector3 best = Vector3.zero;
            foreach (float angle in AvoidanceAngles)
            {
                Vector3 heading = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 probe = start + heading * probeDistance;
                if ((Sweep(start, heading * probeDistance) - probe).sqrMagnitude > .0001f ||
                    agent.Raycast(probe, out _)) continue;
                float score = Vector3.Dot(forward, heading) + .5f * Vector3.Dot(avoidanceHeading, heading);
                if (score <= bestScore) continue;
                bestScore = score;
                best = heading;
            }
            if (best == Vector3.zero) return safe;
            avoidanceHeading = best;
            detourPoint = start + best * probeDistance;
            detouring = true;
            return start + best * Mathf.Min(travel, probeDistance);
        }

        private static bool CanDetour(Collider obstacle)
            => obstacle != null && obstacle.GetComponentInParent<PlayerController>() == null &&
                !(obstacle is CharacterController) && obstacle.GetComponentInParent<DroneBody>() == null &&
                obstacle.GetComponentInParent<LabDoor>() == null;

        private void LateUpdate()
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh || (motor != null && !motor.enabled)) return;
            using var sample = BodyMarker.Auto();
            using (SyncMarker.Auto())
                Physics.SyncTransforms(); // Includes this frame's CharacterController movement and opening gates.
            Vector3 start = transform.position;
            Vector3 desired = agent.nextPosition;
            blocker = null;
            Vector3 safe;
            if (detouring && agent.hasPath && !agent.isStopped)
            {
                // Finish a short validated sidestep before returning control to path following.
                // Otherwise the native solver immediately pulls back toward the obstructed path.
                clearanceSpeed = Mathf.MoveTowards(clearanceSpeed, agent.speed, agent.acceleration * Time.deltaTime);
                safe = Sweep(start, Vector3.ClampMagnitude(detourPoint - start, clearanceSpeed * Time.deltaTime));
                MotionBlocked = true;
                if ((safe - detourPoint).sqrMagnitude < .03f * .03f) detouring = false;
            }
            else
            {
                detouring = false;
                safe = Sweep(start, desired - start);
                MotionBlocked = (safe - desired).sqrMagnitude > .000001f;
                if (!MotionBlocked && agent.hasPath && !agent.isStopped &&
                    agent.remainingDistance > agent.stoppingDistance + .05f)
                {
                    Vector3 ahead = agent.steeringTarget - start;
                    ahead.y = 0f;
                    blocker = null;
                    Sweep(start, Vector3.ClampMagnitude(ahead, .5f));
                    MotionBlocked = CanDetour(blocker);
                }
                if (MotionBlocked) safe = SteerAroundStaticGeometry(start, safe);
                else clearanceSpeed = agent.velocity.magnitude;
            }
            // Never project/warp a collision correction across an edge or a carved gate.
            if (agent.Raycast(safe, out _)) safe = start;
            transform.position = safe;
            agent.nextPosition = safe;
            if (MotionBlocked) agent.velocity = Vector3.zero;
        }

        private void OnDisable()
        {
            if (agent != null) agent.updatePosition = true;
        }
        private void OnEnable()
        {
            if (agent != null) agent.updatePosition = false;
        }
    }
}
