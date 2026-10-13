using UnityEngine;

namespace ShiftFour
{
    public struct DecisionOption
    {
        public string Name;
        public Vector3 Destination;
        public float Score;
        public DecisionOption(string name, Vector3 destination, float score)
        { Name = name; Destination = destination; Score = score; }
    }

    [RequireComponent(typeof(Health), typeof(DroneMotor))]
    public abstract class DroneBrain : MonoBehaviour
    {
        [SerializeField] private string agentName;
        [SerializeField] private Vector3 home;
        [SerializeField] private float visionDistance = 15f;
        [SerializeField] private float attackRange = 11f;
        [SerializeField] private float attackInterval = 1.1f;

        private Health health;
        private DroneMotor motor;
        private PlayerController player;
        private float nextDecision;
        private float nextShot;
        private float lastSeenAt = float.NegativeInfinity;
        private Vector3 lastKnown;
        private Vector3 observedVelocity;
        private Vector3 previousObservation;
        private float previousObservationTime;
        private Renderer visualRenderer;
        private Color normalColor;
        private float flashUntil;

        public string AgentName => agentName;
        public string CurrentDecision { get; private set; } = "Idle";
        public Vector3 Home => home;
        protected Vector3 LastKnown => lastKnown;
        protected float LastSeenAt => lastSeenAt;
        protected Vector3 ObservedVelocity => observedVelocity;
        protected PlayerController Player => player;
        protected DroneMotor Motor => motor;
        protected bool Alive => health != null && health.IsAlive;

        public void Configure(string label, Vector3 post)
        {
            agentName = label;
            home = post;
        }

        protected virtual void Start()
        {
            health = GetComponent<Health>();
            motor = GetComponent<DroneMotor>();
            player = GameSession.Instance != null ? GameSession.Instance.Player : null;
            health.Died += OnDied;
            lastKnown = home;
            visualRenderer = GetComponentInChildren<Renderer>();
            if (visualRenderer != null) normalColor = visualRenderer.material.color;
        }

        private void Update()
        {
            if (visualRenderer != null && Alive && flashUntil > 0f && Time.time > flashUntil)
            {
                visualRenderer.material.color = normalColor;
                flashUntil = 0f;
            }
            if (!Alive || player == null || (GameSession.Instance != null && GameSession.Instance.Ended)) return;
            if (Time.time < nextDecision) return;
            nextDecision = Time.time + 0.3f;
            bool visible = CanSeePlayer();
            Vector3 position = player.transform.position;
            float distance = Vector3.Distance(transform.position, position);
            if (visible)
            {
                float elapsed = Time.time - previousObservationTime;
                if (elapsed > 0.1f && elapsed < 3f)
                    observedVelocity = (position - previousObservation) / elapsed;
                previousObservation = position;
                previousObservationTime = Time.time;
                lastKnown = position;
                lastSeenAt = Time.time;
            }
            DecisionOption choice = Decide(visible, position, distance);
            CurrentDecision = choice.Name;
            motor.GoTo(choice.Destination);
            if (visible && distance <= attackRange) TryShoot();
        }

        protected abstract DecisionOption Decide(bool visible, Vector3 playerPosition, float distance);

        protected static DecisionOption Best(params DecisionOption[] options)
        {
            DecisionOption winner = options[0];
            foreach (DecisionOption option in options)
                if (option.Score > winner.Score) winner = option;
            return winner;
        }

        protected static DecisionOption Option(string name, Vector3 target, float score)
            => new DecisionOption(name, target, score);

        protected bool Reachable(Vector3 target)
        {
            WaypointGraph graph = GameSession.Instance != null ? GameSession.Instance.Graph : null;
            return graph != null && graph.FindRoute(transform.position, target).Count > 0;
        }

        private bool CanSeePlayer()
        {
            Vector3 origin = transform.position + Vector3.up * 1.25f;
            Vector3 vector = player.EyePosition - origin;
            if (vector.magnitude > visionDistance) return false;
            if (Vector3.Angle(transform.forward, vector) > 85f) return false;
            return Physics.Raycast(origin, vector.normalized, out RaycastHit hit, visionDistance,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PlayerController>() == player;
        }

        private void TryShoot()
        {
            if (Time.time < nextShot) return;
            nextShot = Time.time + attackInterval;
            if (visualRenderer != null)
            {
                visualRenderer.material.color = Color.white;
                flashUntil = Time.time + 0.16f;
            }
            Vector3 origin = transform.position + Vector3.up * 1.25f;
            Vector3 vector = player.EyePosition - origin;
            // A second raycast at fire time makes solid shelves and pillars real cover.
            if (Physics.Raycast(origin, vector.normalized, out RaycastHit hit, attackRange + 0.5f,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<PlayerController>() == player)
                player.GetComponent<Health>().TakeDamage(10f);
        }

        private void OnDied(Health dead)
        {
            motor.Stop();
            Collider body = GetComponent<Collider>();
            if (body != null) body.enabled = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.material.color = new Color(0.28f, 0.28f, 0.28f);
            GameSession.Instance?.DroneDestroyed(this);
        }
    }
}
