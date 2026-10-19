using UnityEngine;

namespace ShiftFour
{
    // Member 2: two lateral approaches, route cost and a pursuit fallback.
    public sealed class FlankerBrain : DroneBrain
    {
        protected override DecisionOption Decide(bool visible, Vector3 playerPosition, float distance)
        {
            bool recentlySeen = Time.time - LastSeenAt < 5f;
            Vector3 target = visible ? playerPosition : LastKnown;
            Vector3 toward = target - transform.position;
            toward.y = 0f;
            Vector3 side = toward.sqrMagnitude > 0.1f
                ? new Vector3(-toward.z, 0f, toward.x).normalized * 5f
                : Vector3.right * 5f;
            Vector3 left = target + side;
            Vector3 right = target - side;
            float leftScore = recentlySeen && Reachable(left)
                ? 57f - Vector3.Distance(transform.position, left) * 0.35f : -1f;
            float rightScore = recentlySeen && Reachable(right)
                ? 57f - Vector3.Distance(transform.position, right) * 0.35f : -1f;
            return Best(
                Option("Hold storage", Home, 10f),
                Option("Pursue last sighting", target, recentlySeen ? 38f : -1f),
                Option("Flank left", left, leftScore),
                Option("Flank right", right, rightScore),
                Option("Engage at close range", target, visible && distance < 3.5f ? 70f : -1f));
        }
    }
}
