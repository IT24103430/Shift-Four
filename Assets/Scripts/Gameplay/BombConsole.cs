using UnityEngine;

namespace ShiftFour
{
    // The device is placed on the Storage side of the exit without changing the baked floor.
    public sealed class BombConsole : MonoBehaviour, IInteractable
    {
        private Renderer lamp;
        private MaterialPropertyBlock properties;
        private bool lastReady;
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

            var lampObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampObject.name = "Bomb status light";
            lampObject.transform.SetParent(device.transform, false);
            lampObject.transform.localPosition = new Vector3(0f, .28f, -.53f);
            lampObject.transform.localScale = new Vector3(.7f, .2f, .12f);
            Destroy(lampObject.GetComponent<Collider>());
            console.lamp = lampObject.GetComponent<Renderer>();
            console.properties = new MaterialPropertyBlock();
            console.SetLamp(new Color(.9f, .16f, .08f));
        }

        private void Update()
        {
            var session = GameSession.Instance;
            if (session == null || lamp == null) return;
            bool ready = session.BombDefused || (session.BombArmed && session.Progress.Complete);
            if (ready == lastReady) return;
            lastReady = ready;
            SetLamp(session.BombDefused ? new Color(.12f, .9f, .45f) : new Color(1f, .7f, .12f));
        }

        private void SetLamp(Color color)
        {
            lamp.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", color);
            lamp.SetPropertyBlock(properties);
        }
    }
}
