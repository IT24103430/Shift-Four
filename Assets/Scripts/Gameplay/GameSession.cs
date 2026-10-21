using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftFour
{
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public PlayerController Player { get; private set; }
        public WaypointGraph Graph { get; private set; }
        public MissionProgress Progress { get; } = new MissionProgress();
        public int ConsolesOnline => Progress.ConsolesOnline;
        public int DronesRemaining => 4 - Progress.DronesDefeated;
        public bool Ended { get; private set; }

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
        public void EnterRoom(int id) { if (MissionProgress.Valid(id)) currentRoom = id; }

        private void Awake()
        {
            Instance = this;
            Graph = FindAnyObjectByType<WaypointGraph>();
            Player = FindAnyObjectByType<PlayerController>();
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
            string cause = dead.LastDamage.Attacker is DroneBrain attacker ? attacker.AgentName : "damage";
            Finish($"SYSTEM FAILURE — PLAYER ELIMINATED\nHealth depleted by {cause}\nPress R to restart");
        }
        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            if (Instance == this) Instance = null;
        }

        public void TryExit()
        {
            if (ConsolesOnline == 4 && DronesRemaining == 0)
                Finish("SHIFT COMPLETE — R to restart");
            else { notice = "EXIT LOCKED — complete all four drone and console objectives"; noticeUntil = Time.time + 3f; }
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
            if (Input.GetKeyDown(KeyCode.F3)) showDecisions = !showDecisions;
            if (Ended && Input.GetKeyDown(KeyCode.R)) Restart();
        }

        public void Restart()
        {
            if (Ended)
            {
                var scene = SceneManager.GetActiveScene();
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
