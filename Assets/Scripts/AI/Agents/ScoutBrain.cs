using UnityEngine;

namespace ShiftFour
{
    // Member 1: event memory, patrol, investigation, and last-known-position search.
    public sealed class ScoutBrain : DroneBrain
    {
        private int patrolIndex;

        protected override DecisionOption Decide(bool visible, Vector3 playerPosition, float distance)
        {
            Vector3 patrolA = Home + new Vector3(-4f, 0f, -3f);
            Vector3 patrolB = Home + new Vector3(4f, 0f, 3f);
            Vector3 patrol = patrolIndex == 0 ? patrolA : patrolB;
            if (Vector3.Distance(transform.position, patrol) < 1.6f) patrolIndex = 1 - patrolIndex;
            SoundObservation sound = default;
            bool heard = Hearing != null && Hearing.TryGetLatest(out sound);
            return Best(
                Option("Patrol", patrol, 10f),
                Option("Investigate sound", sound.Position, heard && !visible ? 45f : -1f),
                Option("Search last sighting", LastKnown,
                    Time.time - LastSeenAt < 6f && !visible ? 60f : -1f),
                Option("Chase visible player", playerPosition, visible ? 80f : -1f));
        }
    }
}
