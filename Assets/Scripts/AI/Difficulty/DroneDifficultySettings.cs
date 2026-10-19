using System;
using UnityEngine;

namespace ShiftFour
{
    public enum DroneArchetype { Scout, Guard, Interceptor, Flanker }

    /// <summary>Value data only. Consumers receive a validated copy, never mutable asset state.</summary>
    [Serializable]
    public struct DroneDifficultySettings
    {
        [Min(1f)] public float maximumHealth;
        [Header("Movement (m/s)")]
        [Min(.1f)] public float patrolSpeed;
        [Min(.1f)] public float investigationSpeed;
        [Min(.1f)] public float chaseSpeed;
        [Tooltip("Movement acceleration in m/s squared."), Min(.1f)] public float acceleration;
        [Tooltip("Maximum turn speed in degrees per second; distinct from motor turn responsiveness."), Min(1f)]
        public float angularSpeed;
        [Header("Perception configuration — consumed by later tasks")]
        [Tooltip("Relative hearing multiplier: 1 is normal, 0 disables hearing."), Range(0f, 4f)]
        public float hearingSensitivity;
        [Tooltip("Visual detection distance in metres."), Min(.1f)] public float visionRange;
        [Range(.01f, .98f)] public float suspiciousThreshold;
        [Range(.02f, .99f)] public float investigateThreshold;
        [Range(.03f, 1f)] public float combatThreshold;
        [Tooltip("Bounded search duration in seconds."), Min(.1f)] public float searchDuration;

        // Safe starting data for newly created assets and recovery from non-finite input.
        // Per-drone defaults live in the four authored assets, not in policy scripts.
        public static DroneDifficultySettings Default => new DroneDifficultySettings
        {
            maximumHealth = 100f, patrolSpeed = 1.8f, investigationSpeed = 2.2f, chaseSpeed = 2.6f,
            acceleration = 8f, angularSpeed = 120f, hearingSensitivity = 1f, visionRange = 15f,
            suspiciousThreshold = .25f, investigateThreshold = .6f, combatThreshold = .85f,
            searchDuration = 5f
        };

        public DroneDifficultySettings Validated()
        {
            var value = this;
            var fallback = Default;
            value.maximumHealth = AtLeast(maximumHealth, 1f, fallback.maximumHealth);
            value.patrolSpeed = AtLeast(patrolSpeed, .1f, fallback.patrolSpeed);
            value.chaseSpeed = Mathf.Max(value.patrolSpeed, AtLeast(chaseSpeed, .1f, fallback.chaseSpeed));
            value.investigationSpeed = Mathf.Clamp(FiniteOr(investigationSpeed, fallback.investigationSpeed),
                value.patrolSpeed, value.chaseSpeed);
            value.acceleration = AtLeast(acceleration, .1f, fallback.acceleration);
            value.angularSpeed = AtLeast(angularSpeed, 1f, fallback.angularSpeed);
            value.hearingSensitivity = Mathf.Clamp(FiniteOr(hearingSensitivity, fallback.hearingSensitivity), 0f, 4f);
            value.visionRange = AtLeast(visionRange, .1f, fallback.visionRange);
            value.suspiciousThreshold = Mathf.Clamp(FiniteOr(suspiciousThreshold, fallback.suspiciousThreshold), .01f, .98f);
            value.investigateThreshold = Mathf.Clamp(FiniteOr(investigateThreshold, fallback.investigateThreshold),
                value.suspiciousThreshold + .01f, .99f);
            value.combatThreshold = Mathf.Clamp(FiniteOr(combatThreshold, fallback.combatThreshold),
                value.investigateThreshold + .01f, 1f);
            value.searchDuration = AtLeast(searchDuration, .1f, fallback.searchDuration);
            return value;
        }

        private static float FiniteOr(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        private static float AtLeast(float value, float minimum, float fallback)
            => Mathf.Max(minimum, FiniteOr(value, fallback));
    }
}
