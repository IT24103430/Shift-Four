using UnityEngine;

namespace ShiftFour
{
    public enum DroneMovementMode { Patrol, Investigate, Chase }

    /// <summary>Speed limits only: no navigation requests, positions or tactical decisions.</summary>
    public sealed class DroneMovementSpeeds
    {
        private readonly DroneDifficultySettings settings;
        public DroneMovementMode Mode { get; private set; }
        public float Current { get; private set; }
        public float Target => Mode == DroneMovementMode.Chase ? settings.chaseSpeed :
            Mode == DroneMovementMode.Investigate ? settings.investigationSpeed : settings.patrolSpeed;
        public float AnimationReference => settings.chaseSpeed;

        public DroneMovementSpeeds(DroneDifficultySettings profile)
        {
            settings = profile.Validated();
            Current = settings.patrolSpeed;
        }

        public void SetMode(DroneMovementMode mode)
        {
            Mode = mode == DroneMovementMode.Chase || mode == DroneMovementMode.Investigate
                ? mode : DroneMovementMode.Patrol;
        }

        public static DroneMovementMode FromAwareness(DroneAwarenessState state)
        {
            switch (state)
            {
                case DroneAwarenessState.Combat: return DroneMovementMode.Chase;
                case DroneAwarenessState.Investigate:
                case DroneAwarenessState.Search: return DroneMovementMode.Investigate;
                default: return DroneMovementMode.Patrol;
            }
        }

        public float Step(float deltaTime, float acceleration)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f ||
                float.IsNaN(acceleration) || float.IsInfinity(acceleration) || acceleration <= 0f)
                return Current;
            Current = Mathf.MoveTowards(Current, Target, acceleration * deltaTime);
            return Current;
        }
    }
}
