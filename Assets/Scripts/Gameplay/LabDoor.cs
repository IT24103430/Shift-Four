using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class LabDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private int gateId = -1;
        [SerializeField] private string room;
        [SerializeField] private string drone;
        [SerializeField, Min(.1f)] private float boltSeconds = .4f;
        [SerializeField, Min(.1f)] private float slideSeconds = 1.2f;
        [SerializeField, Min(1.6f)] private float slideDistance = 2.15f;
        private Transform left, right, leftBolt, rightBolt;
        private Vector3 leftClosed, rightClosed, leftBoltClosed, rightBoltClosed;
        private BoxCollider barrier;
        private NavMeshObstacle obstacle;
        private float elapsed;
        private bool opening;
        private bool ready;
        public int GateId => gateId;
        public bool IsOpen { get; private set; }
        public string Status => IsOpen ? "OPEN" : opening ? "UNLOCKING" : "LOCKED";
        public string Prompt => room + " gate: " + Status + " — defeat " + drone + " and activate console";

        public void Configure(int id, string roomName, string agentName)
        {
            if (!MissionProgress.Valid(id)) throw new System.ArgumentOutOfRangeException(nameof(id));
            gateId = id; room = roomName; drone = agentName;
        }

        private void Awake()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            barrier = GetComponent<BoxCollider>();
            barrier.enabled = true;
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = true;
            Transform assembly = transform.Find("Door assembly");
            left = assembly?.Find("LeftPanel"); right = assembly?.Find("RightPanel");
            leftBolt = assembly?.Find("LeftBolt"); rightBolt = assembly?.Find("RightBolt");
            ready = left != null && right != null && leftBolt != null && rightBolt != null && MissionProgress.Valid(gateId);
            if (!ready) { Debug.LogError("Gate requires configured identity and four animation parts: " + name, this); return; }
            leftClosed = left.localPosition; rightClosed = right.localPosition;
            leftBoltClosed = leftBolt.localPosition; rightBoltClosed = rightBolt.localPosition;
        }
        private void Start() { if (ready) GameSession.Instance?.Graph?.SetGateOpen(gateId, false); }
        public void Interact() { } // Read-only prompt; prerequisites exclusively control unlocking.

        private void FixedUpdate()
        {
            if (!ready || IsOpen) return;
            if (!opening && GameSession.Instance != null && !GameSession.Instance.Ended && GameSession.Instance.Progress.CanOpen(gateId)) opening = true;
            if (!opening) return;
            elapsed += Time.fixedDeltaTime;
            float bolt = Mathf.Clamp01(elapsed / Mathf.Max(.1f, boltSeconds));
            leftBolt.localPosition = leftBoltClosed + Vector3.left * (.35f * bolt);
            rightBolt.localPosition = rightBoltClosed + Vector3.right * (.35f * bolt);
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - boltSeconds) / Mathf.Max(.1f, slideSeconds)));
            left.localPosition = leftClosed + Vector3.left * (slideDistance * slide);
            right.localPosition = rightClosed + Vector3.right * (slideDistance * slide);
            if (elapsed < boltSeconds + slideSeconds) return;
            // Root never moves; visual panels have no colliders. Release all passage barriers together.
            barrier.enabled = false;
            if (obstacle != null) obstacle.enabled = false;
            IsOpen = true;
            GameSession.Instance?.Graph?.SetGateOpen(gateId, true);
        }
    }
}
