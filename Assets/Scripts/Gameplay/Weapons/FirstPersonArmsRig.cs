using UnityEngine;

namespace ShiftFour
{
    // References belong to the project-owned rig prefab, never to the imported source model.
    [DisallowMultipleComponent]
    public sealed class FirstPersonArmsRig : MonoBehaviour
    {
        [SerializeField] private Transform leftUpperArm;
        [SerializeField] private Transform leftForearm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightForearm;
        [SerializeField] private Transform rightHand;
        [SerializeField] private SkinnedMeshRenderer skin;

        public Transform LeftUpperArm => leftUpperArm;
        public Transform LeftForearm => leftForearm;
        public Transform LeftHand => leftHand;
        public Transform RightUpperArm => rightUpperArm;
        public Transform RightForearm => rightForearm;
        public Transform RightHand => rightHand;
        public SkinnedMeshRenderer Skin => skin;
        public bool IsValid => skin != null && skin.sharedMesh != null &&
            leftUpperArm != null && leftForearm != null && leftHand != null &&
            rightUpperArm != null && rightForearm != null && rightHand != null &&
            leftForearm.IsChildOf(leftUpperArm) && leftHand.IsChildOf(leftForearm) &&
            rightForearm.IsChildOf(rightUpperArm) && rightHand.IsChildOf(rightForearm) &&
            leftUpperArm.IsChildOf(transform) && rightUpperArm.IsChildOf(transform);
    }
}
