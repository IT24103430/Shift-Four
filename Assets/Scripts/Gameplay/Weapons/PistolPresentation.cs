using UnityEngine;

namespace ShiftFour
{
    [RequireComponent(typeof(FirstPersonViewmodel)), DisallowMultipleComponent]
    public sealed class PistolPresentation : MonoBehaviour
    {
        [SerializeField] private Transform recoilPivot;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Renderer muzzleFlash;
        [SerializeField] private Transform magazinePart;
        [SerializeField, Min(.001f)] private float flashDuration = .035f;
        [SerializeField, Min(0f)] private float recoilDistance = .012f;
        [SerializeField, Min(0f)] private float recoilDegrees = 2f;
        [SerializeField, Min(.1f)] private float recovery = 22f;
        [SerializeField, Min(0f), Tooltip("Small upward presentation travel in metres at full muzzle rise.")]
        private float recoilLift = .0006f;
        [SerializeField, Range(0, .3f), Tooltip("Restrained hand/weapon roll in degrees at full muzzle rise.")]
        private float recoilRoll = .08f;
        private FirstPersonViewmodel model;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private readonly FirstPersonRecoilMotion recoil = new FirstPersonRecoilMotion();
        private float flashUntil;
        private Vector3 magazineRestPosition;
        private Quaternion magazineRestRotation;
        private FirstPersonArms arms;
        private FirstPersonIdleMotion idle;
        private FirstPersonMovementMotion movement;
        private FirstPersonReloadMotion reload;

        public Transform MuzzlePoint => muzzlePoint;
        public Transform RecoilPivot => recoilPivot;
        public Renderer MuzzleFlash => muzzleFlash;
        public Transform MagazinePart => magazinePart;
        public float VisualRecoilStrength => recoil.Strength;
        public double RecoilSampleTime => recoil.SampleTime;
        public Vector3 RecoilPositionOffset => Vector3.back * (recoilDistance * recoil.BackwardWeight) + Vector3.up * (recoilLift * recoil.MuzzleWeight);
        public Quaternion RecoilRotationOffset => Quaternion.Euler(-recoilDegrees * recoil.MuzzleWeight, 0, recoilRoll * recoil.MuzzleWeight);

        private void Awake()
        {
            model = GetComponent<FirstPersonViewmodel>();
            idle = GetComponent<FirstPersonIdleMotion>();
            if (idle == null) idle = gameObject.AddComponent<FirstPersonIdleMotion>();
            movement = GetComponent<FirstPersonMovementMotion>();
            if (movement == null) movement = gameObject.AddComponent<FirstPersonMovementMotion>();
            movement.Initialize();
            reload = GetComponent<FirstPersonReloadMotion>();
            if (recoilPivot != null) { restPosition = recoilPivot.localPosition; restRotation = recoilPivot.localRotation; }
            if (magazinePart == null)
                foreach (var part in GetComponentsInChildren<Transform>(true))
                    if (part.name == "FNFiveSeven_Magazine") { magazinePart = part; break; }
            if (magazinePart != null) { magazineRestPosition = magazinePart.localPosition; magazineRestRotation = magazinePart.localRotation; }
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            arms = GetComponent<FirstPersonArms>();
            if (arms == null) arms = gameObject.AddComponent<FirstPersonArms>();
            arms.Initialize(recoilPivot);
        }

        private void OnEnable()
        {
            model.ShotReceived += OnShot;
            model.ReloadChanged += OnReload;
        }
        private void OnDisable()
        {
            if (model != null) { model.ShotReceived -= OnShot; model.ReloadChanged -= OnReload; }
            recoil.Reset();
            if (idle != null) idle.ResetMotion();
            if (movement != null) movement.ResetMotion();
            ApplyPose(true);
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void OnReload(WeaponReloadState state)
        {
            recoil.Reset();
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            if (state == WeaponReloadState.Idle) ApplyPose(true);
        }

        private void OnShot(WeaponShot _)
        {
            recoil.AcceptShot(Time.timeAsDouble, recovery);
            ApplyPose();
            flashUntil = Time.time + flashDuration;
            if (muzzleFlash != null) muzzleFlash.enabled = true;
        }

        private void LateUpdate()
        {
            // The accepted-shot callback and LateUpdate share one scaled timestamp. Never age a new
            // impulse by the whole preceding frame, which would shorten recoil at lower frame rates.
            recoil.Sample(Time.timeAsDouble, recovery);
            bool suppressed = recoil.Strength > .02f || model.ReloadState == WeaponReloadState.Reloading;
            if (movement != null && movement.isActiveAndEnabled) movement.SamplePlayer(Time.deltaTime, suppressed);
            if (idle != null && idle.isActiveAndEnabled) idle.Advance(Time.deltaTime, suppressed);
            ApplyPose();
            if (muzzleFlash != null && Time.time >= flashUntil) muzzleFlash.enabled = false;
        }

        private void ApplyPose(bool reset = false)
        {
            if (recoilPivot == null) return;
            float progress = !reset && model.ReloadState == WeaponReloadState.Reloading ? model.ReloadProgress : 0;
            bool detailed = reload != null && reload.isActiveAndEnabled && reload.MagazineGripTarget != null;
            if (detailed) reload.Evaluate(progress);
            float tilt = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .2f, progress)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1, progress)));
            recoilPivot.localPosition = restPosition + RecoilPositionOffset + (detailed ? reload.PositionOffset : new Vector3(-.025f, .075f, .015f) * tilt);
            recoilPivot.localRotation = restRotation * (detailed ? reload.RotationOffset : Quaternion.Euler(12 * tilt, -15 * tilt, -28 * tilt)) * RecoilRotationOffset;
            if (idle != null && idle.isActiveAndEnabled)
            {
                float idleWeight = movement != null && movement.isActiveAndEnabled ? movement.IdleWeight : 1;
                recoilPivot.localPosition += idle.PositionOffset * idleWeight;
                recoilPivot.localRotation *= Quaternion.SlerpUnclamped(Quaternion.identity, idle.RotationOffset, idleWeight);
            }
            if (movement != null && movement.isActiveAndEnabled)
            {
                recoilPivot.localPosition += movement.PositionOffset;
                recoilPivot.localRotation *= movement.RotationOffset;
            }
            if (magazinePart != null && magazinePart.parent != null)
            {
                float withdrawal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .35f, progress)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, .85f, progress)));
                magazinePart.localPosition = magazineRestPosition + magazinePart.parent.InverseTransformVector(
                    recoilPivot.TransformVector(detailed ? reload.MagazineOffset : Vector3.down * (.11f * withdrawal)));
                magazinePart.localRotation = magazineRestRotation * (detailed ? reload.MagazineRotation : Quaternion.identity);
            }
            if (arms != null)
            {
                if (detailed) arms.ApplyMagazinePose(reload.MagazineGripTarget, reload.ContactWeight, reload.ForearmTwistLimit, reload.HandClearance);
                else arms.ApplyPose(progress, magazinePart, magazineRestPosition);
            }
        }
    }
}
