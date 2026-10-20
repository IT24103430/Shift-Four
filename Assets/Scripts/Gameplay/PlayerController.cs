using UnityEngine;

namespace ShiftFour
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField, Min(0.1f)] private float eyeHeight = 1.3f;
        [SerializeField, Range(0.01f, 0.1f)] private float nearClip = 0.05f;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float gunRange = 30f;
        [SerializeField] private float gunDamage = 25f;
        [SerializeField] private float fireInterval = 0.24f;
        [SerializeField] private MovementNoiseCadence movementNoise = new MovementNoiseCadence();

        private CharacterController controller;
        private Health health;
        private float pitch;
        private float verticalSpeed;
        private float nextShot;
        private bool suitableGround;
        public string InteractionPrompt { get; private set; } = string.Empty;
        public Vector3 EyePosition => view != null ? view.transform.position : transform.TransformPoint(EyeLocalPosition);

        public void Configure(Camera playerCamera)
        {
            view = playerCamera;
            ApplyCameraPose();
        }

        // Eye height is measured from the capsule bottom, not from an arbitrary root pivot.
        private Vector3 EyeLocalPosition
        {
            get
            {
                if (controller == null) controller = GetComponent<CharacterController>();
                float half = controller.height * .5f;
                float margin = Mathf.Min(controller.radius, half);
                float y = Mathf.Clamp(eyeHeight - half, -half + margin, half - margin);
                return controller.center + Vector3.up * y;
            }
        }

        public void ApplyCameraPose()
        {
            if (view == null) return;
            view.transform.position = transform.TransformPoint(EyeLocalPosition);
            // Keep the complete near-plane footprint inside the collision capsule, even at steep pitch.
            float halfFov = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float diagonal = Mathf.Sqrt(1f + halfFov * halfFov * (1f + view.aspect * view.aspect));
            float clearance = Mathf.Max(.01f, controller.radius - controller.skinWidth);
            view.nearClipPlane = Mathf.Clamp(nearClip, .01f, Mathf.Min(.1f, clearance / diagonal));
        }

        private void OnValidate()
        {
            movementNoise ??= new MovementNoiseCadence();
            movementNoise.Validate();
            ApplyCameraPose();
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            movementNoise ??= new MovementNoiseCadence();
            movementNoise.Validate();
            ApplyCameraPose();
        }

        private void Start() { ApplyCameraPose(); LockCursor(); }

        private void LateUpdate() => ApplyCameraPose();

        private void OnDisable() => movementNoise?.Reset();

        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.Ended) { movementNoise.Reset(); return; }
            if (!health.IsAlive || view == null) { movementNoise.Reset(); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) LockCursor();
                movementNoise.Reset();
                return;
            }

            float yaw = Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
            transform.Rotate(Vector3.up * yaw);
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector3 move = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");
            if (move.sqrMagnitude > 1f) move.Normalize();
            MovePlayer(move, Time.deltaTime);

            Ray ray = new Ray(view.transform.position, view.transform.forward);
            IInteractable target = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                foreach (MonoBehaviour component in hit.collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IInteractable interactable) { target = interactable; break; }
            }
            InteractionPrompt = target?.Prompt ?? string.Empty;
            if (target != null && Input.GetKeyDown(KeyCode.E)) target.Interact();
            if (Input.GetMouseButton(0) && Time.time >= nextShot) Shoot(ray);
        }

        private void MovePlayer(Vector3 move, float deltaTime)
        {
            bool wasGrounded = controller.isGrounded;
            Vector3 before = transform.position;
            suitableGround = false;
            verticalSpeed = wasGrounded ? -1f : verticalSpeed - 20f * deltaTime;
            CollisionFlags flags = controller.Move((move * walkSpeed + Vector3.up * verticalSpeed) * deltaTime);
            bool grounded = wasGrounded && (flags & CollisionFlags.Below) != 0 && suitableGround;
            if (movementNoise.Advance(transform.position - before, grounded, move.sqrMagnitude > .0001f, false, deltaTime))
                SoundEvents.ReportNoise(transform.position, NoiseCategory.Footstep, movementNoise.Intensity(false), this);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Vector3.Dot(hit.normal, Vector3.up) >= Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad))
                suitableGround = true;
        }

        private void Shoot(Ray ray)
        {
            nextShot = Time.time + fireInterval;
            SoundEvents.ReportShot(transform.position);
            if (!Physics.Raycast(ray, out RaycastHit hit, gunRange, ~0, QueryTriggerInteraction.Ignore)) return;
            Health victim = hit.collider.GetComponentInParent<Health>();
            if (victim != null && victim != health) victim.ApplyDamage(gunDamage, this, "Player hitscan shot");
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
