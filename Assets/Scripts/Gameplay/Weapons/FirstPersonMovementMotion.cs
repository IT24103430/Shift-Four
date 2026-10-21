using System;
using UnityEngine;

namespace ShiftFour
{
    // Read-only movement observer. PistolPresentation is the only writer of the shared pose pivot.
    [DisallowMultipleComponent]
    public sealed class FirstPersonMovementMotion : MonoBehaviour
    {
        [Header("Walking (presentation only)")]
        [SerializeField, Min(.1f), Tooltip("Speed used to normalize pose strength; does not change player speed.")]
        private float referenceSpeed = 5f;
        [SerializeField, Min(.01f)] private float stationarySpeed = .08f;
        [SerializeField, Min(.2f), Tooltip("Metres travelled per complete left/right sway cycle.")]
        private float strideLength = 4f;
        [SerializeField, Tooltip("Peak XYZ walking offsets in metres.")] private Vector3 walkPositionAmplitude = new Vector3(.0011f, .0015f, .0006f);
        [SerializeField, Tooltip("Peak pitch/yaw/roll walking angles in degrees.")] private Vector3 walkRotationAmplitude = new Vector3(.14f, .06f, .18f);
        [SerializeField, Min(.02f)] private float blendSeconds = .12f;
        [SerializeField, Tooltip("Maximum lateral lag at the reference strafe speed, in metres.")]
        private float strafeLag = .002f;
        [SerializeField, Tooltip("Maximum forward/back lean at the reference speed, in degrees.")]
        private float forwardLean = .18f;
        [SerializeField, Tooltip("Maximum strafe roll at the reference speed, in degrees.")]
        private float strafeRoll = .24f;

        [Header("Jump and landing")]
        [SerializeField, Tooltip("Steady airborne hold offset in metres.")] private Vector3 airbornePosition = new Vector3(0, -.003f, -.002f);
        [SerializeField, Tooltip("Steady airborne pitch/yaw/roll in degrees.")] private Vector3 airborneRotation = new Vector3(.5f, 0, -.15f);
        [SerializeField, Min(.02f)] private float airborneBlendSeconds = .1f;
        [SerializeField, Min(0), Tooltip("Additional takeoff dip in metres.")] private float takeoffDip = .002f;
        [SerializeField, Min(0)] private float takeoffPitch = .22f;
        [SerializeField, Min(.02f)] private float takeoffPeakSeconds = .06f;
        [SerializeField, Min(0), Tooltip("Maximum additional landing dip in metres, scaled by observed fall speed.")] private float landingDip = .004f;
        [SerializeField, Min(0)] private float landingPitch = .4f;
        [SerializeField, Min(.02f)] private float landingPeakSeconds = .045f;
        [SerializeField, Min(.1f)] private float landingReferenceSpeed = 7f;
        [SerializeField, Range(0, 1), Tooltip("Movement layer strength during recoil/reload.")]
        private float activityStrength = .35f;

        private PlayerController player;
        private CharacterController controller;
        private Vector3 previousPosition;
        private Vector3 direction;
        private double phase, walking, airborne, activity = 1, airTime, takeoffAge = 10, landingAge = 10;
        private float fallSpeed, landingStrength;
        private bool sampled, wasGrounded, observedControllerState;

        public Vector3 PositionOffset { get; private set; }
        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
        public Vector3 LocalVelocity { get; private set; }
        public bool Grounded { get; private set; }
        public float WalkingWeight => (float)walking;
        public float AirborneWeight => (float)airborne;
        public double Phase => phase;
        public int TakeoffCount { get; private set; }
        public int LandingCount { get; private set; }
        public float IdleWeight => (1 - .75f * WalkingWeight) * (1 - .85f * AirborneWeight);

        private void OnEnable() { Initialize(); ResetMotion(); }
        private void OnDisable() => ResetMotion();

        public bool Initialize()
        {
            if (player != null && controller != null) return true;
            player = GetComponentInParent<PlayerController>();
            controller = player != null ? player.GetComponent<CharacterController>() : null;
            if (player != null) previousPosition = player.transform.position;
            return controller != null;
        }

        public void ResetMotion()
        {
            phase = walking = airborne = airTime = 0; activity = 1;
            takeoffAge = landingAge = 10; direction = Vector3.zero;
            fallSpeed = landingStrength = 0; sampled = wasGrounded = Grounded = observedControllerState = false;
            TakeoffCount = LandingCount = 0; LocalVelocity = PositionOffset = Vector3.zero;
            RotationOffset = Quaternion.identity;
            if (player != null) previousPosition = player.transform.position;
        }

        public void SamplePlayer(double deltaSeconds, bool suppressed)
        {
            if (!ValidTime(deltaSeconds) || !Initialize()) return;
            if (!controller.enabled) { ResetMotion(); return; }
            Vector3 position = player.transform.position;
            Vector3 velocity = (position - previousPosition) / (float)deltaSeconds;
            previousPosition = position;
            // Respawns/teleports are not footsteps or landing impacts. Normal jump speeds are well below this.
            if (velocity.sqrMagnitude > 25 * 25) { ResetMotion(); return; }
            Grounded = controller.isGrounded;
            // Before the first Move (for example with an unlocked cursor), isGrounded is unknown/false.
            // Do not invent flight for a stationary player. Contact or actual vertical travel establishes it.
            if (!observedControllerState)
            {
                observedControllerState = Grounded || Mathf.Abs(velocity.y) > .05f;
                if (!observedControllerState) return;
            }
            Advance(deltaSeconds, player.transform.InverseTransformDirection(velocity), controller.isGrounded, suppressed);
        }

        // Deterministic pose sampling also permits tests without changing player physics or synthesizing input.
        public void Advance(double deltaSeconds, Vector3 localVelocity, bool grounded, bool suppressed)
        {
            if (!ValidTime(deltaSeconds) || !Finite(localVelocity)) return;
            LocalVelocity = localVelocity; Grounded = grounded;
            takeoffAge += deltaSeconds; landingAge += deltaSeconds;
            if (sampled && wasGrounded && !grounded)
            {
                airTime = 0; fallSpeed = 0;
                if (localVelocity.y > .15f) { takeoffAge = deltaSeconds; TakeoffCount++; }
            }
            if (!grounded) { airTime += deltaSeconds; fallSpeed = Mathf.Max(fallSpeed, -localVelocity.y); }
            if (sampled && !wasGrounded && grounded)
            {
                if (airTime >= .08 && fallSpeed > .2f)
                {
                    landingAge = deltaSeconds; landingStrength = Mathf.Clamp01(fallSpeed / Mathf.Max(.1f, landingReferenceSpeed));
                    LandingCount++;
                }
                airTime = 0; fallSpeed = 0;
            }
            sampled = true; wasGrounded = grounded;
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            bool walkingNow = grounded && speed > stationarySpeed;
            float strength = walkingNow ? Mathf.Clamp01(speed / Mathf.Max(.1f, referenceSpeed)) : 0;
            double blend = 1 - Math.Exp(-deltaSeconds / Mathf.Max(.02f, blendSeconds));
            walking += (strength - walking) * blend;
            airborne += ((grounded ? 0 : 1) - airborne) * (1 - Math.Exp(-deltaSeconds / Mathf.Max(.02f, airborneBlendSeconds)));
            activity += ((suppressed ? activityStrength : 1) - activity) * blend;
            Vector3 targetDirection = walkingNow ? Vector3.ClampMagnitude(new Vector3(localVelocity.x, 0, localVelocity.z) / Mathf.Max(.1f, referenceSpeed), 1) : Vector3.zero;
            direction = Vector3.LerpUnclamped(direction, targetDirection, (float)blend);
            // Freeze phase as soon as actual travel stops; the held offset eases to neutral without stationary oscillation.
            if (walkingNow) phase = (phase + speed * deltaSeconds / Mathf.Max(.2f, strideLength)) % 1;
            if (walking < .00001) walking = 0;
            if (airborne < .00001) airborne = 0;
            if (direction.sqrMagnitude < 1e-10f) direction = Vector3.zero;
            double angle = phase * Math.PI * 2;
            float side = (float)Math.Sin(angle), step = (float)Math.Sin(2 * angle);
            float walk = WalkingWeight * (1 - AirborneWeight);
            Vector3 positionOffset = Vector3.Scale(walkPositionAmplitude, new Vector3(side, step, -step)) * walk;
            Vector3 angles = Vector3.Scale(walkRotationAmplitude, new Vector3(step, side, -side)) * walk;
            positionOffset.x -= direction.x * strafeLag;
            angles += new Vector3(direction.z * forwardLean, 0, -direction.x * strafeRoll);
            positionOffset += airbornePosition * AirborneWeight;
            angles += airborneRotation * AirborneWeight;
            float launch = Pulse(takeoffAge, takeoffPeakSeconds);
            float impact = Pulse(landingAge, landingPeakSeconds) * landingStrength;
            positionOffset.y -= takeoffDip * launch + landingDip * impact;
            angles.x += takeoffPitch * launch + landingPitch * impact;
            PositionOffset = positionOffset * (float)activity;
            RotationOffset = Quaternion.Euler(angles * (float)activity);
        }

        private static float Pulse(double age, float peakSeconds)
        {
            double t = age / Mathf.Max(.02f, peakSeconds);
            return t > 30 ? 0 : (float)(t * Math.Exp(1 - t));
        }
        private static bool ValidTime(double dt) => !double.IsNaN(dt) && !double.IsInfinity(dt) && dt > 0;
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
