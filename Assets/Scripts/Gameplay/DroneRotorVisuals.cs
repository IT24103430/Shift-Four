using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace ShiftFour
{
    /// <summary>
    /// Sole presentation owner: model-local pose, independent rotors and property-block feedback.
    /// Runs after DroneBody has accepted the physical movement. Never writes the agent root.
    /// </summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class DroneRotorVisuals : MonoBehaviour
    {
        private static readonly ProfilerMarker AnimationMarker = new ProfilerMarker("ShiftFour.DroneAnimation");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        [SerializeField, Min(0f)] private float rotorRpm = 390f;
        [SerializeField] private bool alternateDirection = true;
        [SerializeField, Range(0f, .02f)] private float hoverAmplitude = .02f;
        [SerializeField, Range(0f, 4f)] private float maximumBank = 4f;
        [SerializeField, Range(.08f, .15f)] private float muzzleFlashDuration = .11f;
        [SerializeField, Range(.04f, .25f)] private float tracerDuration = .13f;
        [SerializeField, Range(.02f, .25f)] private float criticalFailureDuration = .12f;
        [SerializeField, Range(10f, 100f)] private float wreckTumbleSpeed = 65f;
        private Health health;
        private DroneBrain brain;
        private NavMeshAgent agent;
        private DroneMotor motor;
        private Transform root;
        private Vector3 restPosition, previousPosition;
        private Quaternion restRotation;
        private float previousYaw;
        private bool bound, sampled;
        private bool dying;
        private float failureAge;
        private DroneWeaponEffects effects;
        private DroneWreckMotion wreck;
        private Vector3 previousTint = Vector3.negativeInfinity;
        private Color previousAlert;
        private Vector3 previousMovementVelocity;
        private bool sampledAcceleration;
        private DroneAnimationPose pose = new DroneAnimationPose();
        private readonly List<Transform> pivots = new List<Transform>();
        private readonly List<Quaternion> pivotRest = new List<Quaternion>();
        private readonly List<TintSlot> tints = new List<TintSlot>();
        public DroneVisualState State => wreck != null ? wreck.State : dying ? DroneVisualState.CriticalFailure : pose.State;
        public DroneWeaponEffects Effects => effects;
        public DroneWreckMotion Wreck => wreck;
        public int DestructionCount { get; private set; }
        public Vector3 MovementVelocity { get; private set; }
        public float MovementSpeed => MovementVelocity.magnitude;
        public float AlertStrength => pose.AlertBlend;
        public float ScanYaw => pose.ScanYaw;
        public DroneAwarenessState AwarenessState => pose.AwarenessState;

        private sealed class TintSlot
        {
            public Renderer Renderer;
            public int Index, Property;
            public Color Color;
            public bool HasEmission;
            public Color Emission;
            public MaterialPropertyBlock Original, Animated;
        }

        private void Awake()
        {
            health = GetComponentInParent<Health>();
            brain = GetComponentInParent<DroneBrain>();
            agent = GetComponentInParent<NavMeshAgent>();
            motor = GetComponentInParent<DroneMotor>();
            root = agent != null ? agent.transform : health != null ? health.transform : transform.parent;
            // A misplaced component must never animate a navigation root or a collider root.
            if (root == null || root == transform) { enabled = false; return; }
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("Rotor", System.StringComparison.Ordinal) || filter.sharedMesh == null) continue;
                Transform mesh = filter.transform;
                Transform pivot = mesh.parent;
                if (pivot == null || pivot.parent != transform || pivot.name != "Animated " + filter.name)
                {
                    Vector3 center = mesh.TransformPoint(filter.sharedMesh.bounds.center);
                    pivot = new GameObject("Animated " + filter.name).transform;
                    pivot.SetParent(transform, false);
                    pivot.position = center;
                    mesh.SetParent(pivot, true);
                }
                pivots.Add(pivot);
                pivotRest.Add(pivot.localRotation);
            }
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials; // Once at binding; never instantiate materials.
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    int property = material.HasProperty(BaseColor) ? BaseColor : LegacyColor;
                    if (!material.HasProperty(property)) continue;
                    var original = new MaterialPropertyBlock();
                    var animated = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original, i);
                    renderer.GetPropertyBlock(animated, i);
                    tints.Add(new TintSlot { Renderer = renderer, Index = i, Property = property,
                        Color = original.HasColor(property) ? original.GetColor(property) : material.GetColor(property),
                        HasEmission = material.HasProperty(EmissionColor),
                        Emission = original.HasColor(EmissionColor) ? original.GetColor(EmissionColor) :
                            material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black,
                        Original = original, Animated = animated });
                }
            }
            effects = new DroneWeaponEffects(root);
        }

        private void OnEnable()
        {
            if (root == null || root == transform) return;
            sampled = false;
            sampledAcceleration = false;
            if (!dying) pose = new DroneAnimationPose();
            if (health != null && !health.IsAlive) BeginDeath();
            previousTint = Vector3.negativeInfinity;
            if (bound) return;
            if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; }
            if (brain != null) brain.ShotFired += OnFired;
            bound = true;
        }

        private void OnDisable()
        {
            if (bound)
            {
                if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
                if (brain != null) brain.ShotFired -= OnFired;
                bound = false;
            }
            if (root == null || root == transform) return;
            effects?.Clear();
            if (dying) return; // Never move a falling/settled wreck back to its flying rest pose.
            transform.SetLocalPositionAndRotation(restPosition, restRotation);
            for (int i = 0; i < pivots.Count; i++) if (pivots[i] != null) pivots[i].localRotation = pivotRest[i];
            foreach (TintSlot tint in tints)
                if (tint.Renderer != null) tint.Renderer.SetPropertyBlock(tint.Original.isEmpty ? null : tint.Original, tint.Index);
        }

        private void OnFired(DroneBrain source)
        {
            if (dying || (health != null && !health.IsAlive)) return;
            pose.Fire();
            effects.Fire(source.LastShotOrigin, source.LastShotHit, muzzleFlashDuration, tracerDuration);
        }
        private void OnDamaged(DamageRecord hit) { if (hit.Fatal) BeginDeath(); else pose.Hit(false); }
        private void OnDied(Health source) => BeginDeath();
        private void BeginDeath()
        {
            if (dying) return;
            dying = true; DestructionCount++;
            pose.Shutdown(); effects?.CancelShot();
        }

        public void GroundImpact(Vector3 point, Vector3 normal)
        { if (isActiveAndEnabled) effects?.Impact(point, normal); }

        public void WreckSettled() => pose.StopRotors();

        private void OnDestroy() => effects?.Dispose();

        private void LateUpdate()
        {
            using var sample = AnimationMarker.Auto();
            float dt = Time.deltaTime;
            if (dt <= 0f || root == null) return;
            Vector3 position = root.position;
            float yaw = root.eulerAngles.y;
            Vector3 velocity = sampled ? (position - previousPosition) / dt : Vector3.zero;
            float yawRate = sampled ? Mathf.DeltaAngle(previousYaw, yaw) / dt : 0f;
            previousPosition = position;
            previousYaw = yaw;
            sampled = true;
            velocity.y = 0f;
            // Teleports/restarts are not flight acceleration. Sample again on the next frame.
            bool teleported = velocity.sqrMagnitude > 400f;
            if (teleported) { velocity = Vector3.zero; yawRate = 0f; }
            if (!dying && motor != null && motor.isActiveAndEnabled) velocity = motor.ActualVelocity;
            MovementVelocity = velocity;
            // A stable reference preserves acceleration/braking cues as the speed limit changes.
            float speed = motor != null && motor.HasMovementProfile ? motor.AnimationReferenceSpeed :
                agent != null ? Mathf.Max(.1f, agent.speed) : 3.5f;
            if (health != null && !health.IsAlive) BeginDeath();
            float forwardAcceleration = sampledAcceleration && !teleported && dt <= .25f ?
                root.InverseTransformDirection((velocity - previousMovementVelocity) / dt).z / speed : 0f;
            previousMovementVelocity = velocity;
            sampledAcceleration = !teleported;
            pose.SetAcceleration(forwardAcceleration);
            pose.SetAwareness(brain != null && brain.Awareness != null && brain.Awareness.IsEnabled ?
                brain.Awareness.State : DroneAwarenessState.Patrol);
            pose.HoverAmplitude = Mathf.Clamp(hoverAmplitude, 0f, .02f);
            pose.BankDegrees = Mathf.Clamp(maximumBank, 0f, 4f);
            pose.Step(dt, root.InverseTransformDirection(velocity) / speed, yawRate, health == null || health.IsAlive);
            if (dying)
            {
                failureAge += dt;
                // Defer construction until after all Health.Died handlers stop navigation/disable hitboxes.
                if (wreck == null) wreck = DroneWreckMotion.Create(this, root, criticalFailureDuration, wreckTumbleSpeed);
            }
            else
            {
                Quaternion tilt = Quaternion.RotateTowards(Quaternion.identity, Quaternion.Euler(pose.Pitch, pose.ScanYaw, pose.Bank), 4f);
                transform.SetLocalPositionAndRotation(restPosition + Vector3.up * pose.Hover, tilt * restRotation);
            }
            effects?.Tick(dt);
            float degrees = rotorRpm * 6f * pose.RotorTravel;
            if (State == DroneVisualState.Disabled) { pose.StopRotors(); degrees = 0f; }
            for (int i = 0; i < pivots.Count; i++)
                pivots[i].Rotate(Vector3.up, degrees * (alternateDirection && i % 2 == 1 ? -1f : 1f), Space.Self);
            float failureFlash = dying && failureAge < criticalFailureDuration ?
                .6f + .4f * Mathf.Sin(failureAge * 100f) : 0f;
            var tintState = new Vector3(pose.FireFlash + failureFlash, pose.HitFlash, pose.ShutdownBlend);
            float alertWeight = dying ? 0f : pose.AlertBlend * (1f - Mathf.Max(pose.FireFlash, pose.HitFlash));
            Color alert = alertWeight > 0f ? pose.AlertColor : Color.clear;
            alert.a = alertWeight;
            if (tintState == previousTint && alert == previousAlert) return;
            previousTint = tintState;
            previousAlert = alert;
            foreach (TintSlot tint in tints)
            {
                Color color = Color.Lerp(tint.Color, pose.AlertColor, alertWeight);
                color = Color.Lerp(color, Color.white, pose.FireFlash * .12f);
                color = Color.Lerp(color, new Color(1f, .24f, .12f, tint.Color.a), pose.HitFlash * .8f);
                color = Color.Lerp(color, new Color(.28f, .28f, .28f, tint.Color.a), pose.ShutdownBlend);
                color = Color.Lerp(color, new Color(2f, 2.4f, 3f, tint.Color.a), failureFlash);
                color.a = tint.Color.a;
                tint.Animated.SetColor(tint.Property, color);
                if (tint.HasEmission)
                    tint.Animated.SetColor(EmissionColor, Color.Lerp(tint.Emission, Color.black, pose.ShutdownBlend));
                tint.Renderer.SetPropertyBlock(tint.Animated, tint.Index);
            }
        }
    }
}
