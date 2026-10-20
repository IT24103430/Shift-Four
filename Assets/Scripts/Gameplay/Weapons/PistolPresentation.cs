using UnityEngine;

namespace ShiftFour
{
    [RequireComponent(typeof(FirstPersonViewmodel)), DisallowMultipleComponent]
    public sealed class PistolPresentation : MonoBehaviour
    {
        [SerializeField] private Transform recoilPivot;
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private Renderer muzzleFlash;
        [SerializeField, Min(.001f)] private float flashDuration = .035f;
        [SerializeField, Min(0f)] private float recoilDistance = .012f;
        [SerializeField, Min(0f)] private float recoilDegrees = 2f;
        [SerializeField, Min(.1f)] private float recovery = 22f;
        private FirstPersonViewmodel model;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float kick;
        private float flashUntil;

        public Transform MuzzlePoint => muzzlePoint;
        public Transform RecoilPivot => recoilPivot;
        public Renderer MuzzleFlash => muzzleFlash;

        private void Awake()
        {
            model = GetComponent<FirstPersonViewmodel>();
            if (recoilPivot != null) { restPosition = recoilPivot.localPosition; restRotation = recoilPivot.localRotation; }
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        private void OnEnable() => model.ShotReceived += OnShot;
        private void OnDisable()
        {
            if (model != null) model.ShotReceived -= OnShot;
            kick = 0f; ApplyPose();
            if (muzzleFlash != null) muzzleFlash.enabled = false;
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

        private void ApplyPose()
        {
            if (recoilPivot == null) return;
            recoilPivot.localPosition = restPosition + Vector3.back * (recoilDistance * kick);
            recoilPivot.localRotation = restRotation * Quaternion.Euler(-recoilDegrees * kick, 0f, 0f);
        }
    }
}
