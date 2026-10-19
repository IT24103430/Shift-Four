using UnityEngine;

namespace ShiftFour
{
    public enum DroneAwarenessState { Patrol, Suspicious, Investigate, Search, Combat, ReturnToPost }

    /// <summary>Alertness only. No navigation, target Transform, physics or attack authorization.</summary>
    public sealed class DroneAwareness
    {
        private readonly AwarenessSettings settings;
        private readonly float suspiciousThreshold, investigateThreshold, searchSeconds;
        private float lastStepAt = float.NaN, stateSince = float.NegativeInfinity;
        private float lastSeenAt = float.NegativeInfinity, lastHeardAt = float.NegativeInfinity;
        private float retiredSoundAt = float.NegativeInfinity;
        private float stateSoundAt = float.NegativeInfinity;
        private float searchUntil, returnUntil;
        private Vector3 lastSeen;
        private SoundObservation lastHeard;
        private bool haveSeen, soundAvailable;

        public DroneAwarenessState State { get; private set; } = DroneAwarenessState.Patrol;
        public bool IsEnabled { get; private set; } = true;
        public bool HasVisualConfirmation { get; private set; }
        public float Suspicion { get; private set; }
        public int Transitions { get; private set; }
        public float LastSeenAt => lastSeenAt;
        public float LastHeardAt => lastHeardAt;
        public float StateSince => stateSince;
        public float PostArrivalRadius => settings.postArrivalRadius;
        public float SuspiciousThreshold => suspiciousThreshold;
        public float InvestigateThreshold => investigateThreshold;
        public float SearchDuration => searchSeconds;

        public DroneAwareness(AwarenessSettings config, DroneDifficultySettings difficulty)
        {
            settings = (config ?? new AwarenessSettings()).Validated();
            var validated = difficulty.Validated();
            suspiciousThreshold = validated.suspiciousThreshold;
            investigateThreshold = validated.investigateThreshold;
            searchSeconds = Mathf.Clamp(validated.searchDuration, .1f, 30f);
        }

        // Inputs describe the current perception snapshot, not newly emitted events.
        // Pass visiblePosition only after the caller's real visual confirmation; false ignores it.
        // Pass the hearing sensor's latest still-valid observation on each decision tick.
        public bool Step(float now, bool visuallyConfirmed, Vector3 visiblePosition,
            bool hasSound, SoundObservation sound, bool atPost)
        {
            if (!IsEnabled || !Finite(now) || now < 0f || (!float.IsNaN(lastStepAt) && now < lastStepAt)) return false;
            float elapsed = float.IsNaN(lastStepAt) ? 0f : now - lastStepAt;
            lastStepAt = now;
            Suspicion = Mathf.Max(0f, Suspicion - settings.suspicionDecayPerSecond * elapsed);
            soundAvailable = false;
            if (hasSound && ValidSound(sound, now))
            {
                if (sound.EmittedAt > lastHeardAt && now - sound.EmittedAt <= settings.maximumNewSoundAge)
                {
                    lastHeard = sound; lastHeardAt = sound.EmittedAt;
                    Suspicion = Mathf.Clamp01(Suspicion + sound.Strength * settings.soundSuspicionGain);
                }
                // Older or altered copies cannot replace/revive an accepted observation.
                soundAvailable = sound.EmittedAt == lastHeardAt && SameSound(sound, lastHeard);
            }
            HasVisualConfirmation = visuallyConfirmed && Finite(visiblePosition);
            if (HasVisualConfirmation)
            {
                haveSeen = true; lastSeen = visiblePosition; lastSeenAt = now; Suspicion = 1f;
                Change(DroneAwarenessState.Combat, now, true);
                return true;
            }

            bool activeSound = soundAvailable && lastHeardAt > retiredSoundAt;
            bool reliable = activeSound && lastHeard.Strength >= settings.minimumNoiseStrength;
            float evidenceUntil = Mathf.Max(haveSeen ? lastSeenAt + settings.visualMemorySeconds : float.NegativeInfinity,
                lastHeardAt >= 0f ? lastHeard.ExpiresAt : float.NegativeInfinity);
            switch (State)
            {
                case DroneAwarenessState.Patrol:
                    if (activeSound && Suspicion >= suspiciousThreshold) Change(DroneAwarenessState.Suspicious, now);
                    break;
                case DroneAwarenessState.Suspicious:
                    if (reliable && Suspicion >= investigateThreshold)
                        Change(DroneAwarenessState.Investigate, now);
                    else if (!soundAvailable || now >= evidenceUntil ||
                        Suspicion < Mathf.Max(0f, suspiciousThreshold - settings.exitHysteresis))
                        BeginSearch(now, now, evidenceUntil);
                    break;
                case DroneAwarenessState.Investigate:
                    float investigationUntil = Mathf.Min(lastHeard.ExpiresAt, lastHeardAt + settings.investigationSeconds);
                    if (!soundAvailable || now >= investigationUntil ||
                        Suspicion < Mathf.Max(0f, suspiciousThreshold - settings.exitHysteresis))
                        BeginSearch(now, Mathf.Min(now, investigationUntil), evidenceUntil);
                    break;
                case DroneAwarenessState.Combat:
                    if (now >= lastSeenAt + settings.visualLossGraceSeconds)
                        BeginSearch(now, lastSeenAt + settings.visualLossGraceSeconds, evidenceUntil);
                    break;
                case DroneAwarenessState.Search:
                    if (lastHeardAt > stateSoundAt && reliable && Suspicion >= investigateThreshold)
                        Change(DroneAwarenessState.Investigate, now);
                    else if (lastHeardAt > stateSoundAt && activeSound && Suspicion >= suspiciousThreshold)
                        Change(DroneAwarenessState.Suspicious, now);
                    else if (now >= searchUntil) BeginReturn(now);
                    break;
                case DroneAwarenessState.ReturnToPost:
                    if (lastHeardAt > stateSoundAt && activeSound && Suspicion >= suspiciousThreshold)
                        Change(DroneAwarenessState.Suspicious, now);
                    else if ((atPost || now >= returnUntil) && Change(DroneAwarenessState.Patrol, now))
                    {
                        retiredSoundAt = lastHeardAt; Suspicion = 0f;
                    }
                    break;
            }
            return true;
        }

        public bool TryGetLastSeen(float now, out Vector3 position)
        {
            position = default;
            if (!IsEnabled || !haveSeen || !Finite(now) || now < lastSeenAt || now >= lastSeenAt + settings.visualMemorySeconds)
                return false;
            position = lastSeen; return true;
        }
        public bool TryGetLastHeard(float now, out SoundObservation observation)
        {
            observation = default;
            if (!IsEnabled || !soundAvailable || !lastHeard.IsFresh(now)) return false;
            observation = lastHeard; return true;
        }

        // Death is an immediate, latched override. No extra tactical "Dead" state is imposed.
        public void Disable()
        {
            IsEnabled = false; HasVisualConfirmation = false; Suspicion = 0f;
            haveSeen = soundAvailable = false; lastSeen = default; lastHeard = default;
            lastSeenAt = lastHeardAt = float.NegativeInfinity;
        }
        private void BeginSearch(float now, float uncertaintyAt, float evidenceUntil)
        {
            float deadline = Mathf.Min(evidenceUntil, uncertaintyAt + searchSeconds);
            if (now >= deadline) { BeginReturn(now); return; }
            if (Change(DroneAwarenessState.Search, now)) searchUntil = deadline;
        }
        private void BeginReturn(float now)
        {
            if (Change(DroneAwarenessState.ReturnToPost, now)) returnUntil = now + settings.returnToPostSeconds;
        }
        private bool Change(DroneAwarenessState next, float now, bool immediate = false)
        {
            if (State == next || (!immediate && now - stateSince < settings.minimumDwellSeconds)) return false;
            State = next; stateSince = now; stateSoundAt = lastHeardAt; Transitions++; return true;
        }
        private static bool ValidSound(SoundObservation sound, float now)
            => sound.IsFresh(now) && Finite(sound.Position) && Finite(sound.Strength) && sound.Strength > 0f && sound.Strength <= 1f &&
                Finite(sound.UncertaintyRadius) && sound.UncertaintyRadius >= 0f && Finite(sound.EmittedAt) && sound.EmittedAt >= 0f &&
                Finite(sound.ExpiresAt) && sound.ExpiresAt > sound.EmittedAt && (uint)sound.Category <= (uint)NoiseCategory.Impact;
        private static bool SameSound(SoundObservation a, SoundObservation b)
            => a.EmittedAt == b.EmittedAt && a.ExpiresAt == b.ExpiresAt && a.Position.Equals(b.Position) &&
                a.Strength == b.Strength && a.Category == b.Category && a.UncertaintyRadius == b.UncertaintyRadius;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
