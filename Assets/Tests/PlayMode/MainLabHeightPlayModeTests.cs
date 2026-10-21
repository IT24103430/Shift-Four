#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class MainLabHeightPlayModeTests
    {
        private CursorLockMode cursorLock;
        private bool cursorVisible;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene old = SceneManager.GetActiveScene();
            Scene empty = SceneManager.CreateScene("After MainLab height test");
            SceneManager.SetActiveScene(empty);
            if (old.path == "Assets/Scenes/MainLab.unity") yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }

        [UnityTest]
        public IEnumerator MainLab_HigherEnvelopeKeepsFourGatesAndNativePassagesFunctional()
        {
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; // Start subscribes drone death events before policies are paused for the fixture.
            Scene scene = SceneManager.GetActiveScene();
            Type brainType = RuntimeType("DroneBrain"), motorType = RuntimeType("DroneMotor");
            Type doorType = RuntimeType("LabDoor"), sessionType = RuntimeType("GameSession");
            Type healthType = RuntimeType("Health"), graphType = RuntimeType("WaypointGraph");
            var brains = Find(scene, brainType);
            var player = Find(scene, RuntimeType("PlayerController")).Single();
            ((Behaviour)player).enabled = false;
            foreach (var brain in brains) ((Behaviour)brain).enabled = false;
            var session = Find(scene, sessionType).Single();
            var progress = sessionType.GetProperty("Progress").GetValue(session);
            Type progressType = progress.GetType();
            var rooms = (string[])progressType.GetField("Rooms").GetValue(null);
            var agents = (string[])progressType.GetField("Agents").GetValue(null);
            var graph = (Component)sessionType.GetProperty("Graph").GetValue(session);
            var doors = Find(scene, doorType).OrderBy(d => (int)doorType.GetProperty("GateId").GetValue(d)).ToArray();
            Assert.That(brains.Length, Is.EqualTo(4));
            Assert.That(doors.Length, Is.EqualTo(4));
            yield return new WaitForSeconds(.15f); // Initial carving is asynchronous.
            foreach (var brain in brains)
            {
                var motor = brain.GetComponent(motorType);
                var visual = (Transform)motorType.GetField("visual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
                Assert.That(visual, Is.Not.Null, brain.name);
                Assert.That(visual.localPosition.y, Is.InRange(1.519f, 1.681f), brain.name);
                Assert.That(brain.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, brain.name);
                Assert.That(brain.GetComponent<NavMeshAgent>().baseOffset, Is.Zero);
            }
            var walls = scene.GetRootGameObjects().Single(g => g.name.StartsWith("Fixed walls")).transform;
            foreach (Transform wall in walls)
            {
                Bounds b = wall.GetComponent<BoxCollider>().bounds;
                Assert.That(b.min.y, Is.EqualTo(0f).Within(.001f));
                Assert.That(b.max.y, Is.EqualTo(4f).Within(.001f));
                Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                Vector3 origin = b.center - normal * 2f;
                origin.y = 3.5f;
                Assert.That(wall.GetComponent<BoxCollider>().Raycast(new Ray(origin, normal), out _, 4f), Is.True);
            }
            string[] names = { "ScoutBrain", "GuardBrain", "InterceptorBrain", "FlankerBrain" };
            for (int id = 0; id < 4; id++)
            {
                var door = doors[id];
                Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(door), Is.False);
                string room = rooms[id];
                string agent = agents[id];
                string prompt = (string)doorType.GetProperty("Prompt").GetValue(door);
                Assert.That(prompt, Does.Contain("defeat " + agent).And.Contain("activate " + room + " console"));
                int before = (int)graphType.GetProperty("Version").GetValue(graph);
                var brain = brains.Single(b => b.GetType().Name == names[id]);
                if (id % 2 == 0) progressType.GetMethod("ActivateConsole").Invoke(progress, new object[] { id });
                else healthType.GetMethod("TakeDamage").Invoke(brain.GetComponent(healthType), new object[] { 10000f });
                yield return new WaitForSeconds(.1f);
                Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(door), Is.False, "One prerequisite must not open gate " + id);
                prompt = (string)doorType.GetProperty("Prompt").GetValue(door);
                if (id % 2 == 0)
                    Assert.That(prompt, Does.Contain("defeat " + agent).And.Not.Contain("activate " + room + " console"));
                else
                    Assert.That(prompt, Does.Contain("activate " + room + " console").And.Not.Contain("defeat " + agent));
                if (id % 2 == 0) healthType.GetMethod("TakeDamage").Invoke(brain.GetComponent(healthType), new object[] { 10000f });
                else progressType.GetMethod("ActivateConsole").Invoke(progress, new object[] { id });
                float until = Time.time + 4f;
                while (!(bool)doorType.GetProperty("IsOpen").GetValue(door) && Time.time < until) yield return null;
                Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(door), Is.True, "Gate " + id);
                Assert.That((string)doorType.GetProperty("Prompt").GetValue(door), Is.EqualTo(room + " gate: OPEN"));
                Assert.That(door.GetComponent<BoxCollider>().enabled, Is.False);
                Assert.That(door.GetComponent<NavMeshObstacle>().enabled, Is.False);
                Assert.That((int)graphType.GetProperty("Version").GetValue(graph), Is.EqualTo(before + 1));
                until = Time.time + 2f;
                bool connected;
                do
                {
                    yield return null;
                    connected = CompletePath(door.transform.position - door.transform.forward * 2f,
                        door.transform.position + door.transform.forward * 2f);
                } while (!connected && Time.time < until);
                Assert.That(connected, Is.True, "Native passage must remain usable after carving settles: gate " + id);
                for (int later = id + 1; later < 4; later++)
                    Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(doors[later]), Is.False);
            }
            for (int i = 0; i < (int)graphType.GetProperty("NodeCount").GetValue(graph); i++)
                Assert.That(((Vector3)graphType.GetMethod("NodePosition").Invoke(graph, new object[] { i })).y, Is.Zero);
        }

        private static Type RuntimeType(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component[] Find(Scene scene, Type type)
            => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren(type, true)).ToArray();
        private static bool CompletePath(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, .25f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit b, .25f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
#endif
