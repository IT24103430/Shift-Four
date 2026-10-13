using UnityEngine;

namespace ShiftFour
{
    public sealed class ConsoleSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private string sectionName = "Lab";
        private bool activated;
        [SerializeField] private Renderer display;
        public int GateId => MissionProgress.RoomId(sectionName);

        public string Prompt => activated ? sectionName + " online" : "E: activate " + sectionName;
        public bool Activated => activated;

        public void Configure(string label, Renderer indicator)
        {
            sectionName = label;
            display = indicator;
        }

        private void Awake()
        {
            if (display == null) display = transform.Find("Indicator")?.GetComponent<Renderer>();
        }

        public void Interact()
        {
            if (activated || GameSession.Instance == null || GameSession.Instance.Ended || !MissionProgress.Valid(GateId)) return;
            activated = true;
            if (display != null) display.material.color = new Color(0.23f, 0.85f, 0.56f);
            GameSession.Instance?.ConsoleActivated(GateId);
        }
    }
}
