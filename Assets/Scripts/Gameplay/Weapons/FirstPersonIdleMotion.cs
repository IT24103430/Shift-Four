using System;
using UnityEngine;

namespace ShiftFour
{
    // Samples offsets from a phase clock; never accumulates transform deltas or touches the camera.
    [DisallowMultipleComponent]
    public sealed class FirstPersonIdleMotion : MonoBehaviour
    {
        [Tooltip("12 breaths per minute gives a five-second loop.")]
        [SerializeField, Range(6, 24)] private float breathsPerMinute = 12;
        [Tooltip("Peak travel in meters. 0.0008 m = 0.8 mm.")]
        [SerializeField, Range(0, .003f)] private float verticalAmplitude = .0008f;
        [Tooltip("Peak travel in meters. 0.0003 m = 0.3 mm.")]
        [SerializeField, Range(0, .002f)] private float lateralAmplitude = .0003f;
        [Tooltip("Peak travel in meters. 0.00025 m = 0.25 mm.")]
        [SerializeField, Range(0, .002f)] private float depthAmplitude = .00025f;
        [Tooltip("Peak breathing pitch in degrees.")]
        [SerializeField, Range(0, .25f)] private float pitchAmplitude = .06f;
        [SerializeField, Range(0, .15f)] private float yawAmplitude = .025f;
        [SerializeField, Range(0, .15f)] private float rollAmplitude = .04f;
        [Tooltip("Exponential blend time constant; roughly three times this value reaches 95%.")]
        [SerializeField, Range(.05f, .5f)] private float blendSeconds = .18f;
        private double phase, elapsed, weight;

        public float BreathsPerMinute => breathsPerMinute;
        public float VerticalAmplitude => verticalAmplitude;
        public float LateralAmplitude => lateralAmplitude;
        public float DepthAmplitude => depthAmplitude;
        public float PitchAmplitude => pitchAmplitude;
        public float YawAmplitude => yawAmplitude;
        public float RollAmplitude => rollAmplitude;
        public float BlendSeconds => blendSeconds;
        public double ElapsedSeconds => elapsed;
        public double Phase => phase;
        public float Weight => (float)weight;
        public Vector3 PositionOffset { get; private set; }
        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;

        private void OnEnable() => ResetMotion();
        private void OnDisable() => ResetMotion();

        public void Configure(FirstPersonWeaponGripConfiguration configuration)
        {
            if (configuration == null) return;
            breathsPerMinute = configuration.BreathsPerMinute; verticalAmplitude = configuration.IdleAmplitude;
        }

        public void ResetMotion()
        {
            phase = elapsed = weight = 0;
            PositionOffset = Vector3.zero; RotationOffset = Quaternion.identity;
        }

        public void Advance(double deltaSeconds, bool suppressed)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0) return;
            elapsed += deltaSeconds;
            phase = (phase + deltaSeconds * Mathf.Clamp(breathsPerMinute, 6, 24) / 60) % 1;
            double target = suppressed ? 0 : 1;
            weight = target + (weight - target) * Math.Exp(-deltaSeconds / Mathf.Max(.05f, blendSeconds));
            double angle = phase * Math.PI * 2;
            float breath = (float)(Math.Sin(angle) * weight);
            float secondary = (float)(Math.Sin(angle * 2) * weight);
            PositionOffset = new Vector3(lateralAmplitude * secondary, verticalAmplitude * breath, depthAmplitude * breath);
            RotationOffset = Quaternion.Euler(pitchAmplitude * breath, yawAmplitude * secondary, rollAmplitude * secondary);
        }
    }
}
