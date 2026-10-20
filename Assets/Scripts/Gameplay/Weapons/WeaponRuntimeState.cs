using System;

namespace ShiftFour
{
    public enum WeaponReloadState { Idle, Reloading }

    // A controller owns these values; the shared definition never stores live player state.
    public sealed class WeaponRuntimeState
    {
        public WeaponDefinition Definition { get; }
        public float NextShotTime { get; private set; }
        public WeaponReloadState ReloadState { get; private set; }

        public WeaponRuntimeState(WeaponDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
        }

        internal bool TryAcceptShot(float now, float interval)
        {
            if (ReloadState != WeaponReloadState.Idle || now < NextShotTime) return false;
            NextShotTime = now + interval;
            return true;
        }

        internal bool BeginReload()
        {
            if (ReloadState != WeaponReloadState.Idle) return false;
            ReloadState = WeaponReloadState.Reloading;
            return true;
        }

        internal bool EndReload()
        {
            if (ReloadState == WeaponReloadState.Idle) return false;
            ReloadState = WeaponReloadState.Idle;
            return true;
        }
    }
}
