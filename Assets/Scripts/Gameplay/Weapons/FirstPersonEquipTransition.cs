using System;

namespace ShiftFour
{
    public enum WeaponPresentationState { Hidden, Equipping, Ready, Unequipping }

    // Scaled-time presentation only. Quintic segments retain pose, velocity and acceleration
    // when reversed; every completed segment has zero velocity/acceleration at its endpoint.
    public sealed class FirstPersonEquipTransition
    {
        private double startedAt, sampledAt, duration, position, velocity, acceleration;
        private double start, v, a, c3, c4, c5, target;
        public WeaponPresentationState State { get; private set; } = WeaponPresentationState.Hidden;
        public float Visibility => (float)position;
        public bool RequestEquip(double now, float seconds) => Request(true, now, seconds);
        public bool RequestUnequip(double now, float seconds) => Request(false, now, seconds);

        private bool Request(bool equip, double now, float seconds)
        {
            Sample(now);
            if (equip ? State == WeaponPresentationState.Equipping || State == WeaponPresentationState.Ready
                      : State == WeaponPresentationState.Unequipping || State == WeaponPresentationState.Hidden) return false;
            startedAt = sampledAt;
            duration = float.IsFinite(seconds) ? Math.Max(.01, seconds) : .3;
            start = position; target = equip ? 1 : 0;
            v = velocity * duration; a = acceleration * duration * duration;
            double delta = target - start;
            c3 = 10 * delta - 6 * v - 1.5 * a;
            c4 = -15 * delta + 8 * v + 1.5 * a;
            c5 = 6 * delta - 3 * v - .5 * a;
            State = equip ? WeaponPresentationState.Equipping : WeaponPresentationState.Unequipping;
            return true;
        }

        public void Sample(double now)
        {
            if (double.IsFinite(now)) sampledAt = Math.Max(sampledAt, now);
            if (State != WeaponPresentationState.Equipping && State != WeaponPresentationState.Unequipping) return;
            double u = Math.Clamp((sampledAt - startedAt) / duration, 0, 1);
            if (u >= 1 - 1e-9)
            {
                position = target; velocity = acceleration = 0;
                State = target > .5 ? WeaponPresentationState.Ready : WeaponPresentationState.Hidden;
                return;
            }
            double u2 = u * u, u3 = u2 * u, u4 = u3 * u, u5 = u4 * u;
            double p = start + v * u + .5 * a * u2 + c3 * u3 + c4 * u4 + c5 * u5;
            position = Math.Clamp(p, 0, 1);
            velocity = (v + a * u + 3 * c3 * u2 + 4 * c4 * u3 + 5 * c5 * u4) / duration;
            acceleration = (a + 6 * c3 * u + 12 * c4 * u2 + 20 * c5 * u3) / (duration * duration);
            if (p < 0 || p > 1) velocity = acceleration = 0;
        }

        public void ResetHidden(double now)
        {
            if (double.IsFinite(now)) sampledAt = Math.Max(sampledAt, now);
            position = velocity = acceleration = 0; State = WeaponPresentationState.Hidden;
        }

        public void ResetReady(double now)
        {
            ResetHidden(now); position = 1; State = WeaponPresentationState.Ready;
        }
    }
}
