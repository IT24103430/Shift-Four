#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class InterceptorMainLabPlayModeTests
    {
        private Component brain, graph, player, session;
        private Vector3[] nodes;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name)).Cast<Component>().ToArray();
        private static object Call(object target, string name, params object[] args)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(target, args);
            }
            throw new MissingMethodException(name);
        }
        private static object Get(object value, string property) => value.GetType().GetProperty(property).GetValue(value);
        private static void Set(object value, string field, object data) => value.GetType().GetField(field).SetValue(value, data);
        private Vector3[] Route(Vector3 a, Vector3 b) => (Vector3[])Call(brain, "ValidatedRoute", a, b);
        private int Version => (int)Get(graph, "Version");
        private object NewPolicy()
        {
            var settings = Activator.CreateInstance(T("InterceptorSettings"));
            return Activator.CreateInstance(T("InterceptorPolicy"), settings);
        }
        private object Tick(object policy, float time, bool visible, Vector3 position, Vector3? drone = null)
            => Call(policy, "Step", time, visible, position, drone ?? brain.transform.position,
                (Vector3)Get(brain, "Home"), brain.GetComponent<NavMeshAgent>().speed, Version, nodes,
                new Func<Vector3, Vector3, Vector3[]>(Route));
        private static Vector3 Destination(object decision) => (Vector3)decision.GetType().GetField("Destination").GetValue(decision);

        [UnitySetUp] public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = Find("PlayerController").Single(); session = Find("GameSession").Single();
            foreach (var b in Find("DroneBrain")) ((Behaviour)b).enabled = false;
            ((Behaviour)player).enabled = false;
            brain = Find("InterceptorBrain").Single(); graph = Find("WaypointGraph").Single();
            // Initialize the adapter's cached graph via the actual decision entrypoint.
            Call(brain, "Decide", false, Vector3.zero, float.PositiveInfinity);
            nodes = Enumerable.Range(0, (int)Get(graph, "NodeCount")).Select(i => (Vector3)Call(graph, "NodePosition", i)).ToArray();
            yield return new WaitForSeconds(.2f);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After interceptor test"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }
        private IEnumerator OpenGates()
        {
            var progress = Get(session, "Progress");
            for (int i = 0; i < 4; i++) { Call(progress, "DefeatDrone", i); Call(progress, "ActivateConsole", i); }
            yield return new WaitForSeconds(2f);
            for (int i = 0; i < 4; i++) Assert.That((bool)Call(graph, "IsGateOpen", i), Is.True);
        }
        // Find a genuine baked route and collision-safe staging point, without adding nodes.
        private void Scenario(out Vector3 source, out Vector3 heading)
        {
            var agent = brain.GetComponent<NavMeshAgent>();
            var body = brain.GetComponent(T("DroneBody"));
            foreach (Vector3 node in nodes)
            for (int angle = 0; angle < 360; angle += 45)
            {
                Vector3 forward = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 start = node - forward * 6f;
                Vector3 staging = node + Vector3.Cross(forward, Vector3.up) * 2.5f;
                if (!NavMesh.SamplePosition(staging, out var hit, .25f, agent.areaMask) ||
                    !(bool)Call(body, "CanOccupy", hit.position) || !(bool)Call(body, "CanOccupy", node)) continue;
                if (!agent.Warp(hit.position)) continue;
                Physics.SyncTransforms();
                var route = Route(start, node);
                if (route == null || Route(brain.transform.position, node) == null ||
                    route.Length != 2 || Vector3.Distance(route[0], route[1]) < 5.5f) continue;
                bool clearSight = true;
                for (int sample = 0; sample <= 7; sample++)
                {
                    Vector3 eye = start + forward * sample * .36f + Vector3.up * 1.3f;
                    Vector3 muzzle = brain.transform.position + Vector3.up * 1.25f;
                    Vector3 ray = eye - muzzle;
                    if (Physics.RaycastAll(muzzle, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore)
                        .Any(h => !h.collider.transform.IsChildOf(brain.transform) && !h.collider.transform.IsChildOf(player.transform)))
                    { clearSight = false; break; }
                }
                if (!clearSight) continue;
                source = start; heading = forward; return;
            }
            Assert.Fail("No suitable existing MainLab route found; inspect graph/clearance without changing scene");
            source = heading = default;
        }
        private object Train(Vector3 source, Vector3 heading, out object decision)
        {
            object policy = NewPolicy(); decision = null;
            for (int i = 0; i <= 6; i++) decision = Tick(policy, i * .3f, true, source + heading * i * .36f);
            return policy;
        }
        [UnityTest] public IEnumerator MainLab_RealRoutes_PredictReverseSearchAndReturnHome()
        {
            yield return OpenGates(); Scenario(out var source, out var heading);
            var policy = Train(source, heading, out var decision);
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("InterceptAhead"));
            Assert.That(Route(brain.transform.position, Destination(decision)), Is.Not.Null);
            Assert.That(Vector3.Dot(Destination(decision) - (source + heading * 2.16f), heading), Is.GreaterThan(1));
            Tick(policy, 2.1f, true, source + heading * 1.8f);
            Assert.That(Get(policy, "SelectedNode"), Is.EqualTo(-1));
            Tick(policy, 2.4f, false, Vector3.one * 10000);
            var last = (Vector3)Get(policy, "LastObserved");
            Tick(policy, 4.6f, false, Vector3.one * -10000, last);
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("SearchLastSeenArea"));
            Tick(policy, 10f, false, Vector3.one * 5000);
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("ReturnHome"));
            TestContext.Out.WriteLine("MainLab trace: InterceptAhead -> reversal/PursueLastKnown -> lost sight/SearchLastSeenArea -> expiry/ReturnHome");
        }
        [UnityTest] public IEnumerator MainLab_LockedGateIsRejected_ActualOpeningUpdatesRoutes()
        {
            // This fixture tests logical/native gate connectivity. Global room-centre node 2
            // is occupied by the Guard and nearby cover in the advanced scene: it is not
            // a valid raised-body starting point, even after its gate opens.
            var validator=Get(brain,"TacticalValidator");
            Func<Vector3,Vector3,Vector3[]> gateRoute=(a,b)=>(Vector3[])Call(validator,"PlayerRoute",a,b);
            Assert.That(gateRoute(nodes[2],nodes[3]),Is.Null,"Server-Control gate must block this edge");
            int before=Version;
            yield return OpenGates();
            Assert.That(Version,Is.GreaterThan(before));
            Assert.That(gateRoute(nodes[2],nodes[3]),Is.Not.Null);
            Assert.That((bool)Call(validator,"CanOccupy",nodes[2]),Is.False,"Occupied global endpoint must remain rejected for a wide drone");
            Assert.That(Route(nodes[2],nodes[3]),Is.Null,"Opening a gate must not override physical obstruction");
            var policy = NewPolicy();
            Tick(policy, 0f, true, nodes[2]);
            Call(graph, "SetGateOpen", 1, false);
            Assert.That(gateRoute(nodes[2], nodes[3]), Is.Null, "Graph gate access remains mandatory even on an open NavMesh");
            Tick(policy, .3f, false, Vector3.one * 1000);
            Assert.That(Get(policy, "SelectedNode"), Is.EqualTo(-1));
        }
        [UnityTest] public IEnumerator MainLab_WallOcclusionAndMovingHiddenPlayer_DoNotUpdateMemory()
        {
            yield return OpenGates(); Scenario(out var source, out var heading);
            var agent = brain.GetComponent<NavMeshAgent>(); agent.isStopped = true;
            ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
            // Place a real cover slab between drone muzzle and a close target.
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = source; cc.enabled = true;
            Vector3 sight = source - brain.transform.position; sight.y = 0;
            Call(player, "ApplyCameraPose"); brain.transform.rotation = Quaternion.LookRotation(sight);
            Physics.SyncTransforms();
            Assert.That((bool)Call(brain, "CanSeePlayer"), Is.True);
            Call(brain, "Decide", true, player.transform.position, 4f);
            var policy = Get(brain, "Policy"); var last = (Vector3)Get(policy, "LastObserved");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (brain.transform.position + source) * .5f + Vector3.up * 1.5f;
            wall.transform.rotation = Quaternion.LookRotation(sight);
            wall.transform.localScale = new Vector3(8, 4, .2f);
            Physics.SyncTransforms();
            Assert.That((bool)Call(brain, "CanSeePlayer"), Is.False);
            Call(brain, "Decide", false, player.transform.position, 4f);
            player.transform.position += Vector3.right * 50f; Physics.SyncTransforms();
            Call(brain, "Decide", false, player.transform.position, 50f);
            Assert.That((Vector3)Get(policy, "LastObserved"), Is.EqualTo(last));
            Object.Destroy(wall); yield return null;
        }
        [UnityTest] public IEnumerator MainLab_RaisedBodyObstacle_RejectsGroundNavMeshRoute()
        {
            yield return OpenGates(); Scenario(out var source, out var heading);
            Vector3 target = source + heading * 6f;
            Vector3 start = brain.transform.position;
            Assert.That(Route(start, target), Is.Not.Null);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = (start + target) * .5f + Vector3.up * 1.7f;
            obstacle.transform.rotation = Quaternion.LookRotation(target - start);
            obstacle.transform.localScale = new Vector3(4f, .3f, .05f);
            Physics.SyncTransforms();
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start, target, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Ground path remains complete below the new raised obstacle");
            Assert.That(Route(start, target), Is.Null, "Drone body clearance must also be required");
            Object.Destroy(obstacle); yield return null;
        }
        [UnityTest] public IEnumerator MainLab_ExpiredObservations_ReturnPhysicallyToControlHome()
        {
            Vector3 home = (Vector3)Get(brain, "Home");
            var agent = brain.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(home + Vector3.back * 4f), Is.True);
            Physics.SyncTransforms();
            object policy = NewPolicy();
            Tick(policy, 0f, true, home + Vector3.left * 4f);
            var decision = Tick(policy, 8f, false, Vector3.one * 99999);
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("ReturnHome"));
            Assert.That(Destination(decision), Is.EqualTo(home));
            Call(brain.GetComponent(T("DroneMotor")), "GoTo", Destination(decision));
            float began = Time.time;
            while (Time.time - began < 8f && Vector3.Distance(brain.transform.position, home) > 1.1f)
            {
                yield return null;
                Assert.That(agent.isOnNavMesh, Is.True);
                Assert.That((bool)Call(brain.GetComponent(T("DroneBody")), "CanOccupy", brain.transform.position), Is.True);
            }
            Assert.That(Vector3.Distance(brain.transform.position, home), Is.LessThanOrEqualTo(1.1f));
            Tick(policy, 9f, false, Vector3.one * -99999);
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("WatchHome"));
            TestContext.Out.WriteLine("Expired sighting -> actual motor return to assigned Control home -> WatchHome");
        }
        [UnityTest] public IEnumerator MainLab_GenuineVisibleMotion_DrivesActualBrainDecisions()
        {
            yield return OpenGates(); Scenario(out var source, out var heading);
            var agent = brain.GetComponent<NavMeshAgent>(); agent.isStopped = true;
            ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
            var controller = player.GetComponent<CharacterController>();
            object decision = null;
            for (int i = 0; i <= 7; i++)
            {
                controller.enabled = false;
                player.transform.position = source + heading * i * .36f;
                controller.enabled = true;
                Call(player, "ApplyCameraPose");
                brain.transform.rotation = Quaternion.LookRotation(player.transform.position - brain.transform.position);
                Physics.SyncTransforms();
                bool visible = (bool)Call(brain, "CanSeePlayer");
                Assert.That(visible, Is.True, "Controlled moving target must genuinely be visible");
                decision = Call(brain, "Decide", visible, player.transform.position, Vector3.Distance(brain.transform.position, player.transform.position));
                if (i < 7) yield return new WaitForSeconds(.3f);
            }
            var policy = Get(brain, "Policy");
            Assert.That(Get(policy, "State").ToString(), Is.EqualTo("InterceptAhead"));
            Assert.That(Route(brain.transform.position, Destination(decision)), Is.Not.Null);
            TestContext.Out.WriteLine("Actual InterceptorBrain: 8 raycast-confirmed moving observations -> reachable forward junction");
        }
        [UnityTest] public IEnumerator MainLab_BaselineComparison_IdenticalMotorRouteAttempts()
        {
            yield return OpenGates(); Scenario(out var source, out var heading);
            Vector3 start = brain.transform.position;
            var improved = Train(source, heading, out var decision);
            Assert.That(Get(improved, "State").ToString(), Is.EqualTo("InterceptAhead"));
            Vector3 observed = source + heading * 2.16f;
            Vector3 baseline = Baseline(start, observed, heading * 1.2f, out bool baselineIntercept);
            var targets = new[] { baseline, Destination(decision) };
            var names = new[] { "baseline", "predictive" };
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/InterceptorEvidence";
            Directory.CreateDirectory(directory);
            string csv = "policy,scenario,intercept_selected,route_distance,route_complete,seconds,nav_errors,replans,switches\n";
            for (int i = 0; i < 2; i++)
            {
                var agent = brain.GetComponent<NavMeshAgent>();
                var motor = brain.GetComponent(T("DroneMotor"));
                agent.Warp(start); Physics.SyncTransforms();
                var route = Route(start, targets[i]);
                if (i == 1) Assert.That(route, Is.Not.Null);
                float length = route == null ? float.NaN : 0f; for (int j = 1; route != null && j < route.Length; j++) length += Vector3.Distance(route[j - 1], route[j]);
                Call(motor, "GoTo", targets[i]); float began = Time.time; int errors = route == null ? 1 : 0;
                while (Time.time - began < 12f && Vector3.Distance(brain.transform.position, targets[i]) > 1.1f)
                {
                    yield return null;
                    if (!agent.isOnNavMesh || (!agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete) || !(bool)Call(brain.GetComponent(T("DroneBody")), "CanOccupy", brain.transform.position)) errors++;
                }
                bool completed = Vector3.Distance(brain.transform.position, targets[i]) <= 1.1f;
                csv += string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},straight,{1},{2:F3},{3},{4:F3},{5},{6},{7}\n",
                    names[i], i == 0 ? baselineIntercept : true, length, completed, Time.time - began, errors,
                    i == 0 ? 0 : (int)Get(improved, "Replans"), i == 0 ? 0 : (int)Get(improved, "Switches"));
                if (i == 1) Assert.That(completed, Is.True, "Predictive policy must complete its real motor route");
                if (i == 1) Assert.That(errors, Is.Zero);
            }
            File.WriteAllText(Path.Combine(directory, "mainlab-comparison.csv"), csv);
            TestContext.Out.WriteLine(csv);
        }
        // Frozen original Interceptor scoring, including its Euclidean distance term.
        private Vector3 Baseline(Vector3 drone, Vector3 source, Vector3 velocity, out bool intercepted)
        {
            float best = 34f; Vector3 result = source; intercepted = false;
            foreach (var node in nodes)
            {
                Vector3 from = node - source; from.y = 0f;
                float ahead = Vector3.Dot(from, velocity.normalized);
                if (ahead < 1f || ((IList)Call(graph, "FindRoute", drone, node)).Count == 0) continue;
                float score = 42f + Mathf.Min(ahead, 15f) * 1.5f - Vector3.Distance(drone, node) * .3f;
                if (score <= best) continue;
                best = score; result = node; intercepted = true;
            }
            return result;
        }
    }
}
#endif
