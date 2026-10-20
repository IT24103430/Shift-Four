using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftFour
{
    /// <summary>Small collider-free gimbal. Its muzzle is also the gameplay shot origin.</summary>
    public sealed class DroneWeaponMount : System.IDisposable
    {
        private readonly Transform root, mount;
        private readonly Vector3 pivotOffset;
        private readonly Material material;
        private readonly float length;
        public Vector3 PivotPosition => root.TransformPoint(pivotOffset);
        public Vector3 MuzzlePosition => mount.position + mount.forward * length;
        public Vector3 Direction => mount.forward;
        public Transform Transform => mount;

        public DroneWeaponMount(Transform visual, Transform owner, DroneBody body)
        {
            root = owner;
            float scale = visual.localScale.x;
            length = .28f * scale;
            pivotOffset = new Vector3(0f, body != null ? body.EnvelopeCenter.y - .24f * scale : visual.localPosition.y,
                .65f * scale);
            mount = new GameObject("Drone weapon gimbal").transform;
            mount.SetParent(visual, false);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "Drone barrel steel (runtime)", hideFlags = HideFlags.DontSave };
            material.SetColor("_BaseColor", new Color(.12f, .16f, .19f));
            material.SetFloat("_Metallic", .7f);
            material.SetFloat("_Smoothness", .4f);
            Part("Weapon housing", PrimitiveType.Cube, new Vector3(0, 0, -.025f), new Vector3(.15f, .12f, .15f));
            Part("Short barrel", PrimitiveType.Cylinder, new Vector3(0, 0, .125f), new Vector3(.075f, .125f, .075f));
            Part("Muzzle collar", PrimitiveType.Cylinder, new Vector3(0, 0, .265f), new Vector3(.10f, .015f, .10f));
            AimAt(null);
        }

        private void Part(string name, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(mount, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (type == PrimitiveType.Cylinder) part.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        public void AimAt(Vector3? target)
        {
            Vector3 pivot = PivotPosition;
            Vector3 direction = target.HasValue ? target.Value - pivot : root.forward;
            if (direction.sqrMagnitude < .0001f) direction = root.forward;
            mount.SetPositionAndRotation(pivot, Quaternion.LookRotation(direction, root.up));
            // Parent model scale already supplies the requested 10% size reduction.
        }

        public void Dispose()
        {
            if (mount != null) Object.Destroy(mount.gameObject);
            if (material != null) Object.Destroy(material);
        }
    }
}
