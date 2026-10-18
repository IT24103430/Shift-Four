using UnityEngine;

namespace ShiftFour
{
    // Member 3: scores cover positions using protection,
    // route cost and firing visibility.
    public sealed class GuardBrain : DroneBrain
    {
        [Header("Guard Cover Settings")]
        [SerializeField] private CoverPoint[] coverPoints;

        
        private CoverPoint currentCover;
        

        // New cover-point based setup
        public void SetCoverPoints(CoverPoint[] points)
        {
            coverPoints = points;
        }

        // Keep this for compatibility with the existing baseline generator.
        public void SetCover(Vector3 first, Vector3 second)
        {
            // Intentionally left empty.
            // The real cover points are assigned through SetCoverPoints / Inspector.
        }

        protected override DecisionOption Decide(
            bool visible,
            Vector3 playerPosition,
            float distance)
        {
            bool alert =
                visible ||
                Time.time - LastSeenAt < 6f;

            Vector3 threat =
                visible ? playerPosition : LastKnown;

            DecisionOption best =
                Option(
                    "Guard server room",
                    Home,
                    10f
                );

            if (!alert)
                return best;

            CoverPoint bestCover = FindBestCover(threat);

            if (bestCover != null)
            {
                currentCover = bestCover;

                Transform playerTransform = null;

                if (GameSession.Instance != null &&
                    GameSession.Instance.Player != null)
                {
                    playerTransform =
                        GameSession.Instance.Player.transform;
                }

                string decisionName =
                    bestCover.IsValidCover(playerTransform)
                        ? "Take protected cover"
                        : "Reposition";

                best = Best(
                    best,
                    Option(
                        decisionName,
                        bestCover.transform.position,
                        ScoreCover(bestCover, threat)
                    )
                );
            }
            else
            {
                // Explicit fallback so the Guard never freezes.
                best = Best(
                    best,
                    Option(
                        "Fallback pursuit",
                        threat,
                        visible ? 35f : 15f
                    )
                );
            }

            if (visible && distance < 4f)
            {
                best = Best(
                    best,
                    Option(
                        "Pressure visible player",
                        threat,
                        75f
                    )
                );
            }

            return best;
        }

        private CoverPoint FindBestCover(Vector3 threat)
        {
            if (coverPoints == null ||
                coverPoints.Length == 0)
            {
                return null;
            }

            CoverPoint bestCover = null;
            float bestScore = float.MinValue;

            foreach (CoverPoint point in coverPoints)
            {
                if (point == null)
                    continue;

                Vector3 position =
                    point.transform.position;

                if (!Reachable(position))
                    continue;

                float score =
                    ScoreCover(point, threat);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCover = point;
                }
            }

            return bestCover;
        }

        private float ScoreCover(
            CoverPoint point,
            Vector3 threat)
        {
            Vector3 position =
                point.transform.position;

            float routeCost =
                Vector3.Distance(
                    transform.position,
                    position
                );

            float protectionScore =
                IsProtected(position, threat)
                    ? point.protectionWeight
                    : 0f;

            float visibilityScore =
                HasFiringVisibility(position, threat)
                    ? point.visibilityWeight
                    : 0f;

            float score =
                protectionScore +
                visibilityScore -
                routeCost *
                point.routeCostWeight;

            return score;
        }

        private bool IsProtected(
            Vector3 coverPosition,
            Vector3 threat)
        {
            Vector3 from =
                coverPosition +
                Vector3.up * 1.15f;

            Vector3 to =
                threat +
                Vector3.up * 1.4f;

            Vector3 direction =
                to - from;

            if (Physics.Raycast(
                from,
                direction.normalized,
                out RaycastHit hit,
                direction.magnitude,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                return
                    hit.collider
                    .GetComponentInParent<PlayerController>()
                    == null;
            }

            return false;
        }

        private bool HasFiringVisibility(
            Vector3 coverPosition,
            Vector3 threat)
        {
            Vector3 from =
                coverPosition +
                Vector3.up * 1.4f;

            Vector3 to =
                threat +
                Vector3.up * 1.4f;

            Vector3 direction =
                to - from;

            if (Physics.Raycast(
                from,
                direction.normalized,
                out RaycastHit hit,
                direction.magnitude,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                return
                    hit.collider
                    .GetComponentInParent<PlayerController>()
                    != null;
            }

            return false;
        }
    }
}