using UnityEngine;

namespace ShiftFour
{
    public enum NoiseCategory { Footstep = 0, Sprint = 1, Gunshot = 2, ConsoleInteraction = 3, Impact = 4 }

    /// <summary>
    /// An emission snapshot, not a tracked target. Source is identity only; consumers must
    /// use Position rather than reading a source's later/hidden transform.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly NoiseCategory Category;
        public readonly float Intensity;
        public readonly float Timestamp;
        public readonly Object Source;

        public NoiseEvent(Vector3 position, NoiseCategory category, float intensity, float timestamp, Object source = null)
        { Position = position; Category = category; Intensity = intensity; Timestamp = timestamp; Source = source; }

        // Intensity is normalized source strength, not metres or visual confidence.
        // Zero is silent and is intentionally not dispatched.
        public bool IsValid => (uint)Category <= (uint)NoiseCategory.Impact &&
            Finite(Position.x) && Finite(Position.y) && Finite(Position.z) &&
            Finite(Intensity) && Intensity > 0f && Intensity <= 1f &&
            Finite(Timestamp) && Timestamp >= 0f;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
