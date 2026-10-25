using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    // One planting station inside Checkpoint. The countdown begins only after arming.
    public sealed class BombConsole : MonoBehaviour, IInteractable
    {
        public const float PlantingSeconds = 4.8f;
        public float PlantingProgress { get; private set; }
        private GameSession plantingSession;
        private Transform charge;
        private FirstPersonPistolGrip plantingArms;
        private FirstPersonWeaponMount weaponMount;
        private Vector3 savedViewPosition;
        private Quaternion leftWristRotation, rightWristRotation;
        private Quaternion keypadWristRotation;
        private TextMesh display;
        private Renderer indicator;
        private MaterialPropertyBlock indicatorProperties;
        private Camera view;
        private Quaternion savedViewRotation;
        private readonly List<Material> materials = new List<Material>();
        private bool planting;
        private bool planted;
        private Vector3 MountPosition => transform.position + Vector3.up * .48f;

        public string Prompt
        {
            get
            {
                var session = GameSession.Instance;
                if (session == null) return string.Empty;
                if (session.BombPlanted) return "BOMB ARMED - REACH THE EXIT";
                if (planting) return "PLANTING TIME BOMB...";
                if (!session.CanPlay) return string.Empty;
                return "E: PLANT TIME BOMB - START DESTRUCTION TIMER";
            }
        }

        public void Interact()
        {
            var session = GameSession.Instance;
            if (!isActiveAndEnabled || planting || session == null || !session.BeginPlanting()) return;
            plantingSession = session;
            StartCoroutine(Plant());
        }

        public static void Install()
        {
            if (FindAnyObjectByType<BombConsole>() != null) return;
            LabDoor checkpointGate = null;
            foreach (var gate in FindObjectsByType<LabDoor>())
                if (gate.GateId == 0) { checkpointGate = gate; break; }
            if (checkpointGate == null) return;
            var device = new GameObject();
            device.name = "Checkpoint time bomb planting station";
            device.transform.position = checkpointGate.transform.position + new Vector3(-2.8f, .45f, -3.2f);
            device.AddComponent<BoxCollider>().size = new Vector3(.5f, .8f, .45f);
            var station = device.AddComponent<BombConsole>();
            station.Part("Planting pedestal", device.transform, Vector3.zero, new Vector3(.5f, .8f, .45f),
                station.MakeMaterial(new Color(.12f, .18f, .2f)));
            station.BuildCharge();
        }

        private Material MakeMaterial(Color color, bool emissive = false)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            materials.Add(material);
            return material;
        }

        private Transform Part(string label, Transform parent, Vector3 position, Vector3 scale,
            Material material, PrimitiveType type = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private void BuildCharge()
        {
            var black = MakeMaterial(new Color(.025f, .035f, .04f));
            var olive = MakeMaterial(new Color(.3f, .32f, .17f));
            var metal = MakeMaterial(new Color(.35f, .4f, .42f));
            var red = MakeMaterial(new Color(.9f, .035f, .02f), true);
            var amber = MakeMaterial(new Color(1f, .55f, .08f), true);
            Part("Mounting plate", transform, new Vector3(0, .408f, 0), new Vector3(.6f, .032f, .54f), metal);
            // A unit-scale parent keeps the carried device rigid as its angle changes.
            charge = new GameObject("Plantable time bomb").transform;
            charge.SetParent(transform, false);
            Part("Charge casing", charge, Vector3.zero, new Vector3(.46f, .13f, .32f), olive);
            for (int i = -1; i <= 1; i += 2)
                Part("Retention strap", charge, new Vector3(i * .15f, .005f, 0), new Vector3(.035f, .145f, .33f), black);
            Part("Timer housing", charge, new Vector3(-.055f, .105f, -.035f), new Vector3(.21f, .04f, .17f), black)
                .localRotation = Quaternion.Euler(-35, 0, 0);
            var screen = new GameObject("Countdown display");
            screen.transform.SetParent(charge, false);
            screen.transform.localPosition = new Vector3(-.055f, .128f, -.051f);
            screen.transform.localRotation = Quaternion.Euler(55, 0, 0);
            display = screen.AddComponent<TextMesh>();
            display.anchor = TextAnchor.MiddleCenter;
            display.alignment = TextAlignment.Center;
            display.fontSize = 48;
            display.characterSize = .015f;
            display.color = new Color(1f, .28f, .08f);
            display.text = "SAFE";
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    Part("Arming keypad", charge, new Vector3(.08f + col * .035f, .082f, -.07f + row * .035f),
                        new Vector3(.025f, .015f, .025f), metal);
            indicator = Part("Armed indicator", charge, new Vector3(.19f, .08f, .105f), Vector3.one * .025f, red, PrimitiveType.Sphere).GetComponent<Renderer>();
            indicatorProperties = new MaterialPropertyBlock();
            Part("Timer lead", charge, new Vector3(0, .075f, .12f), new Vector3(.3f, .012f, .012f), amber);
            charge.gameObject.SetActive(false);
        }

        // Quintic easing gives zero velocity and acceleration at both ends of every stage.
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
        private static float Stage(float time, float start, float end) => Ease((time - start) / (end - start));
        private static Vector3 Arc(Vector3 from, Vector3 to, float amount, float height)
            => Vector3.Lerp(from, to, amount) + Vector3.up * (16f * height * amount * amount * (1f - amount) * (1f - amount));
        private string TimerLabel => (Mathf.CeilToInt(plantingSession.DetonationSeconds) / 60).ToString("00") + ":" +
            (Mathf.CeilToInt(plantingSession.DetonationSeconds) % 60).ToString("00");

        private void CreatePlantingArms()
        {
            if (view == null) return;
            var prefab = Resources.Load<GameObject>("Weapons/FirstPersonArmsRig");
            if (prefab == null) return;
            var rig = Instantiate(prefab, view.transform, false);
            rig.name = "Bomb planting arms";
            rig.transform.localPosition = new Vector3(0, -.18f, .55f);
            plantingArms = rig.GetComponent<FirstPersonPistolGrip>();
            if (plantingArms == null || !plantingArms.Initialize()) { Destroy(rig); plantingArms = null; return; }
            leftWristRotation = plantingArms.SupportHandTarget.localRotation;
            rightWristRotation = plantingArms.RightHandTarget.localRotation;
            keypadWristRotation = rightWristRotation;
            var hand = rig.GetComponent<FirstPersonArmsRig>().RightHand;
            Transform index = null, pinky = null;
            foreach (var bone in hand.GetComponentsInChildren<Transform>())
            {
                if (bone.name.StartsWith("f_index.01")) index = bone;
                if (bone.name.StartsWith("f_pinky.01")) pinky = bone;
            }
            if (index != null && pinky != null)
            {
                Vector3 forward = hand.InverseTransformDirection((index.position + pinky.position) * .5f - hand.position);
                Vector3 across = hand.InverseTransformDirection(pinky.position - index.position);
                keypadWristRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.Cross(forward, across)));
            }
            foreach (var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen = true;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            rig.SetActive(false);
        }

        private IEnumerator Plant()
        {
            planting = true;
            planted = false;
            PlantingProgress = 0f;
            if (charge == null) BuildCharge();
            view = plantingSession.Player != null ? plantingSession.Player.ViewCamera : null;
            savedViewRotation = view != null ? view.transform.rotation : Quaternion.identity;
            savedViewPosition = view != null ? view.transform.position : MountPosition + Vector3.back;
            weaponMount = plantingSession.Player != null ? plantingSession.Player.GetComponent<FirstPersonWeaponMount>() : null;
            weaponMount?.RequestUnequip();
            CreatePlantingArms();
            Vector3 approach = Vector3.ProjectOnPlane(MountPosition - savedViewPosition, Vector3.up).normalized;
            if (approach.sqrMagnitude < .01f) approach = Vector3.forward;
            Quaternion facing = Quaternion.LookRotation(approach);
            Vector3 closeView = MountPosition - approach * .65f + Vector3.up * .45f;
            Quaternion closeLook = Quaternion.LookRotation(MountPosition - closeView);
            float elapsed = 0f;
            while (elapsed < PlantingSeconds && plantingSession != null && plantingSession.CanPlay && plantingSession.IsPlanting)
            {
                PlantingProgress = Mathf.Clamp01(elapsed / PlantingSeconds);
                float focus = Stage(elapsed, 0f, 1f) * (1f - Stage(elapsed, 4.1f, PlantingSeconds));
                if (view != null)
                {
                    view.transform.SetPositionAndRotation(Vector3.Lerp(savedViewPosition, closeView, focus),
                        Quaternion.Slerp(savedViewRotation, closeLook, focus));
                }
                Vector3 held = closeView + facing * new Vector3(0, -.27f, .42f);
                Vector3 stowed = closeView + facing * new Vector3(0, -.9f, .12f);
                float reveal = Stage(elapsed, .35f, 1.15f);
                float place = Stage(elapsed, 1.2f, 2.55f);
                Vector3 carry = Arc(stowed, held, reveal, .04f);
                charge.SetPositionAndRotation(Arc(carry, MountPosition, place, .08f),
                    facing * Quaternion.Euler(Mathf.Lerp(-12f, 0f, place), 0, Mathf.Lerp(-5f, 0, place)));
                charge.gameObject.SetActive(elapsed >= .35f);

                Vector3 left = charge.TransformPoint(new Vector3(-.24f, -.015f, -.035f));
                Vector3 right = charge.TransformPoint(new Vector3(.24f, -.015f, -.035f));
                Vector3 keypad = charge.TransformPoint(new Vector3(.12f, .16f, -.11f));
                float reach = Stage(elapsed, 2.55f, 2.95f);
                right = Arc(right, keypad, reach, .055f);
                // Two deliberate presses with eased down/up strokes, not an oscillating hand.
                float press = Stage(elapsed, 3f, 3.13f) * (1 - Stage(elapsed, 3.13f, 3.28f)) +
                    Stage(elapsed, 3.4f, 3.53f) * (1 - Stage(elapsed, 3.53f, 3.68f));
                right -= Vector3.up * (.045f * press);
                float release = Stage(elapsed, 3.75f, 4.25f);
                Vector3 leftAway = closeView + facing * new Vector3(-.3f, -.85f, .1f);
                Vector3 rightAway = closeView + facing * new Vector3(.3f, -.85f, .1f);
                left = Arc(left, leftAway, release, .03f);
                right = Arc(right, rightAway, release, .03f);
                if (plantingArms != null)
                {
                    plantingArms.gameObject.SetActive(elapsed >= .35f && elapsed < 4.3f);
                    plantingArms.ApplyPlantingPose(left, facing * leftWristRotation,
                        right, facing * Quaternion.Slerp(rightWristRotation, keypadWristRotation, reach));
                }
                display.text = elapsed < 2.55f ? "SAFE" : elapsed < 3.7f ? "ARM" : TimerLabel;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (plantingSession != null && plantingSession.CanPlay && plantingSession.IsPlanting)
            {
                charge.SetPositionAndRotation(MountPosition, facing);
                plantingSession.CompletePlanting();
                planted = plantingSession.BombPlanted;
            }
            PlantingProgress = planted ? 1f : 0f;
            RestorePlayer();
        }

        private void Update()
        {
            var session = GameSession.Instance;
            if (!planted || session == null || display == null) return;
            int remaining = Mathf.CeilToInt(session.BombRemaining);
            display.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            indicatorProperties.SetColor("_EmissionColor", Color.red * (Mathf.Sin(Time.time * 12f) > 0 ? 4f : .1f));
            indicator.SetPropertyBlock(indicatorProperties);
        }

        private void RestorePlayer()
        {
            if (planting && plantingSession != null) plantingSession.CancelPlanting();
            planting = false;
            if (view != null) view.transform.SetPositionAndRotation(savedViewPosition, savedViewRotation);
            weaponMount?.RequestEquip();
            weaponMount = null;
            view = null;
            if (plantingArms != null) Destroy(plantingArms.gameObject);
            plantingArms = null;
            if (charge != null) charge.gameObject.SetActive(planted);
        }
        private void OnDisable() { StopAllCoroutines(); RestorePlayer(); }
        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }

        private void OnGUI()
        {
            if (!planting) return;
            var oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.depth = -25;
            float width = Mathf.Min(420, Screen.width - 40);
            var rect = new Rect((Screen.width - width) / 2, Screen.height * .72f, width, 64);
            GUI.color = new Color(.02f, .04f, .05f, .95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, .65f, .18f);
            GUI.DrawTexture(new Rect(rect.x + 14, rect.yMax - 14, (width - 28) * PlantingProgress, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 14, rect.y + 12, width - 28, 28),
                PlantingProgress < .53f ? "PLANTING TIME BOMB..." : PlantingProgress < .78f ? "SETTING DESTRUCTION TIMER..." : "ARMING / " + TimerLabel);
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }
    }
}
