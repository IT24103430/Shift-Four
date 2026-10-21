using UnityEngine;

namespace ShiftFour
{
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonArms))]
    public sealed class FirstPersonWeaponGripBinding : MonoBehaviour
    {
        [SerializeField] private FirstPersonWeaponGripConfiguration configuration;
        [SerializeField] private Transform posePivot;
        [SerializeField] private GameObject modelGeometry;
        [SerializeField, Tooltip("Preview wrappers are not WeaponDefinitions or playable weapons.")] private bool previewOnly;
        private FirstPersonPistolGrip grip;
        private FirstPersonIdleMotion idle;
        private Vector3 pivotRest;
        private Quaternion pivotRotation;
        public FirstPersonWeaponGripConfiguration Configuration => configuration;
        public Transform PosePivot => posePivot;
        public GameObject ModelGeometry => modelGeometry;
        public bool PreviewOnly => previewOnly;
        public bool IsBound => grip != null && grip.IsReady && Matches(configuration);

        private void Start()
        {
            if (!previewOnly || !Matches(configuration)) return;
            pivotRest = posePivot.localPosition; pivotRotation = posePivot.localRotation;
            idle = GetComponent<FirstPersonIdleMotion>(); GetComponent<FirstPersonArms>().Initialize(posePivot);
        }
        private void LateUpdate()
        {
            if (!previewOnly || !IsBound) return;
            if (idle != null && idle.isActiveAndEnabled)
            {
                idle.Advance(Time.deltaTime, false); posePivot.localPosition = pivotRest + idle.PositionOffset;
                posePivot.localRotation = pivotRotation * idle.RotationOffset;
            }
            grip.ApplyPose(0, null, Vector3.zero);
        }

        private bool Matches(FirstPersonWeaponGripConfiguration data)
        {
            if (data == null || !data.IsPrepared || posePivot == null || !posePivot.IsChildOf(transform) ||
                modelGeometry == null || !modelGeometry.transform.IsChildOf(posePivot)) return false;
            foreach (var filter in modelGeometry.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == data.BodyMesh) return true;
            return false;
        }
        public bool TryApplyConfiguration(FirstPersonWeaponGripConfiguration data)
        {
            if (!Matches(data) || grip == null) return false;
            if (!grip.ApplyConfiguration(data)) return false;
            configuration = data; return true;
        }
        public bool BindOwnedRig(FirstPersonPistolGrip ownedGrip)
        {
            if (ownedGrip == null || !ownedGrip.transform.IsChildOf(posePivot) || !Matches(configuration)) return false;
            grip = ownedGrip;
            if (!TryApplyConfiguration(configuration)) { grip = null; return false; }
            var idle = GetComponent<FirstPersonIdleMotion>(); if (idle != null) idle.Configure(configuration);
            return true;
        }
    }
}
