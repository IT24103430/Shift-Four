using System;
using UnityEngine;

namespace ShiftFour
{
    [Serializable]
    public sealed class AwarenessSettings
    {
        [Min(0f)] public float soundSuspicionGain = 1.25f;
        [Min(0f)] public float suspicionDecayPerSecond = .15f;
        [Range(0f, 1f)] public float minimumNoiseStrength = .1f;
        [Range(0f, .2f)] public float exitHysteresis = .1f;
        [Min(0f)] public float minimumDwellSeconds = .4f;
        [Min(.1f)] public float investigationSeconds = 2.5f;
        [Min(.1f)] public float visualMemorySeconds = 6f;
        [Min(0f)] public float visualLossGraceSeconds = .5f;
        [Min(.05f)] public float maximumNewSoundAge = .5f;
        [Min(.1f)] public float returnToPostSeconds = 4f;
        [Min(.1f)] public float postArrivalRadius = 1.5f;

        public AwarenessSettings Validated()
        {
            var copy = (AwarenessSettings)MemberwiseClone();
            copy.soundSuspicionGain = Bound(soundSuspicionGain, 1.25f, 0f, 4f);
            copy.suspicionDecayPerSecond = Bound(suspicionDecayPerSecond, .15f, 0f, 4f);
            copy.minimumNoiseStrength = Bound(minimumNoiseStrength, .1f, .01f, 1f);
            copy.exitHysteresis = Bound(exitHysteresis, .1f, 0f, .2f);
            copy.minimumDwellSeconds = Bound(minimumDwellSeconds, .4f, 0f, 3f);
            copy.investigationSeconds = Bound(investigationSeconds, 2.5f, .1f, 15f);
            copy.visualMemorySeconds = Bound(visualMemorySeconds, 6f, .1f, 30f);
            copy.visualLossGraceSeconds = Bound(visualLossGraceSeconds, .5f, 0f, copy.visualMemorySeconds);
            copy.maximumNewSoundAge = Bound(maximumNewSoundAge, .5f, .05f, 2f);
            copy.returnToPostSeconds = Bound(returnToPostSeconds, 4f, .1f, 30f);
            copy.postArrivalRadius = Bound(postArrivalRadius, 1.5f, .1f, 10f);
            return copy;
        }
        private static float Bound(float value, float fallback, float min, float max)
            => Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
