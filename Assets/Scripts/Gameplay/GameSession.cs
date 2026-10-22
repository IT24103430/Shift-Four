using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftFour
{
    public sealed class GameSession : MonoBehaviour
    {
        public enum LaunchPhase { Welcome, NameEntry, Intro, Countdown, Playing }
        private static string savedPlayerName;
        private static bool restartingWithName;
        private const float CountdownSeconds = 3f;
        [SerializeField, Min(10f)] private float detonationSeconds = 120f;
        public float DetonationSeconds => Mathf.Max(10f, detonationSeconds);
        // Increase this value to give both sides more health and less shot damage.
        public const float CombatDurationMultiplier = 1.2f;
        public static GameSession Instance { get; private set; }
        public PlayerController Player { get; private set; }
        public WaypointGraph Graph { get; private set; }
        public MissionProgress Progress { get; } = new MissionProgress();
        public int ConsolesOnline => Progress.ConsolesOnline;
        public int DronesRemaining => 4 - Progress.DronesDefeated;
        public bool Ended { get; private set; }
        public bool Won { get; private set; }
        public EndingCinematic Ending { get; private set; }
        public MissionVideo Intro { get; private set; }
        public bool IsPlanting { get; private set; }
        public bool ShowcaseActive { get; private set; }
        public LaunchPhase Phase { get; private set; } = LaunchPhase.Playing;
        public bool CanPlay => Phase == LaunchPhase.Playing && !Ended;
        public string PlayerName { get; private set; } = "PLAYER";
        public float CountdownRemaining => Phase == LaunchPhase.Countdown ? Mathf.Max(0f, countdownEndsAt - Time.unscaledTime) : 0f;
        public bool ShowGo => CanPlay && Time.unscaledTime < goUntil;
        public bool MissionTimerActive => BombPlanted;
        public bool BombPlanted { get; private set; }
        public float BombRemaining => BombPlanted ? Mathf.Max(0f, detonationAt - Time.time) : DetonationSeconds;
        public float MissionRemaining => BombRemaining;
        private float countdownEndsAt;
        private float goUntil;
        private float detonationAt;

        private string outcome = string.Empty;
        private DroneBrain[] drones;
        private LabDoor[] gates;
        private Health playerHealth;
        private int currentRoom;
        private bool showDecisions;
        private string notice = string.Empty;
        private float noticeUntil;
        public int CurrentRoom => currentRoom;
        // Tasks follow the earliest unfinished sector, even while the player revisits a cleared room.
        // Four means that every room is complete and extraction is the remaining objective.
        public int TaskRoom
        {
            get
            {
                for (int room = 0; room < 4; room++)
                    if (!Progress.CanOpen(room) || (room == 0 && !BombPlanted)) return room;
                return 4;
            }
        }
        public string Notice => Time.time < noticeUntil ? notice : string.Empty;
        public string Outcome => outcome;
        public bool ShowDecisions => showDecisions;
        public void EnterRoom(int id)
        {
            if (!MissionProgress.Valid(id) || !CanPlay) return;
            currentRoom = id;
        }

        private void Awake()
        {
            Instance = this;
            Graph = FindAnyObjectByType<WaypointGraph>();
            Player = FindAnyObjectByType<PlayerController>();
            if (gameObject.scene.name == "MainLab" && !Application.isBatchMode)
            {
                Time.timeScale = 0f;
                if (restartingWithName && !string.IsNullOrEmpty(savedPlayerName))
                {
                    Phase = LaunchPhase.Countdown;
                    countdownEndsAt = Time.unscaledTime + CountdownSeconds;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    Phase = LaunchPhase.Welcome;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            restartingWithName = false;
            if (!string.IsNullOrEmpty(savedPlayerName)) PlayerName = savedPlayerName;
        }

        private void Start()
        {
            drones = FindObjectsByType<DroneBrain>();

            gates = FindObjectsByType<LabDoor>();
            if (Player != null)
            {
                var audio = GetComponent<GameplayAudio>();
                if (audio == null) audio = gameObject.AddComponent<GameplayAudio>();
                audio.Initialize(Player, gates);
                playerHealth = Player.GetComponent<Health>();
                if (gameObject.scene.name == "MainLab")
                    playerHealth.TryInitializeMaximum(playerHealth.Maximum * CombatDurationMultiplier);
                playerHealth.Died += OnPlayerDied;
                var hud = GetComponent<CombatHud>();
                if (hud == null) hud = gameObject.AddComponent<CombatHud>();
                hud.Initialize(this, Player, drones);
            }
            if (gameObject.scene.name == "MainLab") BombConsole.Install();
        }

        public void Begin(string name)
        {
            if (Phase != LaunchPhase.NameEntry || Ended) return;
            string chosen = string.IsNullOrWhiteSpace(name) ? "PLAYER" : name.Trim();
            PlayerName = chosen.Substring(0, Mathf.Min(18, chosen.Length));
            savedPlayerName = PlayerName;
            Phase = LaunchPhase.Intro;
            Time.timeScale = 0f;
            Intro = gameObject.AddComponent<MissionVideo>();
            Intro.Play("ShiftFour_Opening_Intro.mp4", Player != null ? Player.ViewCamera : null, StartCountdown);
        }

        private void StartCountdown()
        {
            if (Ended || Phase != LaunchPhase.Intro) return;
            Phase = LaunchPhase.Countdown;
            countdownEndsAt = Time.unscaledTime + CountdownSeconds;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OpenNameEntry()
        {
            if (Phase == LaunchPhase.Welcome && !Ended) Phase = LaunchPhase.NameEntry;
        }

        public void BeginShowcase(string name)
        {
            if (gameObject.scene.name != "MainLab" || Ended || ShowcaseActive) return;
            if (Phase == LaunchPhase.Welcome) OpenNameEntry();
            if (Phase == LaunchPhase.NameEntry) Begin(name);
            ShowcaseActive = true;
            gameObject.AddComponent<ShowcaseDirector>().Initialize(this);
        }

        public bool BeginPlanting()
        {
            if (!CanPlay || IsPlanting || BombPlanted) return false;
            IsPlanting = true;
            return true;
        }

        internal void CancelPlanting() => IsPlanting = false;

        internal void CompletePlanting()
        {
            if (!IsPlanting) return;
            IsPlanting = false;
            if (!CanPlay) return;
            BombPlanted = true;
            detonationAt = Time.time + DetonationSeconds;
            notice = "BOMB ARMED - CLEAR THE SECTORS AND ESCAPE BEFORE DETONATION";
            noticeUntil = Time.time + 4f;
        }

        public void ConsoleActivated(int id) { if (!Ended) Progress.ActivateConsole(id); }
        public void DroneDestroyed(DroneBrain drone)
        {
            int id = drone is ScoutBrain ? 0 : drone is GuardBrain ? 1 :
                drone is InterceptorBrain ? 2 : drone is FlankerBrain ? 3 : -1;
            if (!Ended) Progress.DefeatDrone(id);
        }
        private void OnPlayerDied(Health dead)
        {
            string cause = dead.LastDamage.Attacker is DroneBrain attacker
                ? "Killed by the " + attacker.AgentName + " drone"
                : "Killed by an unknown source";
            Finish(cause + "\nPress R to restart");
        }
        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (Phase != LaunchPhase.Playing) Time.timeScale = 1f;
            if (Instance == this) Instance = null;
        }

        public void TryExit()
        {
            if (!CanPlay) return;
            if (ConsolesOnline == 4 && DronesRemaining == 0 && BombPlanted && BombRemaining > 0f)
            {
                Won = true;
                Finish("SHIFT COMPLETE — R to restart");
            }
            else { notice = Progress.Complete ? "EXIT LOCKED - PLANT THE BOMB IN CHECKPOINT" :
                "EXIT LOCKED - complete all four drone and console objectives"; noticeUntil = Time.time + 3f; }
        }

        private void Finish(string message)
        {
            if (Ended) return;
            Ended = true;
            IsPlanting = false;
            outcome = message;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Won && gameObject.scene.name == "MainLab" && Player != null)
            {
                Ending = gameObject.AddComponent<EndingCinematic>();
                Ending.Begin(this, Player);
            }
        }

        private void Update()
        {
            if (Phase == LaunchPhase.Countdown && Time.unscaledTime >= countdownEndsAt)
            {
                Phase = LaunchPhase.Playing;
                Time.timeScale = 1f;
                goUntil = Time.unscaledTime + .65f;
            }
            if (CanPlay && BombPlanted && BombRemaining <= 0f)
                Finish("BOMB DETONATED\nYou did not reach the exit in time\nPress R to restart");
            if (Input.GetKeyDown(KeyCode.F3)) showDecisions = !showDecisions;
            if (Ended && (Ending == null || !Ending.IsPlaying) && Input.GetKeyDown(KeyCode.R)) Restart();
        }

        public void Restart()
        {
            if (Ended)
            {
                if (Ending != null) Ending.Finish();
                var scene = SceneManager.GetActiveScene();
                restartingWithName = !string.IsNullOrEmpty(savedPlayerName);
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
                else SceneManager.LoadScene(scene.name);
            }
        }

        public string GateStatus(int id)
        {
            if (gates != null) foreach (var gate in gates)
                if (gate != null && gate.GateId == id) return gate.Status;
            return "MISSING";
        }
    }
}
