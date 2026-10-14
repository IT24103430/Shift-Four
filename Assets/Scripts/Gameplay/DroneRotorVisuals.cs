using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    /// <summary>
    /// Presentation only. Finds the OBJ rotor groups and spins their meshes around each
    /// motor's own center. Does not modify the NavMeshAgent, AI, physics or damage logic.
    /// </summary>
    public sealed class DroneRotorVisuals : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rotorRpm = 390f;
        [SerializeField] private bool alternateDirection = true;
        private Health health;
        private readonly List<Transform> pivots = new List<Transform>();

        private void Awake()
        {
            health = GetComponentInParent<Health>();
            // Use direct mesh objects; Blender / Unity FBX hierarchies can add extra parents.
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("Rotor")) continue;
                if (filter.sharedMesh == null) continue;
                Transform mesh = filter.transform;
                Vector3 center = mesh.TransformPoint(filter.sharedMesh.bounds.center);
                GameObject pivotObject = new GameObject("Animated " + filter.name);
                Transform pivot = pivotObject.transform;
                pivot.SetParent(transform, false);
                pivot.position = center;
                mesh.SetParent(pivot, true);
                pivots.Add(pivot);
            }
        }

        private void Update()
        {
            if (health != null && !health.IsAlive) return;
            float degrees = rotorRpm * 6f * Time.deltaTime;
            for (int i = 0; i < pivots.Count; i++)
            {
                float direction = alternateDirection && i % 2 == 1 ? -1f : 1f;
                pivots[i].Rotate(Vector3.up, degrees * direction, Space.Self);
            }
        }
    }
}
