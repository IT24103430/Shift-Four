using UnityEngine;
using UnityEngine.Rendering;

using Unity.Profiling;

namespace ShiftFour
{
    /// <summary>Reusable, collider-free presentation geometry. No gameplay queries or damage.</summary>
    public sealed class DroneWeaponEffects : System.IDisposable
    {
        private static readonly ProfilerMarker EffectsProfile=new ProfilerMarker("ShiftFour.Effects");

        private readonly GameObject rig, flash;
        private readonly Renderer flashRenderer;
        private readonly Light pulse;
        private readonly Material material;
        private readonly LineRenderer tracer;
        private readonly LineRenderer[] sparks = new LineRenderer[8];
        private readonly Collider[] bodyColliders;
        private Vector3 origin, target, sparkOrigin, sparkNormal;
        private float shotAge = 100f, sparkAge = 100f, flashDuration = .11f, tracerDuration = .13f;
        public int ShotCount { get; private set; }
        public bool FlashVisible => flashRenderer.enabled;
        public bool TracerVisible => tracer.enabled;
        public Vector3 TracerTarget => target;

        public DroneWeaponEffects(Transform parent)
        {
            bodyColliders = parent.GetComponentsInChildren<Collider>(true);
            rig = new GameObject("Drone reusable weapon and impact effects");
            rig.transform.SetParent(parent, false);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Drone effects (runtime)", hideFlags = HideFlags.DontSave };
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", new Color(2f, 3f, 4f));
            flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Runtime gimballed muzzle";
            flash.transform.SetParent(rig.transform, false);
            var collider = flash.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            flashRenderer = flash.GetComponent<Renderer>();
            flashRenderer.sharedMaterial = material;
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off; flashRenderer.receiveShadows = false;
            pulse = flash.AddComponent<Light>(); pulse.type = LightType.Point;
            pulse.color = new Color(.35f, .7f, 1f); pulse.range = 1.2f; pulse.shadows = LightShadows.None;
            tracer = Line("Accepted shot tracer", .025f, new Color(.3f, .85f, 1f));
            for (int i = 0; i < sparks.Length; i++) sparks[i] = Line("Impact spark " + i, .018f, new Color(1f, .5f, .1f));
            CancelShot();
        }

        private LineRenderer Line(string label, float width, Color color)
        {
            var go = new GameObject(label); go.transform.SetParent(rig.transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = width; line.endWidth = width * .4f;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false; return line;
        }

        public void Fire(Vector3 acceptedOrigin, Vector3 acceptedHit, float flashSeconds, float tracerSeconds)
        {
            // Find the model's outer surface along the already validated segment. These cached
            // self-collider queries affect only muzzle placement, never hit validation or damage.
            Vector3 segment = acceptedHit - acceptedOrigin;
            Vector3 direction = segment.normalized;
            float reach = .4f;
            var backward = new Ray(acceptedHit, -direction);
            foreach (Collider collider in bodyColliders)
                if (collider != null && collider.enabled && collider.Raycast(backward, out RaycastHit hit, segment.magnitude))
                    reach = Mathf.Max(reach, Vector3.Dot(hit.point - acceptedOrigin, direction) + .06f);
            origin = acceptedOrigin + direction * Mathf.Min(reach, segment.magnitude * .75f);
            target = acceptedHit;
            flashDuration = Mathf.Clamp(flashSeconds, .08f, .15f);
            tracerDuration = Mathf.Max(.04f, tracerSeconds);
            shotAge = 0f; ShotCount++;
            flash.transform.position = origin;
            Tick(0f);
        }

        public void CancelShot()
        {
            shotAge = 100f; flashRenderer.enabled = false; tracer.enabled = false; pulse.enabled = false;
        }

        public void Clear()
        {
            CancelShot(); sparkAge = 100f;
            foreach (LineRenderer spark in sparks) spark.enabled = false;
        }

        public void Impact(Vector3 point, Vector3 normal)
        { sparkOrigin = point + normal * .025f; sparkNormal = normal; sparkAge = 0f; }

        public void Tick(float dt)
        {
            using var profileSample=EffectsProfile.Auto();
            shotAge += dt; sparkAge += dt;
            flashRenderer.enabled = shotAge < flashDuration;
            pulse.enabled = flashRenderer.enabled;
            if (flashRenderer.enabled)
            {
                float strength = 1f - shotAge / flashDuration;
                flash.transform.localScale = Vector3.one * (.09f + .12f * strength);
                pulse.intensity = 1.5f * strength;
            }
            tracer.enabled = shotAge < tracerDuration;
            if (tracer.enabled)
            {
                float t = shotAge / tracerDuration;
                tracer.SetPosition(0, Vector3.Lerp(origin, target, Mathf.Clamp01((t - .3f) * 1.6f)));
                tracer.SetPosition(1, Vector3.Lerp(origin, target, Mathf.Clamp01(.15f + t * 2.5f)));
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                var line = sparks[i]; line.enabled = sparkAge < .35f;
                if (!line.enabled) continue;
                float angle = i * Mathf.PI * 2f / sparks.Length;
                Vector3 tangent = Vector3.Cross(sparkNormal, Vector3.right);
                if (tangent.sqrMagnitude < .1f) tangent = Vector3.Cross(sparkNormal, Vector3.forward);
                tangent.Normalize();
                Vector3 velocity = (sparkNormal * 1.8f + tangent * Mathf.Cos(angle) + Vector3.Cross(sparkNormal, tangent) * Mathf.Sin(angle)) * (1f + i * .08f);
                float tail = Mathf.Max(0f, sparkAge - .035f);
                line.SetPosition(0, sparkOrigin + velocity * tail + Physics.gravity * (.5f * tail * tail));
                line.SetPosition(1, sparkOrigin + velocity * sparkAge + Physics.gravity * (.5f * sparkAge * sparkAge));
            }
        }

        public void Dispose()
        {
            if (rig != null) Object.Destroy(rig);
            if (material != null) Object.Destroy(material);
        }
    }
}
