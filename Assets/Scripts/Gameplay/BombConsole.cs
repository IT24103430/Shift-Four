using UnityEngine;

namespace ShiftFour
{
    // The device is placed on the Storage side of the exit without changing the baked floor.
    public sealed class BombConsole : MonoBehaviour, IInteractable
    {
        private Renderer lamp;
        private Light buttonLight;
        private MaterialPropertyBlock properties;
        private int lastState = -1;
        public string Prompt
        {
            get
            {
                var session = GameSession.Instance;
                if (session == null) return string.Empty;
                if (session.BombDefused) return "BOMB DEFUSED";
                if (!session.BombArmed) return "SECURITY DEVICE - INACTIVE";
                return session.Progress.Complete ? "E: DEFUSE BOMB" : "CLEAR STORAGE DRONE AND CONSOLE FIRST";
            }
        }

        public void Interact() => GameSession.Instance?.DefuseBomb();

        public static void Install()
        {
            if (FindAnyObjectByType<BombConsole>() != null) return;
            LabDoor exit = null;
            foreach (var gate in FindObjectsByType<LabDoor>())
                if (gate.GateId == 3) { exit = gate; break; }
            if (exit == null) return;

            var device = GameObject.CreatePrimitive(PrimitiveType.Cube);
            device.name = "Bomb defusal unit";
            device.transform.position = exit.transform.position + new Vector3(-1.8f, .85f, 2.4f);
            device.transform.localScale = new Vector3(.5f, 1.2f, .45f);
            var shell = device.GetComponent<Renderer>();
            shell.material.color = new Color(.12f, .18f, .2f);
            var console = device.AddComponent<BombConsole>();

            var lampObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampObject.name = "Bomb defusal button";
            lampObject.transform.SetParent(device.transform, false);
            lampObject.transform.localPosition = new Vector3(0f, .28f, -.57f);
            lampObject.transform.localScale = new Vector3(.36f, .22f, .18f);
            lampObject.GetComponent<Collider>().enabled = false;
            console.lamp = lampObject.GetComponent<Renderer>();
            var buttonMaterial = new Material(Shader.Find("Standard"));
            buttonMaterial.EnableKeyword("_EMISSION");
            console.lamp.material = buttonMaterial;
            console.buttonLight = lampObject.AddComponent<Light>();
            console.buttonLight.type = LightType.Point;
            console.buttonLight.range = 1.2f;
            console.buttonLight.shadows = LightShadows.None;
            console.properties = new MaterialPropertyBlock();
            console.UpdateButtonState();
        }

        private void Update()
        {
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            var session = GameSession.Instance;
            if (session == null || lamp == null) return;
            int state = session.BombDefused ? 2 : session.BombArmed && session.Progress.Complete ? 1 : 0;
            if (state == lastState) return;
            lastState = state;
            Color color = state == 2 ? new Color(.035f, .3f, .13f) :
                state == 1 ? new Color(1f, .55f, .06f) : new Color(.3f, .09f, .025f);
            lamp.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", state == 1 ? color * 2f : Color.black);
            lamp.SetPropertyBlock(properties);
            lamp.transform.localPosition = new Vector3(0f, .28f, state == 2 ? -.53f : -.57f);
            buttonLight.color = color;
            buttonLight.intensity = state == 1 ? 1f : 0f;
        }
    }
}
