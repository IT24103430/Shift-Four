using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class DroneMotorEditModeTests
    {
        // Runtime scripts remain in predefined Assembly-CSharp; no production asmdef changes.
        private Type type;
        private GameObject root;
        private Component motor;

        [SetUp]
        public void SetUp()
        {
            type = Type.GetType("ShiftFour.DroneMotor, Assembly-CSharp", true);
            root = new GameObject("Motor contract test");
            root.AddComponent<NavMeshAgent>();
            motor = root.AddComponent(type);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void NewMotor_HasNoGoalAndNoRoute()
        {
            Assert.That(State, Is.EqualTo("NoGoal"));
            Assert.That((bool)type.GetProperty("HasRoute").GetValue(motor), Is.False);
        }

        [Test]
        public void Stop_IsTerminalEvenIfAnotherGoalIsSubmitted()
        {
            Call("Stop");
            Call("GoTo", Vector3.right * 10f);
            Assert.That(State, Is.EqualTo("Stopped"));
            Assert.That((bool)type.GetProperty("HasRoute").GetValue(motor), Is.False);
        }

        [Test]
        public void NonFiniteGoal_IsNotSubmitted()
        {
            Call("GoTo", new Vector3(float.NaN, 0f, 0f));
            Call("GoTo", new Vector3(float.PositiveInfinity, 0f, 0f));
            Assert.That(State, Is.EqualTo("NoGoal"));
        }

        [Test]
        public void RejectedDestination_IsBlockedRatherThanPending()
        {
            type.GetMethod("DestinationRequested", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(motor, new object[] { false });
            Assert.That(State, Is.EqualTo("Blocked"));
            Assert.That((bool)type.GetProperty("HasRoute").GetValue(motor), Is.False);
        }

        [TestCase(true, NavMeshPathStatus.PathComplete, 0f, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathPartial, 0f, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathInvalid, 0f, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, float.PositiveInfinity, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, float.NaN, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, 0.5f, 1f, 1f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, 1.3f, 1f, 0f, true, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, 0.8f, 1f, 0f, true, true)]
        [TestCase(false, NavMeshPathStatus.PathComplete, 0.8f, 0.25f, 0f, false, false)]
        [TestCase(false, NavMeshPathStatus.PathComplete, 0.2f, 0.25f, 1f, false, true)]
        public void Arrival_RequiresAReadyCompletePathAndTheCorrectTolerance(bool pending,
            NavMeshPathStatus status, float remaining, float tolerance, float speedSquared, bool final, bool expected)
        {
            var method = type.GetMethod("CanArrive", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That((bool)method.Invoke(null,
                new object[] { pending, status, remaining, tolerance, speedSquared, final }), Is.EqualTo(expected));
        }

        [TestCase("Scout")] [TestCase("Guard")] [TestCase("Flanker")] [TestCase("Interceptor")]
        public void ExistingPrefab_KeepsItsMotorAndNavigationConfiguration(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent(type), Is.Not.Null);
            var agent = prefab.GetComponent<NavMeshAgent>();
            Assert.That(agent.speed, Is.EqualTo(3.5f));
            Assert.That(agent.acceleration, Is.EqualTo(8f));
            Assert.That(agent.stoppingDistance, Is.EqualTo(1f));
            Assert.That(agent.autoRepath, Is.True);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void Turning_RespectsAngularSpeedAcrossTimesteps(int framesPerSecond)
        {
            Quaternion rotation = Quaternion.identity;
            float dt = 1f / framesPerSecond;
            for (int i = 0; i < framesPerSecond; i++)
            {
                Quaternion next = Turn(rotation, Vector3.right, 7f, 120f, dt);
                Assert.That(Quaternion.Angle(rotation, next), Is.LessThanOrEqualTo(120f * dt + 0.002f));
                rotation = next;
            }
            Assert.That(Quaternion.Angle(rotation, Quaternion.LookRotation(Vector3.right)), Is.LessThan(5f));
        }

        [Test]
        public void Turning_HoldsAtRestAndKeepsTurnSpeedAsResponsiveness()
        {
            Quaternion start = Quaternion.Euler(0f, 20f, 0f);
            Assert.That(Turn(start, Vector3.zero, 7f, 120f, 0.1f), Is.EqualTo(start));
            Assert.That(Quaternion.Angle(start, Turn(start, Vector3.right, 0f, 120f, 0.1f)), Is.LessThan(0.01f));
            float slow = Quaternion.Angle(start, Turn(start, Vector3.right, 2f, 1000f, 0.1f));
            float fast = Quaternion.Angle(start, Turn(start, Vector3.right, 7f, 1000f, 0.1f));
            Assert.That(fast, Is.GreaterThan(slow));
        }

        private Quaternion Turn(Quaternion rotation, Vector3 heading, float response, float maximum, float dt)
            => (Quaternion)type.GetMethod("TurnTowards", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { rotation, heading, response, maximum, dt });

        private string State => type.GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(motor).ToString();
        private void Call(string name, params object[] args) => type.GetMethod(name).Invoke(motor, args);
    }
}
