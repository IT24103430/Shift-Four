using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    /// <summary>Scene-owned, bounded audible effects. Playback never emits gameplay noises.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        public const int VoiceCapacity = 16;
        [SerializeField, Range(0f, 1f)] private float playerShotVolume = .65f;
        [SerializeField, Range(0f, 1f)] private float droneShotVolume = .5f;
        [SerializeField, Range(0f, 1f)] private float doorVolume = .85f;
        [SerializeField, Range(0f, 1f)] private float droneFlightVolume = .45f;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = .12f;
        [SerializeField, Range(0f, 1f)] private float reloadVolume = .65f;
        private AudioSource backgroundMusic;
        private AudioSource reloadSource;
        private AudioClip reloadClip;
        private WeaponController weapons;
        private readonly Dictionary<LabDoor, AudioSource> doorSources = new Dictionary<LabDoor, AudioSource>();
        private AudioClip playerShot, droneShot, doorOpen;
        private AudioClip droneFlight;
        private AudioSource[] flightSources;
        private Health[] flightHealth;
        private AudioSource[] voices;
        private LabDoor[] doors;
        private PlayerController player;
        private int nextVoice;
        public int PlayerShotsPlayed { get; private set; }
        public int DroneShotsPlayed { get; private set; }
        public int DoorsPlayed { get; private set; }
        public int ReloadsPlayed { get; private set; }

        private void Awake()
        {
            playerShot = Resources.Load<AudioClip>("Audio/PlayerPistol");
            droneShot = Resources.Load<AudioClip>("Audio/DroneShot");
            doorOpen = Resources.Load<AudioClip>("Audio/LabDoorOpen");
            droneFlight = Resources.Load<AudioClip>("Audio/DroneFlight");
            reloadClip = Resources.Load<AudioClip>("Audio/PlayerReload");
            voices = new AudioSource[VoiceCapacity];
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = new GameObject("Gameplay audio voice " + i);
                voice.transform.SetParent(transform, false);
                var source = voices[i] = voice.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false;
                source.dopplerLevel = 0f; source.minDistance = 2f; source.maxDistance = 45f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
            }
            var musicClip = Resources.Load<AudioClip>("Audio/BackgroundMusic");
            if (musicClip != null)
            {
                var music = new GameObject("Background music");
                music.transform.SetParent(transform, false);
                backgroundMusic = music.AddComponent<AudioSource>();
                backgroundMusic.clip = musicClip;
                backgroundMusic.loop = true; backgroundMusic.playOnAwake = false;
                backgroundMusic.spatialBlend = 0f; backgroundMusic.dopplerLevel = 0f;
                backgroundMusic.volume = backgroundMusicVolume;
                backgroundMusic.priority = 192;
            }
        }

        public void Initialize(PlayerController owner, LabDoor[] gates)
        {
            BindDoors(false);
            BindWeapon(false);
            ClearDedicatedSources();
            player = owner; doors = gates;
            weapons = player != null ? player.GetComponent<WeaponController>() : null;
            var camera = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (camera != null && FindAnyObjectByType<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            InitializeDedicatedSources();
            if (isActiveAndEnabled) { BindDoors(true); BindWeapon(true); }
            InitializeFlightLoops();
        }

        private void InitializeDedicatedSources()
        {
            if (doors != null)
                foreach (var door in doors)
                {
                    if (door == null) continue;
                    var child = new GameObject("Door opening audio");
                    child.transform.SetParent(door.transform, false);
                    child.transform.localPosition = Vector3.up * 1.2f;
                    var source = child.AddComponent<AudioSource>();
                    source.clip = doorOpen; source.playOnAwake = false;
                    source.spatialBlend = 1f; source.volume = doorVolume; source.dopplerLevel = 0f;
                    source.minDistance = 8f; source.maxDistance = 50f;
                    source.rolloffMode = AudioRolloffMode.Linear; source.priority = 32;
                    doorSources.Add(door, source);
                }
            if (player != null)
            {
                var child = new GameObject("Player reload audio");
                child.transform.SetParent(player.transform, false);
                reloadSource = child.AddComponent<AudioSource>();
                reloadSource.clip = reloadClip; reloadSource.playOnAwake = false;
                reloadSource.spatialBlend = 0f; reloadSource.volume = reloadVolume;
                reloadSource.dopplerLevel = 0f; reloadSource.priority = 64;
            }
        }

        private void ClearDedicatedSources()
        {
            foreach (var source in doorSources.Values)
                if (source != null) { source.Stop(); Destroy(source.gameObject); }
            doorSources.Clear();
            if (reloadSource != null) { reloadSource.Stop(); Destroy(reloadSource.gameObject); }
            reloadSource = null;
        }

        private void BindWeapon(bool bind)
        {
            if (weapons == null) return;
            weapons.ReloadStateChanged -= OnReloadStateChanged;
            if (bind) weapons.ReloadStateChanged += OnReloadStateChanged;
        }

        private void OnReloadStateChanged(WeaponReloadState state)
        {
            if (reloadSource == null) return;
            if (state != WeaponReloadState.Reloading) { reloadSource.Stop(); return; }
            if (reloadClip == null) return;
            reloadSource.Play();
            ReloadsPlayed++;
        }

        private void InitializeFlightLoops()
        {
            ClearFlightLoops();
            if (droneFlight == null) return;
            var drones = FindObjectsByType<DroneBrain>();
            flightSources = new AudioSource[drones.Length];
            flightHealth = new Health[drones.Length];
            for (int i = 0; i < drones.Length; i++)
            {
                var loop = new GameObject("Drone flight loop");
                loop.transform.SetParent(drones[i].transform, false);
                var source = flightSources[i] = loop.AddComponent<AudioSource>();
                flightHealth[i] = drones[i].GetComponent<Health>();
                source.clip = droneFlight; source.loop = true; source.playOnAwake = false;
                source.spatialBlend = 1f; source.volume = droneFlightVolume; source.dopplerLevel = 0f;
                source.minDistance = 4f; source.maxDistance = 28f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
            }
        }

        private void Update()
        {
            if (flightSources == null) return;
            bool ended = GameSession.Instance != null && GameSession.Instance.Ended;
            for (int i = 0; i < flightSources.Length; i++)
            {
                var source = flightSources[i];
                if (source == null) continue;
                bool flying = !ended && source.gameObject.activeInHierarchy &&
                    flightHealth[i] != null && flightHealth[i].IsAlive;
                if (flying && !source.isPlaying) source.Play();
                else if (!flying && source.isPlaying) source.Stop();
            }
        }

        private void ClearFlightLoops()
        {
            if (flightSources != null)
                foreach (var source in flightSources)
                    if (source != null) { source.Stop(); Destroy(source.gameObject); }
            flightSources = null; flightHealth = null;
        }

        private void OnDestroy()
        {
            BindWeapon(false);
            ClearDedicatedSources();
            ClearFlightLoops();
        }

        private void OnEnable()
        {
            SoundEvents.NoiseEmitted += OnNoise; BindDoors(true); BindWeapon(true);
            if (backgroundMusic != null && !backgroundMusic.isPlaying) backgroundMusic.Play();
        }
        private void OnDisable()
        {
            SoundEvents.NoiseEmitted -= OnNoise; BindDoors(false); BindWeapon(false);
            if (voices != null) foreach (var voice in voices) if (voice != null) voice.Stop();
            if (flightSources != null) foreach (var source in flightSources) if (source != null) source.Stop();
            if (backgroundMusic != null) backgroundMusic.Stop();
            if (reloadSource != null) reloadSource.Stop();
            foreach (var source in doorSources.Values) if (source != null) source.Stop();
        }
        private void BindDoors(bool bind)
        {
            if (doors == null) return;
            foreach (var door in doors)
            {
                if (door == null) continue;
                door.OpeningStarted -= OnDoor;
                if (bind) door.OpeningStarted += OnDoor;
            }
        }
        private void OnNoise(NoiseEvent noise)
        {
            if (noise.Category != NoiseCategory.Gunshot) return;
            bool drone = noise.Source is DroneBrain;
            bool local = !drone && player != null && (noise.Position - player.transform.position).sqrMagnitude < .04f;
            if (!Play(drone ? droneShot : playerShot, noise.Position, drone ? droneShotVolume : playerShotVolume, !local)) return;
            if (drone) DroneShotsPlayed++; else PlayerShotsPlayed++;
        }
        private void OnDoor(LabDoor door)
        {
            if (!doorSources.TryGetValue(door, out var source) || source == null || source.clip == null) return;
            // A dedicated voice keeps the supplied recording audible through busy firefights.
            source.Play();
            DoorsPlayed++;
        }
        private bool Play(AudioClip clip, Vector3 position, float volume, bool spatial, float pitch = 1f)
        {
            if (clip == null || voices == null) return false;
            // Prefer an idle voice; replace the oldest round-robin voice only when all are busy.
            int slot = nextVoice;
            for (int i = 0; i < voices.Length; i++)
            {
                int candidate = (nextVoice + i) % voices.Length;
                if (!voices[candidate].isPlaying) { slot = candidate; break; }
            }
            var voice = voices[slot]; nextVoice = (slot + 1) % voices.Length;
            voice.Stop(); voice.transform.position = position;
            voice.clip = clip; voice.volume = volume; voice.pitch = pitch;
            voice.spatialBlend = spatial ? 1f : 0f; voice.Play();
            return true;
        }
    }
}
