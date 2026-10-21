using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    [DisallowMultipleComponent]
    public sealed class DroneHearingSensor : MonoBehaviour
    {
        private static readonly ProfilerMarker HearingMarker = new ProfilerMarker("ShiftFour.Hearing");
        [SerializeField] private HearingSettings settings = new HearingSettings();
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[8];
        private readonly SoundObservation[] memory = new SoundObservation[4];
        private readonly int[] memoryVersions = new int[4];
        // Value keys only, bounded independently of actors. Never retain NoiseEvent.Source.
        private readonly NoiseKey[] recent = new NoiseKey[32];
        private int memoryNext, memoryCount, recentNext, recentCount;
        private int budgetFrame = -1, frameQueries, secondQueries;
        private float secondStart;
        private WaypointGraph graph;
        private Health health;
        private NavMeshAgent agent;
        private NavMeshPath path;
        private float sensitivity = 1f;
        private bool configured;
        public int EvaluationCount { get; private set; }
        public int PhysicsQueryCount { get; private set; }
        public int NativePathQueryCount { get; private set; }
        public int GraphQueryCount { get; private set; }
        public int BudgetRejections { get; private set; }
        public event System.Action<SoundObservation> ObservationAccepted;
        public int MemoryCapacity => memory.Length;

        private readonly struct NoiseKey
        {
            public readonly Vector3 Position;
            public readonly NoiseCategory Category;
            public readonly float Intensity, Timestamp;
            public NoiseKey(NoiseEvent value)
            { Position = value.Position; Category = value.Category; Intensity = value.Intensity; Timestamp = value.Timestamp; }
            public bool Matches(NoiseEvent value) => Position.Equals(value.Position) && Category == value.Category &&
                Intensity == value.Intensity && Timestamp == value.Timestamp;
        }

        private void Awake()
        {
            settings ??= new HearingSettings(); settings.Validate();
            health = GetComponent<Health>(); agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath();
        }
        private void OnValidate() { settings ??= new HearingSettings(); settings.Validate(); }
        private void OnEnable() => SoundEvents.NoiseEmitted += Hear;
        private void OnDisable() { SoundEvents.NoiseEmitted -= Hear; Clear(); }
        private void OnDestroy() => SoundEvents.NoiseEmitted -= Hear;

        public void Configure(WaypointGraph waypoints, float hearingSensitivity)
        {
            graph = waypoints;
            sensitivity = float.IsNaN(hearingSensitivity) || float.IsInfinity(hearingSensitivity)
                ? 1f : Mathf.Clamp(hearingSensitivity, 0f, 4f);
            settings.Validate(); configured = true; Clear();
        }
        private void Clear() { memoryNext = memoryCount = 0; }

        public void EnableRapidResponse()
        {
            settings.queriesPerFrame = Mathf.Max(settings.queriesPerFrame, 4);
            settings.queriesPerSecond = 16;
        }

        public bool TryGetLatest(out SoundObservation observation) => TryGetLatestAt(Time.time, out observation);

        // Explicit clock is for deterministic simulation; reads never refresh an observation's deadline.
        public bool TryGetLatestAt(float now, out SoundObservation observation)
        {
            observation = default;
            if (!isActiveAndEnabled || !configured || (health != null && !health.IsAlive) ||
                graph == null || (GameSession.Instance != null && GameSession.Instance.Ended)) return false;
            bool found = false;
            for (int i = 0; i < memoryCount; i++)
            {
                int index = (memoryNext - 1 - i + memory.Length) % memory.Length;
                SoundObservation candidate = memory[index];
                if (memoryVersions[index] != graph.Version || !candidate.IsFresh(now)) continue;
                if (!found || candidate.EmittedAt > observation.EmittedAt) { observation = candidate; found = true; }
            }
            return found;
        }

        private void Hear(NoiseEvent noise) => ObserveAt(noise, Time.time);

        public bool ObserveAt(NoiseEvent noise, float now)
        {
            if (noise.Source is Component source && source != null && source.transform.IsChildOf(transform) ||
                noise.Source is GameObject owner && owner != null && owner.transform.IsChildOf(transform)) return false;
            if (!isActiveAndEnabled || !configured || !noise.IsValid || !Finite(now) ||
                now < noise.Timestamp || now - noise.Timestamp > settings.maximumEventAge ||
                noise.Timestamp + settings.memorySeconds <= now || sensitivity <= 0f ||
                (health != null && !health.IsAlive) || graph == null || graph.NodeCount == 0 || graph.NodeCount > 32 ||
                (GameSession.Instance != null && GameSession.Instance.Ended)) return false;
            for (int i = 0; i < recentCount; i++) if (recent[i].Matches(noise)) return false;
            float distance = Vector3.Distance(transform.position, noise.Position);
            if (HearingModel.Strength(distance, noise.Intensity, sensitivity, settings.referenceRange, 1f) < settings.detectionThreshold)
                return false;
            if (!TakeBudget()) return false;
            recent[recentNext] = new NoiseKey(noise); recentNext = (recentNext + 1) % recent.Length;
            recentCount = Mathf.Min(recent.Length, recentCount + 1);
            using var sample = HearingMarker.Auto();
            EvaluationCount++;
            Vector3 ear = transform.position + Vector3.up * 1.25f;
            Vector3 emission = noise.Position + Vector3.up * .5f;
            if (!Transmission(ear, emission, out float transmission)) return false;
            float strength = HearingModel.Strength(distance, noise.Intensity, sensitivity, settings.referenceRange, transmission);
            if (strength < settings.detectionThreshold) return false;
            // MainLab is planar. Preserve source X/Z when attaching to its navigation surface.
            // Never snap an inaccessible off-mesh emission across a nearby wall to make it reachable.
            if (Mathf.Abs(noise.Position.y - transform.position.y) > 3f ||
                !Project(noise.Position, out Vector3 sourceFloor) || !Reachable(sourceFloor)) return false;
            float uncertainty = settings.minimumUncertainty + settings.additionalUncertainty * (1f - strength);
            int seed = unchecked(noise.Position.GetHashCode() * 397 ^ noise.Timestamp.GetHashCode() ^
                (int)noise.Category * 31 ^ GetEntityId().GetHashCode());
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector3 estimate = HearingModel.Estimate(sourceFloor, uncertainty, seed + attempt * 7919);
                if (!Project(estimate, out Vector3 goal) || (goal - sourceFloor).magnitude > uncertainty + .001f ||
                    !Reachable(goal)) continue;
                memory[memoryNext] = new SoundObservation(goal, noise.Category, strength, uncertainty,
                    noise.Timestamp, noise.Timestamp + settings.memorySeconds);
                memoryVersions[memoryNext] = graph.Version;
                SoundObservation accepted = memory[memoryNext];
                memoryNext = (memoryNext + 1) % memory.Length; memoryCount = Mathf.Min(memory.Length, memoryCount + 1);
                ObservationAccepted?.Invoke(accepted);
                return true;
            }
            return false;
        }

        private bool TakeBudget()
        {
            if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; frameQueries = 0; }
            // Budget uses the live engine clock; explicit replay timestamps cannot bypass it.
            if (Time.time - secondStart >= 1f) { secondStart = Time.time; secondQueries = 0; }
            if (frameQueries >= settings.queriesPerFrame || secondQueries >= settings.queriesPerSecond)
            { BudgetRejections++; return false; }
            frameQueries++; secondQueries++; return true;
        }

        private bool Project(Vector3 point, out Vector3 floor)
        {
            floor = default;
            Vector3 onLevel = new Vector3(point.x, transform.position.y, point.z);
            if (!NavMesh.SamplePosition(onLevel, out var hit, .35f, agent != null ? agent.areaMask : NavMesh.AllAreas)) return false;
            Vector3 offset = hit.position - onLevel; offset.y = 0f;
            if (offset.sqrMagnitude > .05f * .05f) return false;
            floor = hit.position; return true;
        }
        private bool Reachable(Vector3 destination)
        {
            int mask = agent != null ? agent.areaMask : NavMesh.AllAreas;
            NativePathQueryCount++;
            if (!NavMesh.CalculatePath(transform.position, destination, mask, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            GraphQueryCount++;
            return graph.FindRoute(transform.position, destination).Count > 0;
        }
        private bool Blocker(Collider collider)
            => collider != null && !collider.transform.IsChildOf(transform) &&
                collider.GetComponentInParent<Health>() == null && collider.GetComponentInParent<ConsoleSwitch>() == null;

        private bool Transmission(Vector3 from, Vector3 to, out float transmission)
        {
            transmission = 1f;
            // Raycasts miss origins/endpoints embedded in solids. Fail closed with fixed buffers.
            for (int endpoint = 0; endpoint < 2; endpoint++)
            {
                PhysicsQueryCount++;
                int n = Physics.OverlapSphereNonAlloc(endpoint == 0 ? from : to, .025f, overlaps,
                    settings.obstacleMask, QueryTriggerInteraction.Ignore);
                if (n == overlaps.Length) return false;
                for (int i = 0; i < n; i++) if (Blocker(overlaps[i])) return false;
            }
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            PhysicsQueryCount++;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude,
                settings.obstacleMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (!Blocker(collider)) continue;
                LabDoor gate = collider.GetComponentInParent<LabDoor>();
                if (gate != null) { if (!gate.IsOpen) return false; continue; }
                transmission *= settings.wallTransmission;
            }
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
