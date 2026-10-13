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
                playerHealth = Player.GetComponent<Health>();
                playerHealth.Died += OnPlayerDied;
            }
        }

        public void ConsoleActivated(int id) { if (!Ended) Progress.ActivateConsole(id); }
        public void DroneDestroyed(DroneBrain drone)
        {
            int id = drone is ScoutBrain ? 0 : drone is GuardBrain ? 1 :
                drone is InterceptorBrain ? 2 : drone is FlankerBrain ? 3 : -1;
            if (!Ended) Progress.DefeatDrone(id);
        }
        private void OnPlayerDied(Health _) => Finish("SYSTEM FAILURE — R to restart");
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

        private void OnGUI()
        {
            if (Player == null || playerHealth == null) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(18, 18, 510, 94),
                $"SHIFT FOUR  /  {MissionProgress.Rooms[currentRoom].ToUpperInvariant()}\n" +
                $"Health {playerHealth.Current:0} / {playerHealth.Maximum:0}    Mission {ConsolesOnline + Progress.DronesDefeated}/8\n" +
                "WASD move   Mouse aim   Click fire   E interact   F3 agent details");
            string objectives = "SECURITY OBJECTIVES\n";
            for (int id = 0; id < 4; id++)
            {
                string gateStatus = "MISSING";
                if (gates != null) foreach (LabDoor gate in gates)
                    if (gate != null && gate.GateId == id) gateStatus = gate.Status;
                objectives += $"{MissionProgress.Rooms[id]}: {MissionProgress.Agents[id]} " +
                    (Progress.DroneDefeated(id) ? "disabled" : "active") + " / Console " +
                    (Progress.ConsoleOnline(id) ? "online" : "offline") + " / " + gateStatus + "\n";
            }
            GUI.Box(new Rect(18, 120, 510, 118), objectives);
            string next = !Progress.DroneDefeated(currentRoom) ? "Defeat " + MissionProgress.Agents[currentRoom] :
                !Progress.ConsoleOnline(currentRoom) ? "Activate the " + MissionProgress.Rooms[currentRoom] + " console" : "Proceed through the security gate";
            if (Progress.Complete) next = "All objectives complete — proceed to the green exit";
            GUI.Box(new Rect(18, height - 104, 540, 36), next);
            if (!Ended)
            {
                GUI.Label(new Rect(width / 2 - 8, height / 2 - 14, 24, 24), "+");
                string prompt = Time.time < noticeUntil ? notice : Player.InteractionPrompt;
                if (!string.IsNullOrEmpty(prompt)) GUI.Box(new Rect(18, height - 62, 650, 42), prompt);
            }
            else GUI.Box(new Rect(width / 2 - 240, height / 2 - 50, 480, 100), outcome);
            if (showDecisions && drones != null)
            {
                string status = "AGENT DECISIONS — F3 to hide\n";
                foreach (DroneBrain drone in drones)
                    if (drone != null) status += drone.AgentName + ": " +
                        (drone.GetComponent<Health>().IsAlive ? drone.CurrentDecision : "Disabled") + "\n";
                GUI.Box(new Rect(width - 370, 18, 352, 126), status);
            }
            GUI.matrix = previous;
        }
    }
}
