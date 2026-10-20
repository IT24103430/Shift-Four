using System;
using UnityEngine;

namespace ShiftFour
{
    public enum WeaponReloadState { Idle, Reloading }

    // A controller owns these values; the shared definition never stores live player state.
    public sealed class WeaponRuntimeState
    {
        public WeaponDefinition Definition { get; }
        public float NextShotTime { get; private set; }
        public WeaponReloadState ReloadState { get; private set; }
        public int MagazineCapacity { get; }
        public int RoundsInMagazine { get; private set; }
        public int ReserveRounds { get; private set; }
        private float reloadStartedAt, reloadEndsAt;
        public float ReloadProgress => ReloadState == WeaponReloadState.Reloading
            ? Mathf.Clamp01((Time.time - reloadStartedAt) / (reloadEndsAt - reloadStartedAt)) : 0;
        public bool CanReload => ReloadState == WeaponReloadState.Idle && RoundsInMagazine < MagazineCapacity && ReserveRounds > 0;

        public WeaponRuntimeState(WeaponDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            MagazineCapacity = definition.MagazineCapacity;
            RoundsInMagazine = MagazineCapacity;
            ReserveRounds = definition.StartingReserve;
        }

        internal bool TryAcceptShot(float now, float interval)
        {
            if (ReloadState != WeaponReloadState.Idle || now < NextShotTime || RoundsInMagazine <= 0) return false;
            NextShotTime = now + interval;
            RoundsInMagazine--;
            return true;
        }

        internal bool BeginReload(float now)
        {
            if (!CanReload) return false;
            reloadStartedAt = now;
            reloadEndsAt = now + Definition.ReloadDuration;
            ReloadState = WeaponReloadState.Reloading;
            return true;
        }

        internal bool EndReload(float now)
        {
            if (ReloadState != WeaponReloadState.Reloading || now < reloadEndsAt) return false;
            int transferred = Math.Min(MagazineCapacity - RoundsInMagazine, ReserveRounds);
            RoundsInMagazine += transferred;
            ReserveRounds -= transferred;
            return CancelReload();
        }

        internal bool CancelReload()
        {
            if (ReloadState == WeaponReloadState.Idle) return false;
            ReloadState = WeaponReloadState.Idle;
            return true;
        }
    }
}
