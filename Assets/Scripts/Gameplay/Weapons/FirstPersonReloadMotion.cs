using UnityEngine;

namespace ShiftFour
{
    // Authored presentation curves, sampled only from gameplay-owned normalized reload progress.
    [DisallowMultipleComponent]
    public sealed class FirstPersonReloadMotion : MonoBehaviour
    {
        [SerializeField] private Transform magazineGripTarget;
        [SerializeField] private AnimationCurve access = Curve(0, 0, .16f, 1, .84f, 1, 1, 0);
        [SerializeField] private AnimationCurve handContact = Curve(0, 0, .10f, 0, .26f, 1, .85f, 1, .98f, 0, 1, 0);
        [SerializeField] private AnimationCurve withdrawal = Curve(0, 0, .26f, 0, .44f, 1, .60f, 1, .82f, 0, 1, 0);
        [SerializeField] private AnimationCurve exchange = Curve(0, 0, .43f, 0, .52f, 1, .60f, 1, .73f, 0, 1, 0);
        [SerializeField] private AnimationCurve seat = Curve(0, 0, .80f, 0, .84f, 1, .88f, 0, 1, 0);
        [SerializeField, Tooltip("Pistol access offset in metres.")] private Vector3 accessPosition = new Vector3(-.015f, .03f, .01f);
        [SerializeField, Tooltip("Pistol access pitch/yaw/roll in degrees.")] private Vector3 accessRotation = new Vector3(6, -8, -16);
        [SerializeField, Min(.02f)] private float withdrawalDistance = .16f;
        [SerializeField, Min(0), Tooltip("Rearward travel follows the source magazine's slanted insertion axis, in metres.")]
        private float withdrawalRearward = .067f;
        [SerializeField] private Vector3 exchangeOffset = new Vector3(-.025f, -.04f, -.015f);
        [SerializeField] private Vector3 exchangeRotation = new Vector3(3, 0, -8);
        [SerializeField, Range(0, .005f)] private float seatDistance = .002f;
        [SerializeField, Range(0, .08f)] private float handClearance = .05f;
        [SerializeField, Range(0, 80), Tooltip("Permitted forearm pronation during the magazine grip; wrist limit remains unchanged.")]
        private float forearmTwistLimit = 75;

        public Transform MagazineGripTarget => magazineGripTarget;
        public float ContactWeight { get; private set; }
        public float ForearmTwistLimit => forearmTwistLimit;
        public float HandClearance => handClearance;
        public Vector3 PositionOffset { get; private set; }
        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
        public Vector3 MagazineOffset { get; private set; }
        public Quaternion MagazineRotation { get; private set; } = Quaternion.identity;

        public void Evaluate(float progress)
        {
            float p = float.IsFinite(progress) ? Mathf.Clamp01(progress) : 0;
            float pose = Sample(access, p), swap = Sample(exchange, p);
            ContactWeight = Sample(handContact, p);
            PositionOffset = accessPosition * pose; RotationOffset = Quaternion.Euler(accessRotation * pose);
            MagazineOffset = new Vector3(0, -withdrawalDistance, -withdrawalRearward) * Sample(withdrawal, p) + Vector3.down * (seatDistance * Sample(seat, p)) + exchangeOffset * swap;
            MagazineRotation = Quaternion.Euler(exchangeRotation * swap);
        }

        private void OnDisable() => Evaluate(0);
        private static float Sample(AnimationCurve curve, float p) => curve != null ? Mathf.Clamp01(curve.Evaluate(p)) : 0;
        private static AnimationCurve Curve(params float[] pairs)
        {
            var keys = new Keyframe[pairs.Length / 2];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1], 0, 0);
            return new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        }
    }
}
