#if UNITY_EDITOR
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
    public sealed class DroneMovementSpeedsPlayModeTests
    {
        private static readonly Vector3 Origin = new Vector3(1200f, 0f, 1200f);
        private GameObject root, drone;
        private Component motor, graph, visuals;
        private NavMeshAgent agent;
        private NavMeshData data;
        private NavMeshDataInstance mesh;
        private int frameRate;
        private float captureDelta;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object c, string name) => c.GetType().GetProperty(name).GetValue(c);
        private static object Call(object c, string name, params object[] args) => c.GetType().GetMethod(name).Invoke(c, args);
        private object Field(string name) => motor.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor);
        private void Set(string name, object value) => motor.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(motor, value);
        private string State => Field("state").ToString();
        private void Mode(string mode) => Call(motor, "SetMovementMode", Enum.Parse(T("DroneMovementMode"), mode));
        private static object Settings(string name) => Get(Resources.Load("DroneDifficulty/" + name), "Settings");
        private void Go(Vector3 local) => Call(motor, "GoTo", Origin + local);

        [UnitySetUp] public IEnumerator Setup()
        {
            frameRate = Application.targetFrameRate; captureDelta = Time.captureDeltaTime;
            Application.targetFrameRate = 60; Time.captureDeltaTime = 1f / 60f;
            root = new GameObject("Progressive speed fixture"); root.SetActive(false);
            var g = new GameObject("Graph"); g.transform.SetParent(root.transform);
            graph = g.AddComponent(T("WaypointGraph"));
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, size = new Vector3(70f, .2f, 40f),
                transform = Matrix4x4.TRS(Origin + new Vector3(15f, -.1f, 0f), Quaternion.identity, Vector3.one), area = 0 } };
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Origin, Vector3.one * 150f), Vector3.zero, Quaternion.identity);
            mesh = NavMesh.AddNavMeshData(data);
            CreateDrone("Flanker"); ConfigureGraph(new[] { Vector3.zero });
            root.SetActive(true); yield return null;
            Assert.That(agent.isOnNavMesh, Is.True);
        }

        private void CreateDrone(string name)
        {
            drone = new GameObject(name); drone.SetActive(false); drone.transform.SetParent(root.transform);
            drone.transform.position = Origin;
            agent = drone.AddComponent<NavMeshAgent>(); agent.speed = 3.5f; agent.acceleration = 8f;
            agent.angularSpeed = 120f; agent.stoppingDistance = 1f;
            motor = drone.AddComponent(T("DroneMotor"));
            drone.AddComponent<CapsuleCollider>().center = Vector3.up * 1.6f;
            var body = drone.AddComponent(T("DroneBody"));
            drone.SetActive(true); // Awake binds the agent before public initialization.
            // The parent starts inactive in Setup: activate it for initialization, then leave it active.
            if (!root.activeSelf) root.SetActive(true);
            Set("body", body); Set("graph", graph);
            Assert.That(Call(motor, "InitializeMovement", Settings(name)), Is.True);
            var model = new GameObject("Test visual"); model.transform.SetParent(drone.transform, false);
            visuals = model.AddComponent(T("DroneRotorVisuals"));
        }

        private void ConfigureGraph(Vector3[] points, int gate = -1)
        {
            var nodes = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            { nodes[i] = new GameObject("Node").transform; nodes[i].SetParent(root.transform); nodes[i].position = Origin + points[i]; }
            var edges = Array.CreateInstance(T("GraphEdge"), Math.Max(0, points.Length - 1));
            for (int i = 0; i < edges.Length; i++) edges.SetValue(Activator.CreateInstance(T("GraphEdge"), i, i + 1, gate), i);
            Call(graph, "Configure", nodes, edges);
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            Application.targetFrameRate = frameRate; Time.captureDeltaTime = captureDelta;
            Object.Destroy(root); yield return null;
            if (mesh.valid) mesh.Remove(); if (data != null) Object.Destroy(data); yield return null;
        }

        [UnityTest] public IEnumerator FourProfiles_AllModes_ChangeSpeedWithoutReplanningOrResetting()
        {
            foreach (string name in new[] { "Scout", "Guard", "Interceptor", "Flanker" })
            {
                Object.Destroy(drone); yield return null; CreateDrone(name); yield return null;
                object settings = Settings(name);
                float acceleration = agent.acceleration, angular = agent.angularSpeed;
                Go(Vector3.right * 30f); yield return new WaitForSeconds(.3f);
                object route = Field("route"); object requested = Field("requestedFrame"); object nextPlan = Field("nextPlanAt");
                Vector3 destination = agent.destination;
                foreach (var pair in new[] { ("Investigate", "investigationSpeed"), ("Chase", "chaseSpeed"), ("Patrol", "patrolSpeed") })
                {
                    float before = agent.speed; Mode(pair.Item1);
                    Assert.That(agent.speed, Is.EqualTo(before), "Selecting a mode must not jump the cap");
                    float until = Time.time + .4f;
                    while (Time.time < until)
                    {
                        before = agent.speed; Mode(pair.Item1); yield return null;
                        Assert.That(Mathf.Abs(agent.speed - before), Is.LessThanOrEqualTo(acceleration * Time.deltaTime + .0001f));
                        Assert.That(Field("route"), Is.SameAs(route));
                        Assert.That(Field("requestedFrame"), Is.EqualTo(requested), "No extra SetDestination for speed changes");
                        Assert.That(Field("nextPlanAt"), Is.EqualTo(nextPlan), "No extra A* attempt for speed changes");
                        Assert.That(agent.destination, Is.EqualTo(destination));
                    }
                    float expected = (float)settings.GetType().GetField(pair.Item2).GetValue(settings);
                    Assert.That(agent.speed, Is.EqualTo(expected).Within(.0001f), name + " " + pair.Item1);
                    Assert.That((float)Get(motor, "CruiseSpeed"), Is.EqualTo(agent.speed), "ETA input uses the current limit");
                    Assert.That(Call(motor, "InitializeMovement", Settings("Scout")), Is.False, "Rebinding cannot reset movement");
                    Assert.That(agent.speed, Is.EqualTo(expected).Within(.0001f));
                }
                Assert.That(agent.acceleration, Is.EqualTo(acceleration)); Assert.That(agent.angularSpeed, Is.EqualTo(angular));
                Assert.That(agent.autoBraking, Is.True); Assert.That(agent.stoppingDistance, Is.EqualTo(1f));
            }
        }

        [UnityTest] public IEnumerator MotionAt30Hz() => Motion(30);
        [UnityTest] public IEnumerator MotionAt60Hz() => Motion(60);
        [UnityTest] public IEnumerator MotionAt120Hz() => Motion(120);
        private IEnumerator Motion(int hz)
        {
            Time.captureDeltaTime = 1f / hz; Application.targetFrameRate = hz;
            Mode("Chase");
            ConfigureGraph(new[] { Vector3.zero, Vector3.right * 12f, new Vector3(12f, 0f, 8f) });
            Go(new Vector3(12f, 0f, 8f));
            Quaternion previous = drone.transform.rotation;
            float maxSpeed = 0f, until = Time.time + 12f;
            while (Time.time < until && State != "Arrived")
            {
                yield return null;
                Assert.That(Quaternion.Angle(previous, drone.transform.rotation), Is.LessThanOrEqualTo(agent.angularSpeed * Time.deltaTime + .06f));
                float actual = (float)Get(motor, "ActualSpeed"); maxSpeed = Mathf.Max(maxSpeed, actual);
                Assert.That(actual, Is.LessThanOrEqualTo(4.2f + .04f), "Accepted displacement must respect chase limit");
                Assert.That((Vector3)Get(visuals, "MovementVelocity"), Is.EqualTo((Vector3)Get(motor, "ActualVelocity")));
                previous = drone.transform.rotation;
            }
            Assert.That(maxSpeed, Is.GreaterThan(4f), "The high-speed route must reach cruise");
            Assert.That(State, Is.EqualTo("Arrived"));
            Assert.That(Vector3.Distance(drone.transform.position, Origin + new Vector3(12f, 0f, 8f)), Is.LessThanOrEqualTo(1.06f));
            yield return new WaitForSeconds(.2f);
            Assert.That((float)Get(motor, "ActualSpeed"), Is.LessThan(.1f));
            Assert.That(agent.stoppingDistance, Is.EqualTo(1f));
            Debug.Log("[Speed QA] " + hz + " Hz: arrived, max accepted speed " + maxSpeed.ToString("F3") + " m/s");
        }

        [UnityTest] public IEnumerator StraightMotion_BoundsAccelerationAndBrakesOnModeChange()
        {
            Go(Vector3.right * 30f); yield return new WaitForSeconds(1f); Mode("Chase");
            float previous = (float)Get(motor, "ActualSpeed");
            for (int i = 0; i < 36; i++)
            {
                yield return null;
                float current = (float)Get(motor, "ActualSpeed");
                Assert.That(Mathf.Abs(current - previous), Is.LessThanOrEqualTo(agent.acceleration * Time.deltaTime + .035f));
                previous = current;
            }
            Assert.That(previous, Is.GreaterThan(4f));
            Mode("Patrol");
            bool braking = false;
            for (int i = 0; i < 50; i++)
            {
                yield return null;
                float current = (float)Get(motor, "ActualSpeed");
                // Large-coordinate float quantization can affect two displacement samples.
                Assert.That(Mathf.Abs(current - previous), Is.LessThanOrEqualTo(agent.acceleration * Time.deltaTime + .035f));
                braking |= Get(visuals, "State").ToString() == "Braking";
                previous = current;
            }
            Assert.That(previous, Is.EqualTo(2.8f).Within(.04f));
            Assert.That(braking, Is.True, "A reduced speed limit must still produce animation braking cues");
        }

        [UnityTest] public IEnumerator Chase_RaisedWallBlocksAndResumesWithoutShutdown()
            => Obstacle(false);
        [UnityTest] public IEnumerator Chase_PlayerColliderBlocksAndResumesWithoutShutdown()
            => Obstacle(true);
        private IEnumerator Obstacle(bool player)
        {
            Mode("Chase");
            var obstacle = new GameObject(player ? "Player collider" : "Raised beam"); obstacle.transform.SetParent(root.transform);
            obstacle.transform.position = Origin + Vector3.right * 8f;
            if (player)
            { var cc = obstacle.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f; }
            else
            { var box = obstacle.AddComponent<BoxCollider>(); box.center = Vector3.up * 2.3f; box.size = new Vector3(.2f, .4f, 30f); }
            Physics.SyncTransforms(); Go(Vector3.right * 16f);
            float until = Time.time + 4f;
            while (Time.time < until)
            {
                yield return null;
                Assert.That(drone.transform.position.x, Is.LessThan(Origin.x + (player ? 6.3f : 6.5f)));
                Assert.That(State, Is.Not.EqualTo("Stopped"));
            }
            Assert.That(drone.transform.position.x, Is.GreaterThan(Origin.x + 5f));
            // Static-geometry avoidance may sidestep along the wall. A player forces a full hold.
            if (player) Assert.That((float)Get(motor, "ActualSpeed"), Is.LessThan(.1f), "A full collision hold reports zero to animation");
            Assert.That((float)Get(visuals, "MovementSpeed"), Is.EqualTo((float)Get(motor, "ActualSpeed")));
            Object.Destroy(obstacle); yield return null;
            until = Time.time + 8f;
            while (Time.time < until && State != "Arrived") yield return null;
            Assert.That(State, Is.EqualTo("Arrived"));
        }

        [UnityTest] public IEnumerator Chase_GateInvalidationAndTerminalStopRemainAuthoritative()
        {
            Mode("Chase"); ConfigureGraph(new[] { Vector3.zero, Vector3.right * 16f }, 0);
            Call(graph, "SetGateOpen", 0, true); Go(Vector3.right * 16f);
            yield return new WaitForSeconds(.6f); Call(graph, "SetGateOpen", 0, false);
            yield return new WaitForSeconds(.5f);
            Assert.That(State, Is.EqualTo("Blocked")); Assert.That((bool)Get(motor, "HasRoute"), Is.False);
            Vector3 held = drone.transform.position;
            Mode("Patrol"); yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(held, drone.transform.position), Is.LessThan(.01f));
            Call(graph, "SetGateOpen", 0, true); Mode("Chase");
            yield return new WaitForSeconds(.6f);
            Assert.That((bool)Get(motor, "HasRoute"), Is.True);
            Call(motor, "Stop"); held = drone.transform.position;
            Mode("Patrol"); Go(Vector3.right * 20f);
            Assert.That(Call(motor, "InitializeMovement", Settings("Scout")), Is.False);
            yield return new WaitForSeconds(.5f);
            Assert.That(State, Is.EqualTo("Stopped")); Assert.That(((Behaviour)motor).enabled, Is.False);
            Assert.That((float)Get(motor, "ActualSpeed"), Is.Zero);
            Assert.That(Vector3.Distance(held, drone.transform.position), Is.LessThan(.01f));
        }
    }
}
#endif
