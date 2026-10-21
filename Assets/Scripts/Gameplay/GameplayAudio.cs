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
        [SerializeField, Range(0f, 1f)] private float doorVolume = .45f;
        private AudioClip playerShot, droneShot, doorOpen;
        private AudioSource[] voices;
        private LabDoor[] doors;
        private PlayerController player;
        private int nextVoice;
        public int PlayerShotsPlayed { get; private set; }
        public int DroneShotsPlayed { get; private set; }
        public int DoorsPlayed { get; private set; }

        private void Awake()
        {
            playerShot = Resources.Load<AudioClip>("Audio/PlayerPistol");
            droneShot = Resources.Load<AudioClip>("Audio/DroneShot");
            doorOpen = Resources.Load<AudioClip>("Audio/LabDoorOpen");
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
        }

        public void Initialize(PlayerController owner, LabDoor[] gates)
        {
            BindDoors(false);
            player = owner; doors = gates;
            var camera = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (camera != null && FindAnyObjectByType<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            if (isActiveAndEnabled) BindDoors(true);
        }

        private void OnEnable() { SoundEvents.NoiseEmitted += OnNoise; BindDoors(true); }
        private void OnDisable()
        {
            SoundEvents.NoiseEmitted -= OnNoise; BindDoors(false);
            if (voices != null) foreach (var voice in voices) if (voice != null) voice.Stop();
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
            if (Play(doorOpen, door.transform.position + Vector3.up * 1.2f, doorVolume, true,
                Mathf.Clamp(doorOpen != null ? doorOpen.length / door.OpeningDuration : 1f, .5f, 2f))) DoorsPlayed++;
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
