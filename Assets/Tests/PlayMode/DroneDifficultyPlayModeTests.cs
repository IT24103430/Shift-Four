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
    public sealed class DroneDifficultyPlayModeTests
    {
        private CursorLockMode cursor;
        private bool cursorVisible;
        private GameObject fixture;
        private Object overrideProfile;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object c, string name) => c.GetType().GetProperty(name).GetValue(c);
        private static object Call(object c, string name, params object[] args)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(c, args);
            }
            throw new MissingMethodException(name);
        }
        private static FieldInfo Field(object c, string name)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }
        private static Component[] Find(string n) => Object.FindObjectsByType(T(n), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static float HP(Component brain) => (float)Get(brain.GetComponent(T("Health")), "Current");
        private static float Maximum(Component brain) => (float)Get(brain.GetComponent(T("Health")), "Maximum");
        private static float Expected(Component brain)
        {
            switch (brain.GetType().Name)
            {
                case "ScoutBrain": return 100f;
                case "GuardBrain": return 150f;
                case "InterceptorBrain": return 125f;
                case "FlankerBrain": return 150f;
                default: throw new AssertionException("Unexpected brain");
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            // Standalone health fixtures must not unload the Test Runner's initial scene.
            SceneManager.SetActiveScene(SceneManager.CreateScene("Difficulty fixture " + Guid.NewGuid()));
            Debug.Log("[Difficulty QA] Starting " + TestContext.CurrentContext.Test.Name);
            yield return null;
        }
        private static IEnumerator MainLab()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var player in Find("PlayerController")) ((Behaviour)player).enabled = false;
            foreach (var brain in Find("DroneBrain"))
            {
                ((Behaviour)brain).enabled = false;
                ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (fixture != null) Object.Destroy(fixture);
            if (overrideProfile != null) Object.Destroy(overrideProfile);
            Scene old = SceneManager.GetActiveScene();
            Scene empty = SceneManager.GetSceneByName("After difficulty tests");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("After difficulty tests");
            SceneManager.SetActiveScene(empty);
            if (old != empty) yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            Debug.Log("[Difficulty QA] Finished " + TestContext.CurrentContext.Test.Name);
        }

        [UnityTest] public IEnumerator MainLab_FourProfilesInitializeHealthAndMovement_VisionAndWeaponsRemainUnchanged()
        {
            yield return MainLab();
            var brains = Find("DroneBrain"); Assert.That(brains.Length, Is.EqualTo(4));
            foreach (var brain in brains)
            {
                Assert.That(Get(brain, "DifficultyProfile"), Is.Not.Null, brain.name);
                Assert.That(HP(brain), Is.EqualTo(Expected(brain)), brain.name);
                Assert.That(Maximum(brain), Is.EqualTo(Expected(brain)));
                Assert.That((bool)Call(brain, "InitializeDifficulty"), Is.False);
                var agent = brain.GetComponent<NavMeshAgent>();
                var motor = brain.GetComponent(T("DroneMotor"));
                object settings = Get(brain, "DifficultySettings");
                float patrol = (float)settings.GetType().GetField("patrolSpeed").GetValue(settings);
                Assert.That((bool)Get(motor, "HasMovementProfile"), Is.True);
                float chase = (float)settings.GetType().GetField("chaseSpeed").GetValue(settings);
                Assert.That(agent.speed, Is.InRange(patrol, chase), "Startup binds profile limits; perception may already request chase");
                Assert.That((float)Get(motor, "CruiseSpeed"), Is.EqualTo(agent.speed));
                Assert.That(agent.acceleration, Is.EqualTo(8f)); Assert.That(agent.angularSpeed, Is.EqualTo(120f));
                Assert.That(agent.autoBraking, Is.True);
                Assert.That(Field(brain, "visionDistance").GetValue(brain), Is.EqualTo(15f));
                Assert.That(Field(brain, "attackRange").GetValue(brain), Is.EqualTo(11f));
                Assert.That(Field(brain, "attackInterval").GetValue(brain), Is.EqualTo(1.1f));
            }
            var player = Find("PlayerController").Single();
            Assert.That(Field(player, "walkSpeed").GetValue(player), Is.EqualTo(5f));
            Assert.That(PlayerWeaponTestActions.Data(player, "Damage"), Is.EqualTo(25f));
            Assert.That(PlayerWeaponTestActions.Data(player, "FireInterval"), Is.EqualTo(.24f));
            Assert.That(Maximum(player), Is.EqualTo(100f));
        }

        [UnityTest] public IEnumerator MainLab_AllFourDamagedDrones_ReapplicationCannotHealOrMutateSharedProfiles()
        {
            yield return MainLab();
            foreach (var brain in Find("DroneBrain"))
            {
                var profile = (Object)Get(brain, "DifficultyProfile"); string json = JsonUtility.ToJson(profile);
                var health = brain.GetComponent(T("Health")); Call(health, "TakeDamage", 25f);
                object record = Get(health, "LastDamage");
                for (int i = 0; i < 3; i++) Assert.That((bool)Call(brain, "InitializeDifficulty"), Is.False);
                Assert.That(HP(brain), Is.EqualTo(Expected(brain) - 25f));
                Assert.That(Maximum(brain), Is.EqualTo(Expected(brain)));
                Assert.That(Get(health, "LastDamage"), Is.EqualTo(record));
                Assert.That(Get(brain, "DifficultyProfile"), Is.SameAs(profile));
                Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(json));
            }
        }

        private Component FreshScout()
        {
            fixture = new GameObject("Fresh difficulty fixture"); fixture.SetActive(false);
            var agent = fixture.AddComponent<NavMeshAgent>(); agent.enabled = false;
            var brain = fixture.AddComponent(T("ScoutBrain"));
            ((Behaviour)fixture.GetComponent(T("DroneMotor"))).enabled = false;
            Field(fixture.GetComponent(T("Health")), "maximum").SetValue(fixture.GetComponent(T("Health")), 75f);
            return brain;
        }

        [UnityTest] public IEnumerator InspectorOverride_BindsOnlyThatInstanceAndLeavesDefaultAssetUntouched()
        {
            Component brain = FreshScout();
            var original = Resources.Load("DroneDifficulty/Scout", T("DroneDifficultyProfile"));
            string json = JsonUtility.ToJson(original);
            overrideProfile = Object.Instantiate(original);
            var stored = T("DroneDifficultyProfile").GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
            object data = stored.GetValue(overrideProfile);
            data.GetType().GetField("maximumHealth").SetValue(data, 175f); stored.SetValue(overrideProfile, data);
            Field(brain, "difficultyProfile").SetValue(brain, overrideProfile);
            fixture.SetActive(true); yield return null;
            Assert.That(Get(brain, "DifficultyProfile"), Is.SameAs(overrideProfile));
            Assert.That(HP(brain), Is.EqualTo(175f)); Assert.That(Maximum(brain), Is.EqualTo(175f));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(json));
        }

        [UnityTest] public IEnumerator DamageBeforeBrainStart_LateProfileBindingCannotRestoreHealth()
        {
            Component brain = FreshScout(); fixture.SetActive(true);
            Call(fixture.GetComponent(T("Health")), "TakeDamage", 25f);
            yield return null;
            Assert.That(Get(brain, "DifficultyProfile"), Is.Not.Null);
            Assert.That(HP(brain), Is.EqualTo(50f)); Assert.That(Maximum(brain), Is.EqualTo(75f));
            Assert.That((bool)Call(brain, "InitializeDifficulty"), Is.False);
        }

        [UnityTest] public IEnumerator NaturalRecovery_CannotRearmInitializationEvenAfterFullHealing()
        {
            fixture = new GameObject("Difficulty recovery fixture"); var health = fixture.AddComponent(T("Health"));
            Call(health, "Configure", 75f, true);
            Assert.That((bool)Call(health, "TryInitializeMaximum", 150f), Is.True);
            Field(health, "recoveryDelay").SetValue(health, 0f); Field(health, "recoveryPerSecond").SetValue(health, 1000f);
            Call(health, "TakeDamage", 25f);
            float until = Time.time + 1f;
            while ((float)Get(health, "Current") < 150f && Time.time < until) yield return null;
            Assert.That((float)Get(health, "Current"), Is.EqualTo(150f));
            Assert.That((bool)Call(health, "TryInitializeMaximum", 300f), Is.False);
            Assert.That((float)Get(health, "Maximum"), Is.EqualTo(150f));
            // Also protect late first binding after damage has naturally healed.
            Call(health, "Configure", 75f, true); Call(health, "TakeDamage", 1f);
            until = Time.time + 1f;
            while ((float)Get(health, "Current") < 75f && Time.time < until) yield return null;
            Assert.That((float)Get(health, "Current"), Is.EqualTo(75f));
            Assert.That((bool)Call(health, "TryInitializeMaximum", 150f), Is.False);
        }

        [UnityTest] public IEnumerator InactiveInitializationAndDamage_AwakeMustNotRestoreHealth()
        {
            fixture = new GameObject("Inactive initialization fixture"); fixture.SetActive(false);
            var health = fixture.AddComponent(T("Health")); Call(health, "Configure", 75f, false);
            Assert.That((bool)Call(health, "TryInitializeMaximum", 150f), Is.True);
            Call(health, "TakeDamage", 25f);
            Assert.That((float)Get(health, "Current"), Is.EqualTo(125f));
            fixture.SetActive(true); yield return null;
            Assert.That((float)Get(health, "Current"), Is.EqualTo(125f), "Awake cannot overwrite initialized/damaged health");
            Assert.That((bool)Call(health, "TryInitializeMaximum", 300f), Is.False);
        }

        [UnityTest] public IEnumerator MainLab_ActualLossAndRestart_RestoresAllProfileHealthAndClearsWrecks()
        {
            yield return MainLab();
            foreach (var brain in Find("DroneBrain")) Call(brain.GetComponent(T("Health")), "TakeDamage", 25f);
            var scout = Find("ScoutBrain").Single(); Call(scout.GetComponent(T("Health")), "TakeDamage", 10000f);
            yield return null;
            Assert.That(HP(scout), Is.Zero);
            var session = Find("GameSession").Single();
            Call(Find("PlayerController").Single().GetComponent(T("Health")), "TakeDamage", 10000f);
            Assert.That((bool)Get(session, "Ended"), Is.True); Call(session, "Restart");
            yield return null; yield return null;
            Assert.That(Find("DroneBrain").Length, Is.EqualTo(4));
            foreach (var brain in Find("DroneBrain"))
            {
                Assert.That(HP(brain), Is.EqualTo(Expected(brain)), brain.name);
                Assert.That(Maximum(brain), Is.EqualTo(Expected(brain)));
                Assert.That(Get(brain, "DifficultyProfile"), Is.Not.Null);
                Assert.That((bool)Call(brain, "InitializeDifficulty"), Is.False);
            }
            Assert.That(Find("DroneWreckMotion"), Is.Empty);
            session = Find("GameSession").Single();
            Assert.That(Get(session, "DronesRemaining"), Is.EqualTo(4));
            Assert.That(Get(session, "ConsolesOnline"), Is.EqualTo(0));
            Assert.That((bool)Get(session, "Ended"), Is.False);
        }
    }
}
#endif
