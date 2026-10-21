using UnityEngine;

namespace ShiftFour
{
    // Sound memory deliberately has no Source, exact emission position, player reference or velocity.
    public readonly struct SoundObservation
    {
        public readonly Vector3 Position;
        public readonly NoiseCategory Category;
        public readonly float Strength;
        public readonly float UncertaintyRadius;
        public readonly float EmittedAt;
        public readonly float ExpiresAt;
        public SoundObservation(Vector3 position, NoiseCategory category, float strength, float uncertainty,
            float emittedAt, float expiresAt)
        { Position = position; Category = category; Strength = strength; UncertaintyRadius = uncertainty;
            EmittedAt = emittedAt; ExpiresAt = expiresAt; }
        public bool IsFresh(float now) => now >= EmittedAt && now < ExpiresAt;
    }

    public static class HearingModel
    {
        // The distance penalty is applied after transmission reduces the effective source strength.
        public static float Strength(float distance, float intensity, float sensitivity, float range, float transmission)
        {
            if (!Finite(distance) || !Finite(intensity) || !Finite(sensitivity) || !Finite(range) ||
                !Finite(transmission) || distance < 0f || range <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(intensity) * Mathf.Clamp(sensitivity, 0f, 4f) *
                Mathf.Clamp01(transmission) - distance / range);
        }

        public static Vector3 Estimate(Vector3 position, float radius, int seed)
        {
            // Local integer hash: reproducible inputs, no global UnityEngine.Random state changes.
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            float angle = (hash & 65535u) * (Mathf.PI * 2f / 65536f);
            float length = Mathf.Max(0f, radius) * (.5f + .45f * ((hash >> 16) / 65535f));
            return position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * length;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
