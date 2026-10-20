using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    // Native agents otherwise register before the scene's NavMeshSurface.OnEnable in a fresh player.
    // Authored MainLab agents start disabled; enable them after all scene OnEnable calls, before policy Start.
    [DefaultExecutionOrder(-1000), RequireComponent(typeof(NavMeshSurface))]
    public sealed class LabNavigationStartup : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent[] startupAgents;

        public void Configure(NavMeshAgent[] agents) => startupAgents = agents;

        private void Start()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData == null)
            {
                Debug.LogError("MainLab requires its saved NavMesh data before drones can start.", this);
                return;
            }
            surface.AddData(); // Idempotent; never builds or changes the saved bake.
            if (startupAgents == null) return;
            foreach (var agent in startupAgents)
            {
                if (agent == null || !agent.gameObject.activeInHierarchy) continue;
                agent.enabled = true;
                if (!agent.isOnNavMesh)
                    Debug.LogError("Authored drone spawn is outside the saved NavMesh: " + agent.name, agent);
            }
        }
    }
}
