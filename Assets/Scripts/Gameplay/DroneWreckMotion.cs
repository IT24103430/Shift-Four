using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    /// <summary>Physics owns only this separate child rig after navigation has stopped.</summary>
    public sealed class DroneWreckMotion : MonoBehaviour
    {
        public DroneVisualState State { get; private set; } = DroneVisualState.CriticalFailure;
        public int ImpactCount { get; private set; }
        public Vector3 ImpactPoint { get; private set; }
        public Rigidbody Body { get; private set; }
        private DroneRotorVisuals owner;
        private float age, impactAge, lastSupport = float.NegativeInfinity, failureDuration;
        private readonly List<Collider> shapes = new List<Collider>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private PhysicsMaterial surface;

        public static DroneWreckMotion Create(DroneRotorVisuals owner, Transform root, float failureDuration, float tumbleSpeed)
        {
            var go = new GameObject(owner.name + " disabled wreck rig");
            go.transform.SetPositionAndRotation(owner.transform.position, owner.transform.rotation);
            go.transform.SetParent(root, true);
            var wreck = go.AddComponent<DroneWreckMotion>();
            wreck.owner = owner;
            wreck.failureDuration = Mathf.Max(.02f, failureDuration);
            // Brain already disabled the living mesh colliders; keep those off permanently.
            foreach (Collider collider in owner.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            owner.transform.SetParent(go.transform, true);
            wreck.surface = new PhysicsMaterial("Wreck contact (runtime)") { bounciness = .08f, dynamicFriction = .8f, staticFriction = .8f };
            foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                var bounds = new Bounds(go.transform.InverseTransformPoint(filter.transform.TransformPoint(local.center)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(go.transform.InverseTransformPoint(filter.transform.TransformPoint(corner)));
                }
                Collider shape;
                if (filter.name.StartsWith("Rotor", System.StringComparison.Ordinal))
                {
                    // A small convex cylinder includes every rotor phase, without non-convex dynamic meshes.
                    float radius = new Vector2(local.extents.x * filter.transform.lossyScale.x, local.extents.z * filter.transform.lossyScale.z).magnitude;
                    Mesh mesh = Cylinder(bounds.center, radius + .005f, bounds.extents.y + .005f);
                    wreck.generatedMeshes.Add(mesh);
                    var convex = go.AddComponent<MeshCollider>(); convex.sharedMesh = mesh; convex.convex = true;
                    shape = convex;
                }
                else
                {
                    var box = go.AddComponent<BoxCollider>(); box.center = bounds.center;
                    box.size = bounds.size + Vector3.one * .01f; shape = box;
                }
                shape.sharedMaterial = wreck.surface;
                wreck.shapes.Add(shape);
            }
            wreck.Body = go.AddComponent<Rigidbody>();
            wreck.Body.mass = 4f;
            wreck.Body.useGravity = false;
            wreck.Body.linearDamping = .25f; wreck.Body.angularDamping = 1.4f;
            wreck.Body.interpolation = RigidbodyInterpolation.Interpolate;
            wreck.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            wreck.Body.solverIterations = 12; wreck.Body.solverVelocityIterations = 4;
            wreck.Body.maxDepenetrationVelocity = 2f;
            wreck.Body.angularVelocity = root.TransformDirection(new Vector3(.7f, .1f, 1f).normalized * Mathf.Deg2Rad * tumbleSpeed);
            // Falling presentation never pushes the player or living agents. Environment remains solid.
            foreach (Collider other in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (other.attachedRigidbody == wreck.Body) continue;
                if (other.GetComponentInParent<PlayerController>() == null && other.GetComponentInParent<DroneBrain>() == null) continue;
                foreach (Collider shape in wreck.shapes) Physics.IgnoreCollision(shape, other);
            }
            return wreck;
        }

        private static Mesh Cylinder(Vector3 center, float radius, float halfHeight)
        {
            const int sides = 12;
            // Circumscribe the rotor disk; an inscribed polygon would miss rotating blade tips.
            radius /= Mathf.Cos(Mathf.PI / sides);
            var vertices = new Vector3[sides * 2 + 2];
            var triangles = new int[sides * 12];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                vertices[i] = center + radial + Vector3.up * halfHeight;
                vertices[i + sides] = center + radial - Vector3.up * halfHeight;
                int next = (i + 1) % sides, t = i * 12;
                triangles[t] = i; triangles[t + 1] = next; triangles[t + 2] = i + sides;
                triangles[t + 3] = next; triangles[t + 4] = next + sides; triangles[t + 5] = i + sides;
                triangles[t + 6] = sides * 2; triangles[t + 7] = next; triangles[t + 8] = i;
                triangles[t + 9] = sides * 2 + 1; triangles[t + 10] = i + sides; triangles[t + 11] = next + sides;
            }
            vertices[sides * 2] = center + Vector3.up * halfHeight;
            vertices[sides * 2 + 1] = center - Vector3.up * halfHeight;
            var mesh = new Mesh { name = "Runtime convex rotor clearance", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }

        private void Update()
        {
            if (Body == null || State == DroneVisualState.Disabled) return;
            age += Time.deltaTime;
            if (State == DroneVisualState.CriticalFailure && age >= failureDuration)
            { State = DroneVisualState.Falling; Body.useGravity = true; }
            if (State != DroneVisualState.GroundImpact) return;
            impactAge += Time.deltaTime;
            // Sleeping supported bodies no longer receive OnCollisionStay. Do not leave
            // them permanently in GroundImpact just because their last callback aged out.
            bool supported = Body.IsSleeping() || Time.time - lastSupport < .12f;
            if (supported && impactAge > .4f && (Body.IsSleeping() ||
                (Body.linearVelocity.sqrMagnitude < .04f && Body.angularVelocity.sqrMagnitude < .25f) || impactAge > 1.5f))
            {
                Body.linearVelocity = Body.angularVelocity = Vector3.zero;
                Body.isKinematic = true;
                // Stable visual wrecks cannot obstruct consoles, doors, shots or essential routes.
                foreach (Collider shape in shapes) shape.enabled = false;
                State = DroneVisualState.Disabled;
                owner.WreckSettled();
            }
        }

        private void OnCollisionEnter(Collision collision) => Contact(collision);
        private void OnCollisionStay(Collision collision) => Contact(collision);
        private void Contact(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y < .5f) continue;
                lastSupport = Time.time;
                if (ImpactCount != 0) return;
                ImpactCount = 1; ImpactPoint = contact.point;
                State = DroneVisualState.GroundImpact;
                Body.useGravity = true;
                owner.GroundImpact(contact.point, contact.normal);
                return;
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in generatedMeshes) if (mesh != null) Destroy(mesh);
            if (surface != null) Destroy(surface);
        }
    }
}
