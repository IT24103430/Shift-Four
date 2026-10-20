using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftFour
{
    // CC0 skinned arms fitted to the pistol. Presentation bones never participate in gameplay queries.
    [DisallowMultipleComponent]
    public sealed class FirstPersonArms : MonoBehaviour
    {
        private Transform rig, shoulder, elbow, wrist;
        private Vector3 elbowRest;
        private Vector3 wristRest;
        private Material[] surfaces;
        public bool IsReady => rig != null && shoulder != null && elbow != null && wrist != null;

        public void Initialize(Transform pivot)
        {
            if (rig != null || pivot == null) return;
            var prefab = Resources.Load<GameObject>("Weapons/FirstPersonArms");
            if (prefab == null) return;
            rig = Instantiate(prefab, pivot, false).transform;
            rig.name = "FirstPersonArms";
            foreach (var part in rig.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = 2;
                if (part.name == "upper_arm.L") shoulder = part;
                else if (part.name == "forearm.L") elbow = part;
                else if (part.name == "hand.L") wrist = part;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                surfaces = new Material[3];
                var colors = new[] { new Color(.045f, .065f, .075f), new Color(.16f, .21f, .23f), new Color(.075f, .095f, .105f) };
                var names = new[] { "Glove", "Sleeve", "Cuff" };
                var smoothness = new[] { .28f, .1f, .37f };
                for (int i = 0; i < surfaces.Length; i++)
                {
                    surfaces[i] = new Material(shader) { name = "First person " + names[i] };
                    surfaces[i].SetColor("_BaseColor", colors[i]);
                    surfaces[i].SetFloat("_Smoothness", smoothness[i]);
                }
            }
            foreach (var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (surfaces != null)
                {
                    // FBX import can reorder slots; resolve by authored material name.
                    var slots = skin.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string name = slots[i] != null ? slots[i].name : "";
                        slots[i] = surfaces[name.Contains("Sleeve") ? 1 : name.Contains("Cuff") ? 2 : 0];
                    }
                    skin.sharedMaterials = slots;
                }
                skin.shadowCastingMode = ShadowCastingMode.Off; skin.receiveShadows = false;
                skin.updateWhenOffscreen = true;
            }
            if (!IsReady) return;
            elbowRest = elbow.localPosition;
            wristRest = wrist.localPosition;
        }

        public void ApplyPose(float progress, Transform magazine, Vector3 magazineRestPosition)
        {
            if (!IsReady) return;
            elbow.localPosition = elbowRest;
            wrist.localPosition = wristRest;
            float reach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .2f, progress)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, .97f, progress)));
            if (reach <= 0) return;
            Vector3 offset = rig.TransformVector(new Vector3(-.025f, -.015f, -.015f)) * reach;
            if (magazine != null && magazine.parent != null)
                offset += (magazine.position - magazine.parent.TransformPoint(magazineRestPosition)) * reach;
            // Move the forearm and its hand together. Rotating this imported, nonuniformly scaled
            // bone chain creates shear; a rigid forearm translation preserves the authored grip.
            elbow.localPosition = elbowRest + elbow.parent.InverseTransformVector(offset);
            // Supports older exports with a separate hand-control parent as well.
            if (!wrist.IsChildOf(elbow)) wrist.position += offset;
        }
        private void OnDestroy()
        {
            if (surfaces != null)
                foreach (var material in surfaces)
                    if (material != null) Destroy(material);
        }
    }
}
