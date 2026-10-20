using UnityEngine;

namespace ShiftFour
{
    // Camera-only feedback. The live CharacterController and its world position never move.
    [DisallowMultipleComponent]
    public sealed class PlayerDeathPresentation : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float duration = .65f;
        [SerializeField, Range(0, .8f)] private float dropDistance = .6f;
        private PlayerController owner;
        private Health health;
        private Camera camera;
        private Quaternion restRotation;
        private float startedAt;
        private bool bound;
        private bool started;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        public bool IsActive { get; private set; }
        public float Progress => IsActive ? Mathf.Clamp01((Time.unscaledTime - startedAt) / Mathf.Max(.1f, duration)) : 0;

        public void Initialize(PlayerController actor, Health actorHealth)
        {
            if (owner == actor && health == actorHealth) return;
            Unbind(); Restore(); owner = actor; health = actorHealth;
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() => Bind();
        private void Start()
        {
            started = true;
            if (health != null && !health.IsAlive) OnDied(health);
        }
        private void OnDisable() { Unbind(); Restore(); }
        private void OnDestroy() => Unbind();
        private void Bind()
        {
            if (bound || owner == null || health == null) return;
            health.Died += OnDied; bound = true;
            // Health.Awake may not have initialized Current when PlayerController installs us.
            if (started && !health.IsAlive) OnDied(health);
        }
        private void Unbind()
        {
            if (bound && health != null) health.Died -= OnDied;
            bound = false;
        }
        private void OnDied(Health _)
        {
            if (IsActive) return;
            camera = owner.ViewCamera;
            restRotation = camera != null ? camera.transform.localRotation : Quaternion.identity;
            startedAt = Time.unscaledTime; IsActive = true;
            owner.Weapons?.CancelReload();
        }
        private void Restore()
        {
            if (IsActive && camera != null) camera.transform.localRotation = restRotation;
            IsActive = false;
        }
        public void ApplyPose(Camera view, Vector3 liveEye)
        {
            if (!IsActive || !isActiveAndEnabled || view == null) return;
            if (camera != view) { camera = view; restRotation = view.transform.localRotation; }
            float ease = Mathf.SmoothStep(0, 1, Progress);
            float drop = dropDistance * ease;
            int count = Physics.SphereCastNonAlloc(liveEye, .09f, Vector3.down, hits, drop + .03f,
                WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(owner.transform))
                    drop = Mathf.Min(drop, Mathf.Max(0, hits[i].distance - .03f));
            view.transform.position = liveEye + Vector3.down * drop;
            view.transform.localRotation = restRotation * Quaternion.Euler(18 * ease, 0, -7 * ease);
        }
    }
}
