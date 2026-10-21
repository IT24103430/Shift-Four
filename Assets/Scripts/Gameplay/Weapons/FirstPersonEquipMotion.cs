using UnityEngine;

namespace ShiftFour
{
    // Artist-owned settings on a viewmodel prefab. The mount owns time and root transforms.
    [DisallowMultipleComponent]
    public sealed class FirstPersonEquipMotion : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float equipDuration = .38f;
        [SerializeField, Min(.01f)] private float unequipDuration = .26f;
        [SerializeField] private Vector3 loweredPosition = new Vector3(.015f, -.24f, .025f);
        [SerializeField] private Vector3 loweredRotation = new Vector3(16, -6, 8);
        [SerializeField] private bool blockFiringDuringEquip = false;
        public float EquipDuration => Seconds(equipDuration, .38f);
        public float UnequipDuration => Seconds(unequipDuration, .26f);
        public Vector3 LoweredPosition => loweredPosition;
        public Vector3 LoweredRotation => loweredRotation;
        public bool BlockFiringDuringEquip => blockFiringDuringEquip;
        private static float Seconds(float value, float fallback) => float.IsFinite(value) ? Mathf.Max(.01f, value) : fallback;
    }
}
