using System;

namespace ShiftFour
{
    // Presentation-only, analytically damped impulse. No transforms, weapon state or gameplay events.
    public sealed class FirstPersonRecoilMotion
    {
        public const double MaximumWeight = 1.5;
        private double backward, backwardVelocity, muzzle, muzzleVelocity, lastTime;
        private bool hasTime;

        public float BackwardWeight => (float)backward;
        public float MuzzleWeight => (float)muzzle;
        public float Strength => (float)Math.Max(backward, muzzle);
        public double SampleTime => hasTime ? lastTime : 0;

        public void Reset()
        {
            backward = backwardVelocity = muzzle = muzzleVelocity = lastTime = 0;
            hasTime = false;
        }

        public void AcceptShot(double time, float responseRate)
        {
            if (!ValidTime(time) || (hasTime && time < lastTime)) return;
            Sample(time, responseRate);
            double rate = Rate(responseRate);
            backward = Math.Min(MaximumWeight, backward + 1);
            muzzle = Math.Min(MaximumWeight, muzzle + .65);
            // Immediate shoulder/hand impulse, followed by a small, delayed muzzle peak.
            muzzleVelocity = Math.Min(MaximumWeight * rate, muzzleVelocity + 1.25 * rate);
        }

        public void Sample(double time, float responseRate)
        {
            if (!ValidTime(time) || (hasTime && time < lastTime)) return;
            if (!hasTime) { lastTime = time; hasTime = true; return; }
            double dt = time - lastTime; lastTime = time;
            if (dt <= 0) return;
            double rate = Rate(responseRate);
            if (rate * dt >= 60)
            {
                backward = backwardVelocity = muzzle = muzzleVelocity = 0; return;
            }
            Integrate(ref backward, ref backwardVelocity, dt, rate);
            Integrate(ref muzzle, ref muzzleVelocity, dt, rate);
            if (Math.Max(backward, muzzle) < .00001 && Math.Max(Math.Abs(backwardVelocity), Math.Abs(muzzleVelocity)) / rate < .00001)
                backward = backwardVelocity = muzzle = muzzleVelocity = 0;
        }

        private static void Integrate(ref double position, ref double velocity, double dt, double rate)
        {
            double j = velocity + rate * position, decay = Math.Exp(-rate * dt);
            position = (position + j * dt) * decay;
            velocity = (velocity - rate * j * dt) * decay;
            if (position > MaximumWeight)
            {
                position = MaximumWeight;
                if (velocity > 0) velocity = 0;
            }
            if (position < 0) { position = 0; velocity = 0; }
        }

        private static double Rate(float value) => float.IsFinite(value) ? Math.Max(.1, value) : 22;
        private static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
