using System;
using UnityEngine;

namespace ShiftFour
{
    /// <summary>Distance-paced disturbances. Sprint is a producer option, not a movement mechanic.</summary>
    [Serializable]
    public sealed class MovementNoiseCadence
    {
        [SerializeField, Min(.2f), Tooltip("Horizontal metres between walking disturbances.")]
        private float walkStepDistance = 1.6f;
        [SerializeField, Min(.2f), Tooltip("Horizontal metres between sprint disturbances; no sprint input is added.")]
        private float sprintStepDistance = 2.1f;
        [SerializeField, Range(.15f, 1f), Tooltip("Minimum seconds of actual movement between disturbances.")]
        private float minimumInterval = .22f;
        [SerializeField, Range(0f, 1f), Tooltip("Normalized sensing strength, not AudioSource volume. Walking target: 4–6 m.")]
        private float walkingIntensity = .2f;
        [SerializeField, Range(0f, 1f), Tooltip("Normalized sensing strength. Future sprint target: 10–12 m.")]
        private float sprintingIntensity = .45f;

        [NonSerialized] private float distance;
        [NonSerialized] private float elapsed;

        public float Intensity(bool sprinting) => sprinting ? sprintingIntensity : walkingIntensity;

        public void Validate()
        {
            walkStepDistance = Valid(walkStepDistance, 1.6f, .2f, 4f);
            sprintStepDistance = Valid(sprintStepDistance, 2.1f, .2f, 4f);
            minimumInterval = Valid(minimumInterval, .22f, .15f, 1f);
            walkingIntensity = Valid(walkingIntensity, .2f, 0f, 1f);
            sprintingIntensity = Valid(sprintingIntensity, .45f, 0f, 1f);
        }

        public void Reset() { distance = 0f; elapsed = 0f; }

        // Call once after a movement operation, using displacement after collision resolution.
        // Idle/airborne samples clear partial steps; long stalls/warps never catch up in a burst.
        public bool Advance(Vector3 displacement, bool grounded, bool locomoting, bool sprinting, float deltaTime)
        {
            float horizontal = new Vector2(displacement.x, displacement.z).magnitude;
            // The tiny distance tolerance keeps the 20 m/s bound inclusive despite float rounding.
            if (!grounded || !locomoting || !Finite(deltaTime) || deltaTime <= 0f || deltaTime > .25f ||
                !Finite(horizontal) || horizontal < .1f * deltaTime || horizontal > 20f * deltaTime + .00001f)
            {
                Reset();
                return false;
            }

            distance += horizontal;
            elapsed += deltaTime;
            if (distance < (sprinting ? sprintStepDistance : walkStepDistance) || elapsed < minimumInterval)
                return false;
            Reset();
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Valid(float value, float fallback, float min, float max)
            => Finite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }
}
