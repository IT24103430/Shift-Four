using UnityEngine;

namespace ShiftFour
{
    public sealed class ConsoleSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private string sectionName = "Lab";
        private bool activated;
        [SerializeField] private Renderer display;
        private Renderer button;
        private Light buttonLight;
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
            if (display == null) return;

            // Keep the existing indicator as a recessed bezel and put a round switch on it.
            display.transform.localScale = new Vector3(.55f, .42f, .08f);
            var bezel = new MaterialPropertyBlock();
            bezel.SetColor("_BaseColor", new Color(.055f, .065f, .075f));
            bezel.SetColor("_Color", new Color(.055f, .065f, .075f));
            display.SetPropertyBlock(bezel);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Red activation button";
            cap.transform.SetParent(transform, false);
            cap.transform.localPosition = new Vector3(0f, .25f, -.64f);
            cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cap.transform.localScale = new Vector3(.22f, .07f, .22f);
            cap.GetComponent<Collider>().enabled = false;
            button = cap.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.EnableKeyword("_EMISSION");
            button.material = material;
            buttonLight = cap.AddComponent<Light>();
            buttonLight.type = LightType.Point;
            buttonLight.range = 1.5f;
            buttonLight.color = new Color(1f, .08f, .05f);
            buttonLight.shadows = LightShadows.None;
            SetButtonState();
        }

        private void SetButtonState()
        {
            if (button == null) return;
            var color = activated ? new Color(.24f, .035f, .025f) : new Color(1f, .07f, .045f);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", activated ? Color.black : color * 2.5f);
            button.SetPropertyBlock(properties);
            button.transform.localPosition = new Vector3(0f, .25f, activated ? -.59f : -.64f);
            buttonLight.intensity = activated ? 0f : 1.25f;
        }

        public void Interact()
        {
            if (activated || GameSession.Instance == null || GameSession.Instance.Ended || !MissionProgress.Valid(GateId)) return;
            activated = true;
            SetButtonState();
            GameSession.Instance?.ConsoleActivated(GateId);
            SoundEvents.ReportNoise(transform.position, NoiseCategory.ConsoleInteraction,
                SoundEvents.DefaultIntensity(NoiseCategory.ConsoleInteraction), this);
        }
    }
}
