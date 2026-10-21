using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftFour
{
    public sealed class GameSession : MonoBehaviour
    {
        public enum LaunchPhase { NameEntry, Countdown, Playing }
        private static string savedPlayerName;
        private static bool restartingWithName;
        private const float CountdownSeconds = 3f;
        private const float BombSeconds = 120f;
        public static GameSession Instance { get; private set; }
        public PlayerController Player { get; private set; }
        public WaypointGraph Graph { get; private set; }
        public MissionProgress Progress { get; } = new MissionProgress();
        public int ConsolesOnline => Progress.ConsolesOnline;
        public int DronesRemaining => 4 - Progress.DronesDefeated;
        public bool Ended { get; private set; }
        public bool Won { get; private set; }
        public LaunchPhase Phase { get; private set; } = LaunchPhase.Playing;
        public bool CanPlay => Phase == LaunchPhase.Playing && !Ended;
        public string PlayerName { get; private set; } = "PLAYER";
        public float CountdownRemaining => Phase == LaunchPhase.Countdown ? Mathf.Max(0f, countdownEndsAt - Time.unscaledTime) : 0f;
        public bool ShowGo => CanPlay && Time.unscaledTime < goUntil;
        public bool BombArmed { get; private set; }
        public bool BombDefused { get; private set; }
        public float BombRemaining => BombArmed && !BombDefused ? Mathf.Max(0f, bombEndsAt - Time.time) : BombSeconds;
        private float countdownEndsAt;
        private float goUntil;
        private float bombEndsAt;

        private string outcome = string.Empty;
        private DroneBrain[] drones;
        private LabDoor[] gates;
        private Health playerHealth;
        private int currentRoom;
        private bool showDecisions;
        private string notice = string.Empty;
        private float noticeUntil;
        public int CurrentRoom => currentRoom;
        public string Notice => Time.time < noticeUntil ? notice : string.Empty;
        public string Outcome => outcome;
        public bool ShowDecisions => showDecisions;
        public void EnterRoom(int id)
        {
            if (!MissionProgress.Valid(id) || !CanPlay) return;
            currentRoom = id;
            if (id == 3 && !BombArmed)
            {
                BombArmed = true;
                bombEndsAt = Time.time + BombSeconds;
                notice = "BOMB ARMED - 02:00 TO DEFUSE";
                noticeUntil = Time.time + 4f;
            }
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
                    Phase = LaunchPhase.NameEntry;
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
            Phase = LaunchPhase.Countdown;
            countdownEndsAt = Time.unscaledTime + CountdownSeconds;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void DefuseBomb()
        {
            if (!CanPlay || !BombArmed || BombDefused || !Progress.Complete || BombRemaining <= 0f) return;
            BombDefused = true;
            notice = "BOMB DEFUSED - REACH THE EXIT";
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
            if (ConsolesOnline == 4 && DronesRemaining == 0 && BombDefused)
            {
                Won = true;
                Finish("SHIFT COMPLETE — R to restart");
            }
            else { notice = Progress.Complete ? "EXIT LOCKED - DEFUSE THE BOMB" :
                "EXIT LOCKED - complete all four drone and console objectives"; noticeUntil = Time.time + 3f; }
        }

        private void Finish(string message)
        {
            if (Ended) return;
            Ended = true;
            outcome = message;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (Phase == LaunchPhase.Countdown && Time.unscaledTime >= countdownEndsAt)
            {
                Phase = LaunchPhase.Playing;
                Time.timeScale = 1f;
                goUntil = Time.unscaledTime + .65f;
            }
            if (CanPlay && BombArmed && !BombDefused && BombRemaining <= 0f)
                Finish("BOMB DETONATED\nThe Storage timer expired\nPress R to restart");
            if (Input.GetKeyDown(KeyCode.F3)) showDecisions = !showDecisions;
            if (Ended && Input.GetKeyDown(KeyCode.R)) Restart();
        }

        public void Restart()
        {
            if (Ended)
            {
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
