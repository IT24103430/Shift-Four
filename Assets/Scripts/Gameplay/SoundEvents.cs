using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftFour
{
    public static class SoundEvents
    {
        private static readonly Signal<NoiseEvent> noises = new Signal<NoiseEvent>();
        private static readonly Signal<Vector3> shots = new Signal<Vector3>();

        public static event Action<NoiseEvent> NoiseEmitted
        {
            add => noises.Add(value);
            remove => noises.Remove(value);
        }

        // Compatibility channel. A gunshot passes through the same dispatcher exactly once.
        public static event Action<Vector3> ShotFired
        {
            add => shots.Add(value);
            remove => shots.Remove(value);
        }

        static SoundEvents() => BindSceneCleanup();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            noises.Clear(); shots.Clear();
            BindSceneCleanup(); // Also handles Enter Play Mode with domain reload disabled.
        }

        private static void BindSceneCleanup()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            noises.Prune(scene, true);
            shots.Prune(scene, true);
        }

        public static float DefaultIntensity(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Footstep: return .2f;
                case NoiseCategory.Sprint: return .45f;
                case NoiseCategory.Gunshot: return 1f;
                case NoiseCategory.ConsoleInteraction: return .4f;
                case NoiseCategory.Impact: return .3f;
                default: return 0f;
            }
        }

        public static void ReportShot(Vector3 position)
            => ReportNoise(position, NoiseCategory.Gunshot, DefaultIntensity(NoiseCategory.Gunshot));

        public static bool ReportNoise(Vector3 position, NoiseCategory category, float intensity,
            UnityEngine.Object source = null)
            => ReportNoise(new NoiseEvent(position, category, intensity, Time.time, source));

        // Explicit timestamps make simulation/replay tests independent of the Unity clock.
        public static bool ReportNoise(NoiseEvent noise)
        {
            if (!noise.IsValid) return false;
            // Freeze both channels for this logical event. Changes during a callback apply
            // to the next event; destroyed Unity targets are always skipped.
            var noiseSnapshot = noises.Snapshot;
            var shotSnapshot = shots.Snapshot;
            noises.Dispatch(noiseSnapshot, noise);
            if (noise.Category == NoiseCategory.Gunshot) shots.Dispatch(shotSnapshot, noise.Position);
            return true;
        }

        // Copy-on-write registration: normal dispatch allocates no invocation-list arrays.
        // Unique callbacks preserve their original registration order, including duplicate +=.
        private sealed class Signal<T>
        {
            public Action<T>[] Snapshot { get; private set; } = Array.Empty<Action<T>>();

            public void Add(Action<T> listener)
            {
                if (listener == null) return;
                foreach (Action<T> callback in listener.GetInvocationList())
                {
                    if (Dead(callback) || Array.IndexOf(Snapshot, callback) >= 0) continue;
                    var next = new Action<T>[Snapshot.Length + 1];
                    Array.Copy(Snapshot, next, Snapshot.Length);
                    next[next.Length - 1] = callback;
                    Snapshot = next;
                }
            }

            public void Remove(Action<T> listener)
            {
                if (listener == null) return;
                foreach (Action<T> callback in listener.GetInvocationList()) RemoveSingle(callback);
            }

            private void RemoveSingle(Action<T> callback)
            {
                int index = Array.IndexOf(Snapshot, callback);
                if (index < 0) return;
                var next = new Action<T>[Snapshot.Length - 1];
                Array.Copy(Snapshot, 0, next, 0, index);
                Array.Copy(Snapshot, index + 1, next, index, next.Length - index);
                Snapshot = next;
            }

            public void Clear() => Snapshot = Array.Empty<Action<T>>();

            private static bool Dead(Action<T> callback)
                => callback.Target is UnityEngine.Object owner && owner == null;

            private static bool Stale(Action<T> callback, Scene scene, bool includeScene)
            {
                if (Dead(callback)) return true;
                if (!includeScene) return false;
                if (callback.Target is Component component) return component.gameObject.scene == scene;
                return callback.Target is GameObject go && go.scene == scene;
            }

            public void Prune(Scene scene, bool includeScene)
            {
                int keep = 0;
                foreach (var callback in Snapshot) if (!Stale(callback, scene, includeScene)) keep++;
                if (keep == Snapshot.Length) return;
                var next = new Action<T>[keep]; int index = 0;
                foreach (var callback in Snapshot)
                    if (!Stale(callback, scene, includeScene)) next[index++] = callback;
                Snapshot = next;
            }

            public void Dispatch(Action<T>[] snapshot, T value)
            {
                bool dead = false;
                foreach (var callback in snapshot)
                {
                    if (Dead(callback)) { dead = true; continue; }
                    try { callback(value); }
                    catch (Exception exception)
                    {
                        // One broken observer must not suppress later listeners or the legacy
                        // gunshot channel. Remove it and report once, rather than log every shot.
                        RemoveSingle(callback);
                        Debug.LogException(exception);
                    }
                }
                if (dead) Prune(default, false);
            }
        }
    }
}
