using System;
using UnityEngine;

namespace ShiftFour
{
    [Serializable]
    public sealed class HearingSettings
    {
        [Min(1f)] public float referenceRange = 26f;
        [Range(.001f, .5f)] public float detectionThreshold = .01f;
        [Range(0f, 1f)] public float wallTransmission = .25f;
        [Range(.1f, 10f)] public float memorySeconds = 5f;
        [Range(.01f, 1f)] public float maximumEventAge = .25f;
        [Range(0f, 4f)] public float minimumUncertainty = .6f;
        [Range(0f, 4f)] public float additionalUncertainty = 1.4f;
        [Range(1, 8)] public int queriesPerFrame = 2;
        [Range(1, 16)] public int queriesPerSecond = 8;
        public LayerMask obstacleMask = ~0;

        public void Validate()
        {
            referenceRange = Bound(referenceRange, 26f, 1f, 100f);
            detectionThreshold = Bound(detectionThreshold, .01f, .001f, .5f);
            wallTransmission = Bound(wallTransmission, .25f, 0f, 1f);
            memorySeconds = Bound(memorySeconds, 5f, .1f, 10f);
            maximumEventAge = Bound(maximumEventAge, .25f, .01f, 1f);
            minimumUncertainty = Bound(minimumUncertainty, .6f, 0f, 4f);
            additionalUncertainty = Bound(additionalUncertainty, 1.4f, 0f, 4f);
            queriesPerFrame = Mathf.Clamp(queriesPerFrame, 1, 8);
            queriesPerSecond = Mathf.Clamp(queriesPerSecond, 1, 16);
        }
        private static float Bound(float value, float fallback, float min, float max)
            => Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
    }
}
