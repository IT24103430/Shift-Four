#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class PlayerNoisePlayModeTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private CursorLockMode cursor;
        private bool cursorVisible;
        private static readonly Vector3 Origin = new Vector3(1000, 0, 1000);
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
        private static object NoiseField(NoiseListenerProbe probe, string name) => probe.LastNoise.GetType().GetField(name).GetValue(probe.LastNoise);
        private static void ResetBus() => T("SoundEvents").GetMethod("ResetForPlaySession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            ResetBus(); owned.Clear();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Player noise fixture " + Guid.NewGuid()));
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in owned) if (go != null) Object.Destroy(go);
            ResetBus();
            Scene old = SceneManager.GetActiveScene();
            Scene empty = SceneManager.GetSceneByName("After player noise tests");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("After player noise tests");
            SceneManager.SetActiveScene(empty);
            if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        private GameObject Own(string name) { var go = new GameObject(name); owned.Add(go); return go; }
        private GameObject Box(string name, Vector3 position, Vector3 size, bool trigger = false)
        {
            var go = Own(name); go.transform.position = position;
            var box = go.AddComponent<BoxCollider>(); box.size = size; box.isTrigger = trigger; return go;
        }
        private NoiseListenerProbe Probe() => Own("Action observer").AddComponent<NoiseListenerProbe>();
        private Component Player(bool floor = true, float height = 0f)
        {
            if (floor) Box("Walkable floor", Origin + Vector3.down * .5f, new Vector3(200, 1, 200));
            var go = Own("Player"); go.transform.position = Origin + Vector3.up * height;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f;
            cc.slopeLimit = 50f; cc.stepOffset = .3f; cc.skinWidth = .05f; cc.minMoveDistance = 0f;
            var player = go.AddComponent(T("PlayerController"));
            // Invoke the same MovePlayer path as Update, with real CharacterController collisions and no input simulation.
            ((Behaviour)player).enabled = false;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) Move(player, Vector3.zero);
            return player;
        }
        private static void Move(Component player, Vector3 direction, float dt = 1f / 60f) => Call(player, "MovePlayer", direction, dt);

        [UnityTest] public IEnumerator StationaryGroundedController_ProducesNoSteps()
        {
            var player = Player(); var probe = Probe();
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            for (int i = 0; i < 600; i++) Move(player, Vector3.zero);
            Assert.That(probe.Noises, Is.Zero); Assert.That(probe.Shots, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator ActualWalking_EmitsBoundedStepsAtResolvedPosition()
        {
            var player = Player(); var probe = Probe(); Vector3 start = player.transform.position;
            int lastStep = -1000; string trace = "sample,elapsed_simulated_seconds,z,events\n";
            for (int i = 0; i < 600; i++)
            {
                int before = probe.Noises; Move(player, Vector3.forward);
                Assert.That(probe.Noises - before, Is.InRange(0, 1));
                if (probe.Noises != before)
                {
                    Assert.That((i - lastStep) / 60f, Is.GreaterThanOrEqualTo(.22f)); lastStep = i;
                    Assert.That(NoiseField(probe, "Category").ToString(), Is.EqualTo("Footstep"));
                    Assert.That(NoiseField(probe, "Intensity"), Is.EqualTo(.2f));
                    Assert.That(NoiseField(probe, "Position"), Is.EqualTo(player.transform.position));
                    Assert.That(NoiseField(probe, "Source"), Is.SameAs(player));
                    trace += $"{i},{(i + 1) / 60f},{player.transform.position.z},{probe.Noises}\n";
                }
            }
            Assert.That(player.transform.position.z - start.z, Is.EqualTo(50f).Within(.1f));
            Assert.That(probe.Noises, Is.InRange(29, 32)); Assert.That(probe.Shots, Is.Zero);
            Write("player-walking.csv", trace);
            yield return null;
        }

        [UnityTest] public IEnumerator WalkingIntoWall_UsesActualDisplacementAndThenRemainsSilent()
        {
            var player = Player(); Box("Blocking wall", Origin + Vector3.forward * 2f + Vector3.up, new Vector3(10, 3, 1));
            Physics.SyncTransforms(); var probe = Probe();
            for (int i = 0; i < 600; i++) Move(player, Vector3.forward);
            Assert.That(player.transform.position.z - Origin.z, Is.InRange(.8f, 1.3f));
            Assert.That(probe.Noises, Is.Zero, "Requested input against a wall must not count as walked distance");
            yield return null;
        }

        [UnityTest] public IEnumerator AirborneAndTriggerOnlySupport_StaySilent()
        {
            var player = Player(false, 20f);
            Box("Trigger is not ground", Origin + Vector3.up * 19f, new Vector3(200, 1, 200), true);
            Physics.SyncTransforms(); var probe = Probe();
            for (int i = 0; i < 60; i++) Move(player, Vector3.forward);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.False);
            Assert.That(player.transform.position.z - Origin.z, Is.GreaterThan(4f));
            Assert.That(probe.Noises, Is.Zero);
            yield return null;
        }

        [UnityTest] public IEnumerator FallingAndLanding_DoNotEmitCatchupSteps()
        {
            var player = Player(true, 3f); var probe = Probe();
            int iterations = 0;
            while (!player.GetComponent<CharacterController>().isGrounded && iterations++ < 120)
                Move(player, Vector3.forward);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(probe.Noises, Is.Zero);
            for (int i = 0; i < 10; i++) Move(player, Vector3.forward);
            Assert.That(probe.Noises, Is.Zero, "Landing does not preserve airborne distance");
            for (int i = 0; i < 20; i++) Move(player, Vector3.forward);
            Assert.That(probe.Noises, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator Console_EmitsOnceOnlyAfterSuccessfulActivation()
        {
            var session = Own("Mission").AddComponent(T("GameSession")); ((Behaviour)session).enabled = false;
            var console = Own("Console").AddComponent(T("ConsoleSwitch"));
            console.transform.position = Origin; Call(console, "Configure", "Checkpoint", null);
            var probe = Probe(); Call(console, "Interact");
            Assert.That(Get(console, "Activated"), Is.True); Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(1));
            Assert.That(probe.Noises, Is.EqualTo(1)); Assert.That(probe.Shots, Is.Zero);
            Assert.That(NoiseField(probe, "Category").ToString(), Is.EqualTo("ConsoleInteraction"));
            Assert.That(NoiseField(probe, "Intensity"), Is.EqualTo(.4f));
            Assert.That(NoiseField(probe, "Position"), Is.EqualTo(Origin)); Assert.That(NoiseField(probe, "Source"), Is.SameAs(console));
            Call(console, "Interact"); Call(console, "Interact");
            Assert.That(probe.Noises, Is.EqualTo(1)); Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest] public IEnumerator Console_RejectsMissingSessionInvalidIdentityAndEndedMissionSilently()
        {
            var console = Own("Console").AddComponent(T("ConsoleSwitch"));
            Call(console, "Configure", "Checkpoint", null); var probe = Probe();
            Call(console, "Interact"); Assert.That(Get(console, "Activated"), Is.False);
            var session = Own("Mission").AddComponent(T("GameSession")); ((Behaviour)session).enabled = false;
            Call(console, "Configure", "Unknown", null); Call(console, "Interact");
            Assert.That(Get(console, "Activated"), Is.False);
            Call(console, "Configure", "Checkpoint", null); Call(session, "Finish", "Fixture mission ended"); Call(console, "Interact");
            Assert.That(Get(console, "Activated"), Is.False); Assert.That(probe.Noises, Is.Zero);
            yield return null;
        }

        private static void Write(string name, string content)
        {
            string path = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/PlayerNoiseEvidence";
            Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, name), content);
        }
    }
}
#endif
