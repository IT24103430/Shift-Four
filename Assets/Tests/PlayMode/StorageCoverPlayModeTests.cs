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
    public sealed class StorageCoverPlayModeTests
    {
        private CursorLockMode cursor;
        private bool cursorVisible;
        private float captureDelta;
        private int frameRate;
        private Component flanker;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static object Call(Component target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            captureDelta = Time.captureDeltaTime; frameRate = Application.targetFrameRate;
            Time.captureDeltaTime = 1f / 60f; Application.targetFrameRate = 120;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var player in Find("PlayerController")) ((Behaviour)player).enabled = false;
            foreach (var brain in Find("DroneBrain"))
            {
                ((Behaviour)brain).enabled = false;
                ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                brain.GetComponent<NavMeshAgent>().isStopped = true;
            }
            flanker = Find("FlankerBrain").Single();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After Storage cover test"));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            Time.captureDeltaTime = captureDelta; Application.targetFrameRate = frameRate;
        }

        [UnityTest] public IEnumerator OnlyApprovedCoverIsRemoved_TwoSolidDefensivePropsRemain()
        {
            var storage = GameObject.Find("Storage").transform;
            Assert.That(storage.Find("Fixed cover 2"), Is.Null, "Remove the whole object, including collision and decorative children");
            Assert.That(storage.GetComponentsInChildren<Renderer>().Any(r =>
                (r.name == "Cargo band" || r.name == "Storage equipment face") &&
                Mathf.Abs(r.bounds.center.x - 10f) < .01f &&
                r.bounds.center.z > -8.81f && r.bounds.center.z < -8.74f), Is.False,
                "The approved cover's separately parented face and four bands must not float in the cleared space");
            foreach (string name in new[] { "Fixed cover 1", "Fixed cover 3" })
            {
                var cover = storage.Find(name);
                Assert.That(cover, Is.Not.Null);
                Assert.That(cover.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger), Is.True);
                Assert.That(cover.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True);
            }
            Assert.That(storage.Find("Storage console"), Is.Not.Null);
            Assert.That(Find("LabDoor").Length, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest] public IEnumerator SavedNavMesh_KeepsConsoleExitAndBothSideApproachesComplete()
        {
            Vector3 start = new Vector3(10, 0, -2.5f);
            foreach (Vector3 goal in new[] { new Vector3(15.5f, 0, -4.5f), new Vector3(18, 0, -15),
                new Vector3(5, 0, -13), new Vector3(15, 0, -13) })
            {
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(start, goal, NavMesh.AllAreas, path), Is.True, goal.ToString());
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), goal.ToString());
            }
            yield return null;
        }

        [UnityTest] public IEnumerator FlankerMotor_ReachesLeftAndRightApproachesWithoutBodyPenetration()
        {
            var motor = flanker.GetComponent(T("DroneMotor"));
            var body = flanker.GetComponent(T("DroneBody"));
            var agent = flanker.GetComponent<NavMeshAgent>();
            foreach (Vector3 goal in new[] { new Vector3(5, 0, -13), new Vector3(15, 0, -13) })
            {
                ((Behaviour)motor).enabled = false;
                agent.ResetPath();
                Assert.That(agent.Warp(new Vector3(10, .01333333f, -5.6f)), Is.True);
                Physics.SyncTransforms();
                Assert.That((bool)Call(body, "CanOccupy", flanker.transform.position), Is.True);
                agent.isStopped = false;
                ((Behaviour)motor).enabled = true;
                Call(motor, "SetMovementMode", Enum.Parse(T("DroneMovementMode"), "Chase"));
                Call(motor, "GoTo", goal);
                float until = Time.time + 18f;
                while (Vector3.Distance(flanker.transform.position, goal) > 1.1f && Time.time < until)
                {
                    yield return null;
                    Assert.That((bool)Call(body, "CanOccupy", flanker.transform.position), Is.True, "Raised-body clearance");
                }
                Assert.That(Vector3.Distance(flanker.transform.position, goal), Is.LessThanOrEqualTo(1.1f), goal.ToString());
                Debug.Log("[Storage QA] Flanker reached " + goal + " at " + flanker.transform.position);
            }
        }
    }
}
#endif
