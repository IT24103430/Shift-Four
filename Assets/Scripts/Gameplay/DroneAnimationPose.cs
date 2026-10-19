using UnityEngine;

namespace ShiftFour
{
    public enum DroneVisualState { Idle, Moving, Turning, Braking, Firing, Damaged, Shutdown,
        CriticalFailure, Falling, GroundImpact, Disabled }

    /// <summary>Presentation state only. Explicit time steps make damping and rotor travel testable.</summary>
    public sealed class DroneAnimationPose
    {
        public DroneVisualState State { get; private set; }
        public float Pitch { get; private set; }
        public float Bank { get; private set; }
        public float Hover { get; private set; }
        public float RotorSpeed { get; private set; } = .8f;
        public float RotorTravel { get; private set; }
        public float FireFlash => fireTime / .16f;
        public float HitFlash => hitTime / .24f;
        public float ShutdownBlend { get; private set; }
        public float HoverAmplitude { get; set; } = .02f;
        public float BankDegrees { get; set; } = 4f;
        public DroneAwarenessState AwarenessState { get; private set; }
        public Color AlertColor { get; private set; }
        public float AlertBlend { get; private set; }
        public float ScanYaw { get; private set; }
        public float ForwardAcceleration { get; private set; }
        private float clock, fireTime, hitTime, previousSpeed, deceleration;
        private float accelerationTarget, cuePhase, scanPhase, scanBlend;
        private bool dead;

        public DroneAnimationPose() { }

        public DroneAnimationPose(bool alive)
        {
            if (alive) return;
            Shutdown();
            RotorSpeed = 0f;
            ShutdownBlend = 1f;
        }

        public void Fire()
        {
            if (!dead) fireTime = .16f;
        }

        public void Hit(bool fatal)
        {
            if (fatal) Shutdown();
            else if (!dead) hitTime = .24f;
        }

        public void Shutdown()
        {
            dead = true;
            fireTime = hitTime = 0f;
            AlertBlend = ScanYaw = scanBlend = ForwardAcceleration = accelerationTarget = 0f;
            AlertColor = Color.black;
            AwarenessState = DroneAwarenessState.Patrol;
            State = DroneVisualState.Shutdown;
        }

        // Presentation inputs only. They cannot change sensing, movement modes or shot permission.
        public void SetAwareness(DroneAwarenessState state)
        {
            if (!dead) AwarenessState = (uint)state <= (uint)DroneAwarenessState.ReturnToPost ? state : DroneAwarenessState.Patrol;
        }

        public void SetAcceleration(float normalizedForwardAcceleration)
        {
            accelerationTarget = dead || float.IsNaN(normalizedForwardAcceleration) || float.IsInfinity(normalizedForwardAcceleration)
                ? 0f : Mathf.Clamp01(normalizedForwardAcceleration);
        }

        public void StopRotors() { RotorSpeed = 0f; RotorTravel = 0f; }

        public void Step(float dt, Vector3 localVelocity, float yawDegreesPerSecond, bool alive)
        {
            if (!alive) Shutdown();
            if (dt <= 0f) return;
            clock = Mathf.Repeat(clock + dt, Mathf.PI * 2f);
            fireTime = Mathf.Max(0f, fireTime - dt);
            hitTime = Mathf.Max(0f, hitTime - dt);
            float speed = localVelocity.magnitude; // Velocity is normalized by configured cruise speed.
            // Filter the signed change before classifying braking. Clipping each noisy
            // negative/positive sample first biases steady flight toward perpetual braking.
            float slowing = (previousSpeed - speed) / dt;
            previousSpeed = speed;
            deceleration = Damp(deceleration, slowing, 8f, dt);
            bool braking = deceleration > .18f;
            bool moving = speed > .04f;
            bool turning = moving && Mathf.Abs(yawDegreesPerSecond) > 8f;
            ForwardAcceleration = Damp(ForwardAcceleration, accelerationTarget, 10f, dt);
            State = dead ? DroneVisualState.Shutdown : hitTime > 0f ? DroneVisualState.Damaged :
                fireTime > 0f ? DroneVisualState.Firing : braking ? DroneVisualState.Braking :
                turning ? DroneVisualState.Turning : moving ? DroneVisualState.Moving : DroneVisualState.Idle;

            float pitchTarget = dead ? 0f : -Mathf.Clamp(localVelocity.z, -1f, 1f) +
                Mathf.Clamp01(deceleration) - ForwardAcceleration * .55f + FireFlash * .65f;
            float bankTarget = dead ? 0f : -Mathf.Clamp(yawDegreesPerSecond / 90f, -1f, 1f) * BankDegrees -
                Mathf.Clamp(localVelocity.x, -1f, 1f) * .4f + HitFlash * .8f;
            bankTarget += dead ? 0f : Mathf.Sin(clock * 1.5f) * .12f * (1f - Mathf.Clamp01(speed));
            Pitch = Damp(Pitch, pitchTarget, 9f, dt);
            Bank = Damp(Bank, bankTarget, 9f, dt);
            ShutdownBlend = Damp(ShutdownBlend, dead ? 1f : 0f, 5f, dt);
            Hover = Mathf.Sin(clock * 3f) * HoverAmplitude * (1f - ShutdownBlend);
            float target = dead ? 0f : .8f + Mathf.Clamp01(speed) * .2f + ForwardAcceleration * .06f + FireFlash * .12f;
            float rate = dead ? 4f : 8f;
            float decay = Mathf.Exp(-rate * dt);
            // Integrate the exponential exactly: spin-down angle is independent of frame rate.
            RotorTravel = target * dt + (RotorSpeed - target) * (1f - decay) / rate;
            RotorSpeed = target + (RotorSpeed - target) * decay;
            if (dead && RotorSpeed < .001f) RotorSpeed = 0f;
            StepAwareness(dt, speed);
        }

        private void StepAwareness(float dt, float speed)
        {
            if (dead) return;
            bool combat = AwarenessState == DroneAwarenessState.Combat;
            bool scanning = AwarenessState == DroneAwarenessState.Investigate || AwarenessState == DroneAwarenessState.Search;
            bool suspicious = AwarenessState == DroneAwarenessState.Suspicious;
            cuePhase = Mathf.Repeat(cuePhase + dt * Mathf.PI * 2f * (combat ? 2f : .35f), Mathf.PI * 2f);
            scanPhase = Mathf.Repeat(scanPhase + dt * Mathf.PI * 2f * .3f, Mathf.PI * 2f);
            float pulse = .5f + .5f * Mathf.Sin(cuePhase);
            float target = combat ? .18f + .1f * pulse : scanning ? .09f + .05f * pulse : suspicious ? .06f + .04f * pulse : 0f;
            AlertBlend = Damp(AlertBlend, target, 8f, dt);
            if (target == 0f && AlertBlend < .0001f) AlertBlend = 0f;
            Color color = combat ? new Color(1f,.06f,.12f) : scanning ? new Color(1f,.55f,.08f) : new Color(1f,.72f,.12f);
            AlertColor = Color.Lerp(AlertColor, color, 1f - Mathf.Exp(-8f * dt));
            scanBlend = Damp(scanBlend, scanning ? 1f : 0f, 4f, dt);
            // Scanning never turns the navigation root or competes with a moving bank.
            ScanYaw = Mathf.Sin(scanPhase) * 1.2f * scanBlend * (1f - Mathf.Clamp01(speed / .12f)) *
                (1f - Mathf.Max(FireFlash, HitFlash));
        }

        private static float Damp(float from, float to, float rate, float dt)
            => to + (from - to) * Mathf.Exp(-rate * dt);
    }
}
