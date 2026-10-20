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
        private float clock, fireTime, hitTime, previousSpeed, deceleration;
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
            State = DroneVisualState.Shutdown;
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
            State = dead ? DroneVisualState.Shutdown : hitTime > 0f ? DroneVisualState.Damaged :
                fireTime > 0f ? DroneVisualState.Firing : braking ? DroneVisualState.Braking :
                turning ? DroneVisualState.Turning : moving ? DroneVisualState.Moving : DroneVisualState.Idle;

            float pitchTarget = dead ? 0f : -Mathf.Clamp(localVelocity.z, -1f, 1f) +
                Mathf.Clamp01(deceleration) + FireFlash * .65f;
            float bankTarget = dead ? 0f : -Mathf.Clamp(yawDegreesPerSecond / 90f, -1f, 1f) * BankDegrees -
                Mathf.Clamp(localVelocity.x, -1f, 1f) * .4f + HitFlash * .8f;
            bankTarget += dead ? 0f : Mathf.Sin(clock * 1.5f) * .12f * (1f - Mathf.Clamp01(speed));
            Pitch = Damp(Pitch, pitchTarget, 9f, dt);
            Bank = Damp(Bank, bankTarget, 9f, dt);
            ShutdownBlend = Damp(ShutdownBlend, dead ? 1f : 0f, 5f, dt);
            Hover = Mathf.Sin(clock * 3f) * HoverAmplitude * (1f - ShutdownBlend);
            float target = dead ? 0f : .8f + Mathf.Clamp01(speed) * .2f + FireFlash * .12f;
            float rate = dead ? 4f : 8f;
            float decay = Mathf.Exp(-rate * dt);
            // Integrate the exponential exactly: spin-down angle is independent of frame rate.
            RotorTravel = target * dt + (RotorSpeed - target) * (1f - decay) / rate;
            RotorSpeed = target + (RotorSpeed - target) * decay;
            if (dead && RotorSpeed < .001f) RotorSpeed = 0f;
        }

        private static float Damp(float from, float to, float rate, float dt)
            => to + (from - to) * Mathf.Exp(-rate * dt);
    }
}
