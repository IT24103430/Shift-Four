using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class DroneMotorPlayModeTests
    {
        private static readonly Vector3 Offset = new Vector3(1000f, 0f, 1000f);
        private Type motorType, graphType, edgeType;
        private GameObject root, drone;
        private Component motor, graph;
        private NavMeshAgent agent;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private int previousFrameRate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 60;
            motorType = Type.GetType("ShiftFour.DroneMotor, Assembly-CSharp", true);
            graphType = Type.GetType("ShiftFour.WaypointGraph, Assembly-CSharp", true);
            edgeType = Type.GetType("ShiftFour.GraphEdge, Assembly-CSharp", true);
            root = new GameObject("Isolated motor test");
            root.SetActive(false);
            var graphObject = new GameObject("Test graph");
            graphObject.transform.SetParent(root.transform);
            graph = graphObject.AddComponent(graphType);
            BuildFloor(false);
            drone = new GameObject("Test drone");
            drone.transform.SetParent(root.transform);
            drone.transform.position = Offset;
            agent = drone.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 1f;
            motor = drone.AddComponent(motorType);
            Set("graph", graph);
            ConfigureGraph(new[] { Vector3.zero });
            root.SetActive(true);
            yield return null;
            Assert.That(agent.isOnNavMesh, Is.True, "The isolated test agent must bind to its in-memory floor.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.targetFrameRate = previousFrameRate;
            Object.Destroy(root);
            yield return null;
            if (instance.valid) instance.Remove();
            if (data != null) Object.Destroy(data);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoGoal_GateChangeDoesNotStartMovement()
        {
            GraphCall("SetGateOpen", 0, true);
            yield return new WaitForSeconds(0.4f);
            Assert.That(State, Is.EqualTo("NoGoal"));
            Assert.That(HasRoute, Is.False);
            Assert.That(Vector3.Distance(drone.transform.position, Offset), Is.LessThan(0.25f));
        }

        [UnityTest]
        public IEnumerator AlreadyAtGoal_ArrivesWithoutLeavingThePoint()
        {
            Go(Vector3.zero);
            yield return WaitForState("Arrived");
            Assert.That(Vector3.Distance(drone.transform.position, Offset), Is.LessThan(0.25f));
        }

        [UnityTest]
        public IEnumerator OrderedRoute_ArrivesAndKeepsSpeedAndAcceleration()
        {
            var corner = Vector3.right * 4f;
            var end = corner + Vector3.forward * 4f;
            ConfigureGraph(new[] { Vector3.zero, corner, end });
            Go(end);
            Assert.That(State, Is.EqualTo("Pending"));
            yield return WaitForState("Arrived");
            Assert.That(HasRoute, Is.False);
            Assert.That(Vector3.Distance(drone.transform.position, Offset + end), Is.LessThanOrEqualTo(1.1f));
            Assert.That(agent.velocity.magnitude, Is.LessThanOrEqualTo(0.1f));
            Assert.That(agent.speed, Is.EqualTo(3.5f));
            Assert.That(agent.acceleration, Is.EqualTo(8f));
            Assert.That(agent.stoppingDistance, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator RepeatedUnreachableGoal_IsCachedUntilGateChanges()
        {
            var end = Vector3.right * 5f;
            ConfigureGraph(new[] { Vector3.zero, end }, 2);
            Go(end);
            Assert.That(State, Is.EqualTo("Blocked"));
            object failedRoute = Get("route");
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.15f);
                Go(end);
                Assert.That(Get("route"), Is.SameAs(failedRoute));
            }
            GraphCall("SetGateOpen", 2, true);
            yield return WaitForState("Arrived");
        }

        [UnityTest]
        public IEnumerator ChangedGoal_RetriesAfterNoGraphRoute()
        {
            ConfigureGraph(new[] { Vector3.zero, Vector3.right * 5f }, 1);
            Go(Vector3.right * 5f);
            Assert.That(State, Is.EqualTo("Blocked"));
            Go(Vector3.forward * 4f);
            yield return WaitForState("Arrived");
            Assert.That(Vector3.Distance(drone.transform.position, Offset + Vector3.forward * 4f), Is.LessThan(1.1f));
        }

        [UnityTest]
        public IEnumerator ArrivedAgent_DisplacementAllowsSameGoalAgain()
        {
            var end = Vector3.right * 4f;
            Go(end);
            yield return WaitForState("Arrived");
            Assert.That(agent.Warp(Offset), Is.True, "Controlled displacement within the test floor only.");
            Go(end);
            yield return WaitForState("Arrived");
            Assert.That(Vector3.Distance(drone.transform.position, Offset + end), Is.LessThan(1.1f));
        }

        [UnityTest]
        public IEnumerator PartialPath_IsBlockedAndCannotBecomeArrivalAcrossGap()
        {
            Object.Destroy(drone);
            yield return null;
            instance.Remove();
            Object.Destroy(data);
            BuildFloor(true);
            drone = new GameObject("Left island drone");
            drone.transform.SetParent(root.transform);
            drone.transform.position = Offset + Vector3.left * 2f;
            agent = drone.AddComponent<NavMeshAgent>();
            agent.stoppingDistance = 1f;
            motor = drone.AddComponent(motorType);
            Set("graph", graph);
            ConfigureGraph(new[] { Vector3.right * 2f });
            yield return null;
            // Both endpoints map to one graph node in the older A*. Native navigation must reject it.
            Go(Vector3.right * 2f);
            yield return WaitForState("Blocked");
            Assert.That(HasRoute, Is.False);
            Assert.That(drone.transform.position.x, Is.LessThan(Offset.x));
        }

        [UnityTest]
        public IEnumerator NearbyWaypointAcrossWall_IsNotSkippedWhilePathDetours()
        {
            Object.Destroy(drone);
            yield return null;
            instance.Remove();
            Object.Destroy(data);
            var sources = new List<NavMeshBuildSource>
            {
                Floor(Vector3.zero, new Vector3(20f, 0.2f, 20f)),
                new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(0.1f, 2f, 6f),
                    transform = Matrix4x4.TRS(Offset + Vector3.up, Quaternion.identity, Vector3.one),
                    area = 1
                }
            };
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.2f; // In-memory fixture only: make a sub-1.35m wall separation.
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.05f;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(Offset, Vector3.one * 50f), Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            drone = new GameObject("Wall detour drone");
            drone.transform.SetParent(root.transform);
            drone.transform.position = Offset + Vector3.left * 0.5f;
            agent = drone.AddComponent<NavMeshAgent>();
            agent.stoppingDistance = 1f;
            motor = drone.AddComponent(motorType);
            Set("graph", graph);
            ConfigureGraph(new[] { Vector3.right * 0.5f });
            yield return null;
            Assert.That(agent.isOnNavMesh, Is.True);
            Assert.That(NavMesh.SamplePosition(Offset + Vector3.right * 0.5f,
                out NavMeshHit target, 0.1f, NavMesh.AllAreas), Is.True, "The far-side target must be on the fixture NavMesh.");
            var path = new NavMeshPath();
            Assert.That(agent.CalculatePath(target.position, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "The fixture must offer a full detour.");
            Assert.That(Vector3.Distance(drone.transform.position, Offset + Vector3.right * 0.5f), Is.LessThan(1.35f));
            Go(Vector3.right * 0.5f);
            yield return WaitForState("Following", 2f);
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(agent.remainingDistance, Is.GreaterThan(2f));
            Assert.That((int)Get("step"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator TemporarilyDisabledAgent_PreservesGoalAndRecoversWhenEnabled()
        {
            agent.enabled = false;
            Go(Vector3.right * 4f);
            Assert.That(State, Is.EqualTo("Blocked"));
            yield return new WaitForSeconds(0.6f);
            agent.enabled = true;
            yield return WaitForState("Arrived");
        }

        [UnityTest]
        public IEnumerator Stop_PreventsNewGoalsAndGateRecovery()
        {
            Go(Vector3.right * 5f);
            yield return new WaitForSeconds(0.2f);
            MotorCall("Stop");
            var stopped = drone.transform.position;
            Go(Vector3.forward * 5f);
            GraphCall("SetGateOpen", 3, true);
            yield return new WaitForSeconds(0.6f);
            Assert.That(State, Is.EqualTo("Stopped"));
            Assert.That(HasRoute, Is.False);
            Assert.That(Vector3.Distance(stopped, drone.transform.position), Is.LessThan(0.05f));
        }

        [UnityTest]
        public IEnumerator RepeatedFollowingGoal_DoesNotReplaceRouteOrResetCursor()
        {
            Go(Vector3.right * 6f);
            yield return WaitForState("Following");
            object route = Get("route");
            for (int i = 0; i < 6; i++)
            {
                Go(Vector3.right * 6f);
                yield return new WaitForSeconds(0.1f);
                Assert.That(Get("route"), Is.SameAs(route));
            }
        }

        [UnityTest]
        public IEnumerator ArrivedGoal_UnrelatedGateChangeDoesNotRecalculate()
        {
            Go(Vector3.right * 3f);
            yield return WaitForState("Arrived");
            object route = Get("route");
            GraphCall("SetGateOpen", 3, true);
            yield return new WaitForSeconds(0.4f);
            Assert.That(State, Is.EqualTo("Arrived"));
            Assert.That(Get("route"), Is.SameAs(route));
        }

        [UnityTest]
        public IEnumerator GateChangeWithNoRoute_HasBoundedGraceRetriesAndKeepsOtherGateLocked()
        {
            var end = Vector3.right * 5f;
            ConfigureGraph(new[] { Vector3.zero, end }, 2);
            Go(end);
            GraphCall("SetGateOpen", 3, true); // Gate 2 still blocks the requested route.
            float until = Time.time + 4f;
            while (Time.time < until && (State != "Blocked" || (int)Get("retryCount") < 2)) yield return null;
            Assert.That(State, Is.EqualTo("Blocked"));
            Assert.That((int)Get("retryCount"), Is.EqualTo(2));
            object failed = Get("route");
            yield return new WaitForSeconds(0.8f);
            Go(end);
            Assert.That(Get("route"), Is.SameAs(failed));
            Assert.That((bool)graphType.GetMethod("IsGateOpen").Invoke(graph, new object[] { 2 }), Is.False);
            GraphCall("SetGateOpen", 2, true);
            yield return WaitForState("Arrived");
        }

        [UnityTest]
        public IEnumerator StalledAgent_RecoversAfterCooldown()
        {
            Go(Vector3.right * 5f);
            yield return WaitForState("Following");
            agent.isStopped = true; // Simulate a native agent that has a valid path but makes no progress.
            yield return WaitForState("Arrived");
            Assert.That((int)Get("retryCount"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClearedNativePath_IsBlockedInsteadOfReportedAsArrival()
        {
            Go(Vector3.right * 6f);
            yield return WaitForState("Following");
            agent.ResetPath();
            yield return WaitForState("Blocked", 4f);
            Assert.That(HasRoute, Is.False);
            Assert.That(Vector3.Distance(drone.transform.position, Offset + Vector3.right * 6f), Is.GreaterThan(1.1f));
        }

        [UnityTest]
        public IEnumerator PersistentStall_StopsAfterTwoRetriesAndGraphChangeRestartsBudget()
        {
            agent.speed = 0f; // Deliberately stalled fixture; the motor must not override configured speed.
            var end = Vector3.right * 5f;
            Go(end);
            float until = Time.time + 10f;
            while (Time.time < until && (State != "Blocked" || (int)Get("retryCount") < 2)) yield return null;
            Assert.That(State, Is.EqualTo("Blocked"));
            Assert.That((int)Get("retryCount"), Is.EqualTo(2));
            object failed = Get("route");
            yield return new WaitForSeconds(0.8f);
            Go(end);
            Assert.That(Get("route"), Is.SameAs(failed));
            Assert.That(HasRoute, Is.False);
            agent.speed = 3.5f;
            GraphCall("SetGateOpen", 0, true);
            yield return WaitForState("Arrived");
            Assert.That((int)Get("retryCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator Hover_UsesAuthoredPositionAndReconfigureDoesNotDrift()
        {
            var model = new GameObject("Authored visual").transform;
            model.SetParent(drone.transform);
            var authored = new Vector3(0.7f, 2.3f, -0.4f);
            model.localPosition = authored;
            MotorCall("Configure", model);
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.1f);
                MotorCall("Configure", model);
                Assert.That(model.localPosition.x, Is.EqualTo(authored.x));
                Assert.That(model.localPosition.z, Is.EqualTo(authored.z));
                Assert.That(Mathf.Abs(model.localPosition.y - authored.y), Is.LessThanOrEqualTo(0.081f));
            }
        }

        [UnityTest]
        public IEnumerator MovingAgent_RespectsTurnLimitAndHoldsHeadingAfterArrival()
        {
            Go(Vector3.right * 5f);
            Quaternion previous = drone.transform.rotation;
            Vector3 samplePosition = drone.transform.position;
            float sampleAt = Time.time;
            float until = Time.time + 8f;
            while (Time.time < until && State != "Arrived")
            {
                yield return null;
                Assert.That(Quaternion.Angle(previous, drone.transform.rotation),
                    Is.LessThanOrEqualTo(agent.angularSpeed * Time.deltaTime + 0.05f));
                if (Time.time - sampleAt >= 0.2f)
                {
                    // Measure travel, avoiding instantaneous native velocity quantization at large coordinates.
                    float elapsed = Time.time - sampleAt;
                    Assert.That(Vector3.Distance(samplePosition, drone.transform.position),
                        Is.LessThanOrEqualTo(agent.speed * elapsed + 0.03f));
                    samplePosition = drone.transform.position;
                    sampleAt = Time.time;
                }
                previous = drone.transform.rotation;
            }
            Assert.That(State, Is.EqualTo("Arrived"));
            previous = drone.transform.rotation;
            yield return new WaitForSeconds(0.4f);
            Assert.That(Quaternion.Angle(previous, drone.transform.rotation), Is.LessThan(0.01f));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AllFourExistingPrefabs_CanFollowTheSharedMotorContract()
        {
            Object.Destroy(drone);
            yield return null;
            foreach (string name in new[] { "Scout", "Flanker", "Guard", "Interceptor" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/" + name + ".prefab");
                drone = Object.Instantiate(prefab, Offset, Quaternion.identity, root.transform);
                agent = drone.GetComponent<NavMeshAgent>();
                motor = drone.GetComponent(motorType);
                // Direct motor integration fixture; tactical behavior is validated separately in MainLab.
                ((Behaviour)drone.GetComponent(Type.GetType("ShiftFour." + name + "Brain, Assembly-CSharp", true))).enabled = false;
                Set("graph", graph);
                yield return null;
                Go(Vector3.right * 3f);
                yield return WaitForState("Arrived");
                MotorCall("Stop");
                Assert.That(State, Is.EqualTo("Stopped"), name);
                Object.Destroy(drone);
                yield return null;
            }
        }
#endif

        private string State => Get("state").ToString();
        private bool HasRoute => (bool)motorType.GetProperty("HasRoute").GetValue(motor);
        private object Get(string name) => motorType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
        private void Set(string name, object value) => motorType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(motor, value);
        private void Go(Vector3 local) => MotorCall("GoTo", Offset + local);
        private void MotorCall(string name, params object[] args) => motorType.GetMethod(name).Invoke(motor, args);
        private void GraphCall(string name, params object[] args) => graphType.GetMethod(name).Invoke(graph, args);

        private IEnumerator WaitForState(string expected, float timeout = 8f)
        {
            float until = Time.time + timeout;
            while (Time.time < until && State != expected) yield return null;
            Assert.That(State, Is.EqualTo(expected));
        }

        private void ConfigureGraph(Vector3[] points, int gate = -1)
        {
            var nodes = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                nodes[i] = new GameObject("Node " + i).transform;
                nodes[i].SetParent(root.transform);
                nodes[i].position = Offset + points[i];
            }
            Array edges = Array.CreateInstance(edgeType, Math.Max(0, points.Length - 1));
            for (int i = 0; i < edges.Length; i++)
                edges.SetValue(Activator.CreateInstance(edgeType, new object[] { i, i + 1, gate }), i);
            GraphCall("Configure", nodes, edges);
        }

        private void BuildFloor(bool disconnected)
        {
            var sources = new List<NavMeshBuildSource>();
            if (disconnected)
            {
                sources.Add(Floor(Vector3.left * 3f, new Vector3(4f, 0.2f, 8f)));
                sources.Add(Floor(Vector3.right * 3f, new Vector3(4f, 0.2f, 8f)));
            }
            else sources.Add(Floor(Vector3.zero, new Vector3(20f, 0.2f, 20f)));
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Offset, Vector3.one * 50f), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            instance = NavMesh.AddNavMeshData(data);
        }

        private static NavMeshBuildSource Floor(Vector3 center, Vector3 size)
            => new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = size,
                transform = Matrix4x4.TRS(Offset + center + Vector3.down * 0.1f, Quaternion.identity, Vector3.one),
                area = 0
            };
    }
}
