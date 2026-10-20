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
        private FirstPersonViewmodel model;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float kick;
        private float flashUntil;
        private Vector3 magazineRestPosition;
        private FirstPersonArms arms;

        public Transform MuzzlePoint => muzzlePoint;
        public Transform RecoilPivot => recoilPivot;
        public Renderer MuzzleFlash => muzzleFlash;
        public Transform MagazinePart => magazinePart;

        private void Awake()
        {
            model = GetComponent<FirstPersonViewmodel>();
            if (recoilPivot != null) { restPosition = recoilPivot.localPosition; restRotation = recoilPivot.localRotation; }
            if (magazinePart == null)
                foreach (var part in GetComponentsInChildren<Transform>(true))
                    if (part.name == "FNFiveSeven_Magazine") { magazinePart = part; break; }
            if (magazinePart != null) magazineRestPosition = magazinePart.localPosition;
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
            kick = 0f; ApplyPose(true);
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void OnReload(WeaponReloadState state)
        {
            kick = 0;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
            if (state == WeaponReloadState.Idle) ApplyPose(true);
        }

        private void OnShot(WeaponShot _)
        {
            kick = Mathf.Min(1.5f, kick + 1f);
            ApplyPose();
            flashUntil = Time.time + flashDuration;
            if (muzzleFlash != null) muzzleFlash.enabled = true;
        }

        private void LateUpdate()
        {
            kick *= Mathf.Exp(-recovery * Time.deltaTime);
            if (kick < .0001f) kick = 0f;
            ApplyPose();
            if (muzzleFlash != null && Time.time >= flashUntil) muzzleFlash.enabled = false;
        }

        private void ApplyPose(bool reset = false)
        {
            if (recoilPivot == null) return;
            float progress = !reset && model.ReloadState == WeaponReloadState.Reloading ? model.ReloadProgress : 0;
            float tilt = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .2f, progress)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1, progress)));
            recoilPivot.localPosition = restPosition + Vector3.back * (recoilDistance * kick) + new Vector3(-.025f, .075f, .015f) * tilt;
            recoilPivot.localRotation = restRotation * Quaternion.Euler(-recoilDegrees * kick + 12 * tilt, -15 * tilt, -28 * tilt);
            if (magazinePart != null && magazinePart.parent != null)
            {
                float withdrawal = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .35f, progress)) *
                    (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, .85f, progress)));
                magazinePart.localPosition = magazineRestPosition + magazinePart.parent.InverseTransformVector(
                    recoilPivot.TransformVector(Vector3.down * (.11f * withdrawal)));
            }
            if (arms != null) arms.ApplyPose(progress, magazinePart, magazineRestPosition);
        }
    }
}
