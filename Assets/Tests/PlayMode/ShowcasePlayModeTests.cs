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
        public IEnumerator RecordedShowcase_ClearsFourRoomsDefusesBombAndWinsBeforeTimerExpires()
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
            float deadline = Time.time + 119f;
            while (!(bool)Get(session, "Ended") && Time.time < deadline)
            {
                for (int i = 0; i < drones.Length; i++)
                    decisions[i].Add(Get(drones[i], "CurrentDecision").ToString());
                yield return null;
            }
            Assert.That(Get(session, "Won"), Is.True, Get(session, "Outcome").ToString());
            Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(4));
            Assert.That(Get(session, "DronesRemaining"), Is.Zero);
            Assert.That(Get(session, "BombDefused"), Is.True);
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
