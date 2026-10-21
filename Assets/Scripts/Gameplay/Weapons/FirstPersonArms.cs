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
        private FirstPersonPistolGrip grip;
        public bool IsReady => rig != null && shoulder != null && elbow != null && wrist != null;

        public void Initialize(Transform pivot)
        {
            if (rig != null || pivot == null) return;
            var ownedPrefab = Resources.Load<GameObject>("Weapons/FirstPersonArmsRig");
            var prefab = ownedPrefab != null ? ownedPrefab : Resources.Load<GameObject>("Weapons/FirstPersonArms");
            if (prefab == null) return;
            rig = Instantiate(prefab, pivot, false).transform;
            rig.name = "FirstPersonArms";
            foreach (var part in rig.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = 2;
                if (ownedPrefab == null)
                {
                    if (part.name == "upper_arm.L") shoulder = part;
                    else if (part.name == "forearm.L") elbow = part;
                    else if (part.name == "hand.L") wrist = part;
                }
            }
            if (ownedPrefab != null)
            {
                var bindings = rig.GetComponent<FirstPersonArmsRig>();
                if (bindings == null || !bindings.IsValid)
                {
                    Debug.LogError("The first-person arms prefab has invalid rig bindings.", this);
                    return;
                }
                shoulder = bindings.LeftUpperArm;
                elbow = bindings.LeftForearm;
                wrist = bindings.LeftHand;
                grip = rig.GetComponent<FirstPersonPistolGrip>();
                if (grip != null && grip.Initialize())
                {
                    grip.ApplyPose(0, null, Vector3.zero);
                }
            }
            // Both the owned prefab and legacy import retain their authored shared materials.
            // This component never creates, replaces, edits, or destroys a material.
            foreach (var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
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
            if (grip != null)
            {
                if (grip.isActiveAndEnabled) grip.ApplyPose(progress, magazine, magazineRestPosition);
                return;
            }
            elbow.localPosition = elbowRest;
            wrist.localPosition = wristRest;
            float reach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .2f, progress)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, .97f, progress)));
            if (reach <= 0) return;
            Vector3 offset = rig.TransformVector(new Vector3(-.025f, -.015f, -.015f)) * reach;
            if (magazine != null && magazine.parent != null)
                offset += (magazine.position - magazine.parent.TransformPoint(magazineRestPosition)) * reach;
            // Retain the existing procedural reload: translate the forearm and hand together.
            // Skeletal animation authoring is independent of this rig/asset upgrade.
            elbow.localPosition = elbowRest + elbow.parent.InverseTransformVector(offset);
            // Supports older exports with a separate hand-control parent as well.
            if (!wrist.IsChildOf(elbow)) wrist.position += offset;
        }

        public void ApplyMagazinePose(Transform magazineGrip, float contactWeight, float forearmTwistLimit, float handClearance)
        {
            if (!IsReady) return;
            if (grip != null && grip.isActiveAndEnabled) grip.ApplyMagazinePose(magazineGrip, contactWeight, forearmTwistLimit, handClearance);
            else ApplyPose(0, null, Vector3.zero);
        }
    }
}
