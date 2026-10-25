#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class ShowcasePlayModeTests
    {
        private Type soundType;
        private Action<Vector3> shotListener;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);

        [UnityTest]
        public IEnumerator Welcome_OpensNameEntryBeforeStartingMission()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var session = Object.FindAnyObjectByType(T("GameSession")) as Component;
            var phase = session.GetType().GetProperty("Phase");
            phase.SetValue(session, Enum.Parse(phase.PropertyType, "Welcome"));
            session.GetType().GetMethod("Begin").Invoke(session, new object[] { "PLAYER ONE" });
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Welcome"));
            session.GetType().GetMethod("OpenNameEntry").Invoke(session, null);
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("NameEntry"));
            session.GetType().GetMethod("Begin").Invoke(session, new object[] { "PLAYER ONE" });
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Intro"));
            Assert.That(Get(session, "CanPlay"), Is.False);
            var intro = (Component)Get(session, "Intro");
            Assert.That(Get(intro, "IsPlaying"), Is.True);
            intro.GetType().GetMethod("Finish").Invoke(intro, null);
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Countdown"));
            Assert.That(Get(session, "PlayerName"), Is.EqualTo("PLAYER ONE"));
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator WinningAtExit_PlaysAndSkipsSuppliedEnding()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var session = Object.FindAnyObjectByType(T("GameSession")) as Component;
            Assert.That(session, Is.Not.Null);
            object progress = Get(session, "Progress");
            for (int id = 0; id < 4; id++)
            {
                progress.GetType().GetMethod("ActivateConsole").Invoke(progress, new object[] { id });
                progress.GetType().GetMethod("DefeatDrone").Invoke(progress, new object[] { id });
            }
            var bomb = (Component)Object.FindAnyObjectByType(T("BombConsole"));
            bomb.GetType().GetMethod("Interact").Invoke(bomb, null);
            Assert.That(Get(session, "IsPlanting"), Is.True);
            session.GetType().GetMethod("TryExit").Invoke(session, null);
            Assert.That(Get(session, "Won"), Is.False, "Planting must finish before extraction.");
            yield return new WaitForSeconds(5.2f);
            Assert.That(Get(session, "BombPlanted"), Is.True);
            session.GetType().GetMethod("TryExit").Invoke(session, null);
            Assert.That(Get(session, "Won"), Is.True);
            var ending = Get(session, "Ending") as Component;
            Assert.That(ending, Is.Not.Null);
            Assert.That(Get(ending, "IsPlaying"), Is.True);
            var player = (Component)Get(session, "Player");
            var camera = (Camera)Get(player, "ViewCamera");
            Assert.That(camera.enabled, Is.False);
            ending.GetType().GetMethod("Finish").Invoke(ending, null);
            Assert.That(Get(ending, "IsPlaying"), Is.False);
            Assert.That(camera.enabled, Is.True);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator RecordedShowcase_ClearsFourRoomsPlantsBombAndWinsBeforeTimerExpires()
        {
            float originalScale = Time.timeScale;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var session = Object.FindAnyObjectByType(T("GameSession")) as Component;
            Assert.That(session, Is.Not.Null);
            int acceptedShots = 0;
            soundType = T("SoundEvents");
            shotListener = _ => acceptedShots++;
            soundType.GetEvent("ShotFired").AddEventHandler(null, shotListener);
            var drones = new Component[]
            {
                Object.FindAnyObjectByType(T("ScoutBrain")) as Component,
                Object.FindAnyObjectByType(T("GuardBrain")) as Component,
                Object.FindAnyObjectByType(T("InterceptorBrain")) as Component,
                Object.FindAnyObjectByType(T("FlankerBrain")) as Component
            };
            var decisions = new HashSet<string>[drones.Length];
            for (int i = 0; i < drones.Length; i++)
            {
                Assert.That(drones[i], Is.Not.Null);
                decisions[i] = new HashSet<string>();
            }
            session.GetType().GetMethod("BeginShowcase").Invoke(session, new object[] { "DEMO" });
            Time.timeScale = 4f; // Keep the complete mission rules while shortening the test's wall time.
            float started = Time.time;
            float deadline = Time.time + 128f;
            while (!(bool)Get(session, "Ended") && Time.time < deadline)
            {
                for (int i = 0; i < drones.Length; i++)
                    decisions[i].Add(Get(drones[i], "CurrentDecision").ToString());
                yield return null;
            }
            Assert.That(Get(session, "Won"), Is.True, Get(session, "Outcome").ToString());
            var ending = Get(session, "Ending") as Component;
            Assert.That(ending, Is.Not.Null, "Winning in MainLab must start the in-game ending.");
            Assert.That(Get(ending, "IsPlaying"), Is.True);
            var gameplayCamera = (Camera)Get((Component)Get(session, "Player"), "ViewCamera");
            Assert.That(gameplayCamera.enabled, Is.False, "The ending camera must take over the view.");
            ending.GetType().GetMethod("Finish").Invoke(ending, null);
            Assert.That(gameplayCamera.enabled, Is.True, "Finishing the ending restores the gameplay camera.");
            Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(4));
            Assert.That(Get(session, "DronesRemaining"), Is.Zero);
            Assert.That(Get(session, "BombPlanted"), Is.True);
            Assert.That(acceptedShots, Is.GreaterThanOrEqualTo(4), "The demo must show actual pistol shots.");
            for (int i = 0; i < drones.Length; i++)
            {
                bool acted = false;
                foreach (string decision in decisions[i])
                    if (decision != "Idle") acted = true;
                Assert.That(acted, Is.True, drones[i].name + " must visibly enter an AI decision.");
                object lastDamage = Get(drones[i].GetComponent(T("Health")), "LastDamage");
                string reason = lastDamage.GetType().GetField("Reason").GetValue(lastDamage).ToString();
                Assert.That(reason, Is.EqualTo("Player hitscan shot"),
                    drones[i].name + " should fall to actual pistol hits in the standard route.");
            }
            var player = (Component)Get(session, "Player");
            Assert.That(Get(player.GetComponent(T("Health")), "IsAlive"), Is.True);
            TestContext.Out.WriteLine("Showcase simulated seconds=" + (Time.time - started) +
                ", accepted shots=" + acceptedShots);
            Time.timeScale = originalScale;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (soundType != null && shotListener != null)
                soundType.GetEvent("ShotFired").RemoveEventHandler(null, shotListener);
            Time.timeScale = 1f;
            Scene current = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After recorded showcase"));
            if (current.isLoaded) yield return SceneManager.UnloadSceneAsync(current);
        }
    }
}
#endif
