using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class WaypointGraphTests
    {
        // Test asmdefs cannot reference predefined Assembly-CSharp. Reflection keeps the
        // production assembly layout intact while exercising its actual public methods.
        private Type graphType;
        private Type edgeType;
        private Component graph;
        private GameObject root;
        private Scene testScene;
        private NavMeshData navData;
        private NavMeshDataInstance navInstance;
        private static readonly Vector3 Offset = new Vector3(1000f, 0f, 1000f);

        [SetUp]
        public void SetUp()
        {
            graphType = Type.GetType("ShiftFour.WaypointGraph, Assembly-CSharp", true);
            edgeType = Type.GetType("ShiftFour.GraphEdge, Assembly-CSharp", true);
            testScene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Isolated navigation test");
            SceneManager.MoveGameObjectToScene(root, testScene);
            graph = root.AddComponent(graphType);
        }

        [TearDown]
        public void TearDown()
        {
            if (navInstance.valid) navInstance.Remove();
            if (navData != null) Object.DestroyImmediate(navData);
            Object.DestroyImmediate(root);
            if (testScene.IsValid()) EditorSceneManager.ClosePreviewScene(testScene);
        }

        [Test]
        public void StartEqualsGoal_ReturnsSuccessfulStationaryRoute()
        {
            Configure(new[] { Vector3.zero });
            CollectionAssert.AreEqual(new[] { Offset }, Route(Vector3.zero, Vector3.zero));
        }

        [Test]
        public void OpenRoute_ReturnsPointsInTravelOrder()
        {
            var a = new Vector3(0f, 0f, 5f);
            var b = new Vector3(5f, 0f, 5f);
            Configure(new[] { Vector3.zero, a, b }, Edge(0, 1), Edge(1, 2));
            CollectionAssert.AreEqual(new[] { Offset + a, Offset + b }, Route(Vector3.zero, b));
            CollectionAssert.AreEqual(new[] { Offset + a, Offset }, Route(b, Vector3.zero));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void ClosedGate_BlocksItsEdge(int gate)
        {
            var end = new Vector3(5f, 0f, 0f);
            Configure(new[] { Vector3.zero, end }, Edge(0, 1, gate));
            Assert.That(Route(Vector3.zero, end), Is.Empty);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void GateOpens_OnlyItsOwnEdgeBecomesTraversable(int gate)
        {
            var middle = new Vector3(5f, 0f, 0f);
            var end = new Vector3(10f, 0f, 0f);
            Configure(new[] { Vector3.zero, middle, end },
                Edge(0, 1, gate), Edge(1, 2, (gate + 1) % 4));
            Invoke("SetGateOpen", gate, true);
            CollectionAssert.AreEqual(new[] { Offset + middle }, Route(Vector3.zero, middle));
            Assert.That(Route(Vector3.zero, end), Is.Empty);
        }

        [Test]
        public void DisconnectedGoal_ReturnsNoPath()
        {
            var end = new Vector3(10f, 0f, 0f);
            Configure(new[] { Vector3.zero, Vector3.forward * 5f, end }, Edge(0, 1));
            Assert.That(Route(Vector3.zero, end), Is.Empty);
        }

        [Test]
        public void Reconstruction_PreservesLaterNodeNearOrigin()
        {
            var a = Vector3.forward * 5f;
            var b = Vector3.forward * 0.75f;
            var end = Vector3.right * 5f;
            Configure(new[] { Vector3.zero, a, b, end }, Edge(0, 1), Edge(1, 2), Edge(2, 3));
            CollectionAssert.AreEqual(new[] { Offset + a, Offset + b, Offset + end },
                Route(Vector3.zero, end));
        }

        [Test]
        public void Version_ChangesOncePerGateTransitionAndNotDuringSearch()
        {
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, Edge(0, 1, 0));
            int initial = Version;
            Route(Vector3.zero, Vector3.right * 5f);
            Invoke("SetGateOpen", 0, false);
            Assert.That(Version, Is.EqualTo(initial));
            Invoke("SetGateOpen", 0, true);
            Assert.That(Version, Is.EqualTo(initial + 1));
            Invoke("SetGateOpen", 0, true);
            Route(Vector3.zero, Vector3.right * 5f);
            Assert.That(Version, Is.EqualTo(initial + 1));
            Invoke("SetGateOpen", 0, false);
            Assert.That(Version, Is.EqualTo(initial + 2));
            Assert.That(Route(Vector3.zero, Vector3.right * 5f), Is.Empty);
        }

        [Test]
        public void AlternativeRoute_UsesShortestOpenPathAndDetoursAroundLockedEdge()
        {
            var detour = Vector3.forward * 10f;
            var shortcut = Vector3.right * 5f;
            var end = Vector3.right * 10f;
            Configure(new[] { Vector3.zero, detour, shortcut, end },
                Edge(0, 1), Edge(1, 3), Edge(0, 2, 2), Edge(2, 3));
            CollectionAssert.AreEqual(new[] { Offset + detour, Offset + end }, Route(Vector3.zero, end));
            Invoke("SetGateOpen", 2, true);
            CollectionAssert.AreEqual(new[] { Offset + shortcut, Offset + end }, Route(Vector3.zero, end));
        }

        [Test]
        public void CheaperParentFoundLater_ReplacesEarlierParent()
        {
            var first = new Vector3(4f, 0f, 1f);
            var better = new Vector3(2f, 0f, 3f);
            var shared = new Vector3(5f, 0f, 4f);
            var end = new Vector3(10f, 0f, 0f);
            Configure(new[] { Vector3.zero, first, better, shared, end },
                Edge(0, 1), Edge(0, 2), Edge(1, 3), Edge(2, 3), Edge(3, 4));
            CollectionAssert.AreEqual(new[] { Offset + better, Offset + shared, Offset + end },
                Route(Vector3.zero, end));
        }

        [Test]
        public void Configure_InvalidatesVersionAndResetsGateStates()
        {
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, Edge(0, 1, 1));
            Invoke("SetGateOpen", 1, true);
            int before = Version;
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, Edge(0, 1, 1));
            Assert.That(Version, Is.EqualTo(before + 1));
            Assert.That(Route(Vector3.zero, Vector3.right * 5f), Is.Empty);
        }

        [Test]
        public void NullNodes_AreSkippedWithoutThrowing()
        {
            Configure(new[] { Vector3.zero, Vector3.right * 5f });
            var nodes = (Transform[])graphType.GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(graph);
            nodes[0] = null;
            CollectionAssert.AreEqual(new[] { Offset + Vector3.right * 5f },
                Route(Vector3.right * 5f, Vector3.right * 5f));
            nodes[1] = null;
            Assert.That(Route(Vector3.zero, Vector3.zero), Is.Empty);
        }

        [Test]
        public void InvalidSerializedGate_DoesNotBecomeUnrestricted()
        {
            object invalid = Edge(0, 1);
            edgeType.GetField("gateId").SetValue(invalid, -2);
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, invalid);
            Assert.That(Route(Vector3.zero, Vector3.right * 5f), Is.Empty);
        }

        [Test]
        public void InvalidEdgeIndices_AreIgnored()
        {
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, Edge(0, 99), Edge(-1, 1));
            Assert.That(Route(Vector3.zero, Vector3.right * 5f), Is.Empty);
        }

        [Test]
        public void NonFiniteQuery_ReturnsNoPath()
        {
            Configure(new[] { Vector3.zero });
            Assert.That(Route(Vector3.zero, new Vector3(float.NaN, 0f, 0f)), Is.Empty);
            Assert.That(Route(new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.zero), Is.Empty);
        }

        [Test]
        public void EmptyGraph_ReturnsNoPath()
        {
            Configure(Array.Empty<Vector3>());
            Assert.That(Route(Vector3.zero, Vector3.right), Is.Empty);
        }

        [Test]
        public void SameNearestNodeAcrossBlockedPassage_ReturnsNoPath()
        {
            BuildFloor(true);
            var left = Vector3.left * 3f;
            var right = Vector3.right * 3f;
            Configure(new[] { right, Vector3.left * 20f }, Edge(0, 1, 3));
            Assert.That(Route(left, right), Is.Empty);
            Assert.That(Route(right, left), Is.Empty);
        }

        [Test]
        public void SameNodeWithConnectedLocalEndpoints_ReturnsReachableDestination()
        {
            BuildFloor(false);
            Configure(new[] { Vector3.zero });
            var route = Route(Vector3.left * 2f, Vector3.right * 2f);
            Assert.That(route, Is.Not.Empty);
            Assert.That(route[route.Count - 1], Is.EqualTo(Offset + Vector3.right * 2f));
        }

        [Test]
        public void SameNodeWithoutNavMesh_DoesNotInventConnection()
        {
            Configure(new[] { Vector3.zero });
            Assert.That(Route(Vector3.left, Vector3.right), Is.Empty);
        }

        [Test]
        public void DestinationNearGoalNode_IsStillTheRequestedEndpoint()
        {
            BuildFloor(false);
            Configure(new[] { Vector3.zero, Vector3.right * 5f }, Edge(0, 1));
            var destination = Vector3.right * 5.2f;
            var route = Route(Vector3.zero, destination);
            CollectionAssert.AreEqual(new[] { Offset + Vector3.right * 5f, Offset + destination }, route);
        }

        [Test]
        public void SameNodeOnOtherIsland_DoesNotForceLocalMovementThroughThatNode()
        {
            BuildFloor(true);
            Configure(new[] { Vector3.right * 3f });
            CollectionAssert.AreEqual(new[] { Offset + Vector3.left * 3f },
                Route(Vector3.left * 4f, Vector3.left * 3f));
        }

        [Test]
        public void DisconnectedEndpointConnectors_ReturnNoPathInEitherDirection()
        {
            BuildFloor(true);
            var left = Vector3.left * 3f;
            var nearGap = Vector3.left * 2f;
            var right = Vector3.right * 3f;
            Configure(new[] { left, nearGap }, Edge(0, 1));
            Assert.That(Route(left, right), Is.Empty);
            Assert.That(Route(right, left), Is.Empty);
        }

        [Test]
        public void OffMeshDestination_IsNotAppendedAsDirectPath()
        {
            BuildFloor(false);
            Configure(new[] { Vector3.zero });
            Assert.That(Route(Vector3.zero, Vector3.right * 30f), Is.Empty);
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)] [TestCase(3, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)] [TestCase(3, true)]
        public void NearestWaypointAcrossLockedGate_UsesReachableAttachment(int gate, bool reverse)
        {
            BuildGateFloors();
            var post = Vector3.right * 8f;
            var acrossGate = Vector3.left * 2f;
            var alternative = Vector3.right * 6f;
            var target = Vector3.right * 1.8f;
            Configure(new[] { post, acrossGate, alternative }, Edge(0, 2), Edge(2, 1, gate));

            // The target is closer to acrossGate, but can only reach waypoints on its own floor.
            Assert.That(Vector3.Distance(target, acrossGate), Is.LessThan(Vector3.Distance(target, alternative)));
            AssertConnected(post, target, true);
            AssertConnected(acrossGate, target, false);
            int before = Version;
            Vector3 origin = reverse ? target : post;
            Vector3 destination = reverse ? post : target;
            CollectionAssert.AreEqual(new[] { Offset + alternative, Offset + destination },
                Route(origin, destination));
            Assert.That(Version, Is.EqualTo(before));
            Assert.That((bool)Invoke("IsGateOpen", gate), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void AlternativeAttachment_CannotBypassLockedGraphEdge(int gate)
        {
            BuildGateFloors();
            var post = Vector3.left * 8f;
            var left = Vector3.left * 2f;
            var right = Vector3.right * 6f;
            var target = Vector3.right * 1.8f;
            Configure(new[] { post, left, right }, Edge(0, 1), Edge(1, 2, gate));
            AssertConnected(post, target, false);
            Assert.That(Route(post, target), Is.Empty);
            Assert.That(Route(target, post), Is.Empty);
        }

        private int Version => (int)graphType.GetProperty("Version").GetValue(graph);

        private object Invoke(string method, params object[] args)
            => graphType.GetMethod(method).Invoke(graph, args);

        private List<Vector3> Route(Vector3 origin, Vector3 destination)
            => (List<Vector3>)Invoke("FindRoute", Offset + origin, Offset + destination);

        private object Edge(int from, int to, int gate = -1)
            => Activator.CreateInstance(edgeType, new object[] { from, to, gate });

        private void Configure(Vector3[] positions, params object[] edges)
        {
            var nodes = new Transform[positions.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new GameObject("Node " + i).transform;
                nodes[i].SetParent(root.transform);
                nodes[i].position = Offset + positions[i];
            }
            Array connections = Array.CreateInstance(edgeType, edges.Length);
            for (int i = 0; i < edges.Length; i++) connections.SetValue(edges[i], i);
            Invoke("Configure", nodes, connections);
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
            BuildNavMesh(sources);
        }

        private void BuildGateFloors()
            => BuildNavMesh(new List<NavMeshBuildSource>
            {
                Floor(Vector3.left * 5f, new Vector3(8f, 0.2f, 8f)),
                Floor(Vector3.right * 5f, new Vector3(8f, 0.2f, 8f))
            });

        private static void AssertConnected(Vector3 from, Vector3 to, bool expected)
        {
            Assert.That(NavMesh.SamplePosition(Offset + from, out NavMeshHit start, 0.25f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(Offset + to, out NavMeshHit end, 0.25f, NavMesh.AllAreas), Is.True);
            var path = new NavMeshPath();
            bool connected = NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete;
            Assert.That(connected, Is.EqualTo(expected), "Validate the fixture's physical connectivity.");
        }

        private void BuildNavMesh(List<NavMeshBuildSource> sources)
        {
            navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(Offset, Vector3.one * 50f), Vector3.zero, Quaternion.identity);
            Assert.That(navData, Is.Not.Null, "In-memory test NavMesh must build.");
            navInstance = NavMesh.AddNavMeshData(navData);
            Assert.That(NavMesh.SamplePosition(Offset + Vector3.right * 3f, out _, 0.5f, NavMesh.AllAreas), Is.True);
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
