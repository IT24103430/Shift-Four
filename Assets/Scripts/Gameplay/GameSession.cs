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

        private void Awake()
        {
            Instance = this;
            Graph = FindAnyObjectByType<WaypointGraph>();
            Player = FindAnyObjectByType<PlayerController>();
        }

        private void Start()
        {
            drones = FindObjectsByType<DroneBrain>();

            if (Player != null) Player.GetComponent<Health>().Died += _ => Finish("SYSTEM FAILURE — R to restart");
        }

        public void ConsoleActivated(int id) { if (!Ended) Progress.ActivateConsole(id); }
        public void DroneDestroyed(DroneBrain drone)
        {
            int id = drone is ScoutBrain ? 0 : drone is GuardBrain ? 1 :
                drone is InterceptorBrain ? 2 : drone is FlankerBrain ? 3 : -1;
            if (!Ended) Progress.DefeatDrone(id);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void TryExit()
        {
            if (ConsolesOnline == 4 && DronesRemaining == 0)
                Finish("SHIFT COMPLETE — R to restart");
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
            if (Ended && Input.GetKeyDown(KeyCode.R))
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
            if (Player == null) return;
            GUI.Box(new Rect(12, 12, 420, 104),
                $"SHIFT FOUR\nHealth: {Player.GetComponent<Health>().Current:0} / {Player.GetComponent<Health>().Maximum:0}    Consoles: {ConsolesOnline}/4    Drones: {DronesRemaining}\nWASD move  |  Mouse aim  |  Click fire  |  E interact  |  Escape unlock cursor");
            GUI.Label(new Rect(Screen.width / 2 - 8, Screen.height / 2 - 14, 24, 24), "+");
            if (drones != null)
            {
                string status = "AGENT DECISIONS (viva view)\n";
                foreach (DroneBrain drone in drones)
                    if (drone != null) status += drone.AgentName + ": " +
                        (drone.GetComponent<Health>().IsAlive ? drone.CurrentDecision : "Disabled") + "\n";
                GUI.Box(new Rect(Screen.width - 302, 12, 290, 126), status);
            }
            if (!Ended)
            {
                string prompt = Player.InteractionPrompt;
                if (!string.IsNullOrEmpty(prompt)) GUI.Box(new Rect(12, Screen.height - 62, 360, 42), prompt);
                if (ConsolesOnline == 4 && DronesRemaining == 0)
                    GUI.Box(new Rect(Screen.width / 2 - 160, 12, 320, 38), "Proceed to the green exit");
            }
            else GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 50, 400, 100), outcome);
        }
    }
}
