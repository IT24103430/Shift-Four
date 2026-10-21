using UnityEngine;

namespace ShiftFour
{
    // Member 3: scores fixed cover positions, changing cover as the player moves.
    public sealed class GuardBrain : DroneBrain
    {
        [SerializeField] private Vector3[] coverPoints;
        [SerializeField] private bool aggressivePursuit = true;

        public void SetCover(Vector3 first, Vector3 second) => coverPoints = new[] { first, second };

        protected override DecisionOption Decide(bool visible, Vector3 playerPosition, float distance)
        {
            bool alert = visible || Time.time - LastSeenAt < 6f;
            Vector3 threat = visible ? playerPosition : LastKnown;
            DecisionOption best = Option("Guard server room", Home, 10f);
            if (alert && coverPoints != null)
            {
                foreach (Vector3 point in coverPoints)
                {
                    if (!Reachable(point)) continue;
                    // A short ray from cover toward the player tests whether solid geometry protects it.
                    Vector3 from = point + Vector3.up * 1.15f;
                    Vector3 to = threat + Vector3.up * 1.4f;
                    bool blocked = Physics.Raycast(from, (to - from).normalized,
                        out RaycastHit hit, Vector3.Distance(from, to), ~0,
                        QueryTriggerInteraction.Ignore) &&
                        hit.collider.GetComponentInParent<PlayerController>() == null;
                    float score = 40f + (blocked ? 22f : 0f)
                        - Vector3.Distance(transform.position, point) * 0.3f
                        - Vector3.Distance(threat, point) * 0.1f;
                    best = Best(best, Option(blocked ? "Take protected cover" : "Reposition", point, score));
                }
            }
            return Best(best,
                Option("Pressure visible player", threat, visible && (aggressivePursuit || distance < 4f) ? 75f : -1f));
        }
    }
}
