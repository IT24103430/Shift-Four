using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ShiftFour.Tests
{
    // Read-only checks: never save, bake, replace or apply prefab overrides.
    public sealed class FourRoomIntegrationEditModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static string Output => Environment.GetEnvironmentVariable("SHIFTFOUR_INTEGRATION_EVIDENCE") ?? "Logs/FourRoomIntegration";
        private static Component[] Components(GameObject root, string type) => root.GetComponentsInChildren(T(type), true).Cast<Component>().ToArray();
        private static void References(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
                foreach (var component in transform.GetComponents<Component>())
                {
                    Assert.That(component, Is.Not.Null, transform.name);
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && !property.objectReferenceEntityIdValue.Equals(default(EntityId)))
                            Assert.That(property.objectReferenceValue, Is.Not.Null, transform.name + ": " + property.propertyPath);
                }
            }
        }
        private static void Drone(GameObject root, string name)
        {
            foreach (string type in new[] { "DroneBrain", "DroneMotor", "DroneBody", "Health", "DroneRotorVisuals" })
                Assert.That(Components(root, type).Length, Is.EqualTo(1), name + " " + type);
            var brain = Components(root, "DroneBrain").Single();
            Assert.That(brain.GetType().Name, Is.EqualTo(name + "Brain"));
            Assert.That(((Behaviour)brain).enabled, Is.True);
            Assert.That(Components(root, "DroneHearingSensor").Length, Is.LessThanOrEqualTo(1), "Start adds a sensor only when absent");
            foreach (var sensor in Components(root, "DroneHearingSensor")) Assert.That(((Behaviour)sensor).enabled, Is.True);
            var visual = Components(root, "DroneRotorVisuals").Single();
            Assert.That(((Behaviour)visual).enabled, Is.True);
            Assert.That(visual.transform, Is.Not.EqualTo(brain.transform), "Model pose must not own the navigation root");
            Assert.That(root.GetComponentsInChildren<NavMeshAgent>(true).Length, Is.EqualTo(1));
            var motor = Components(root, "DroneMotor").Single();
            var reference = new SerializedObject(motor).FindProperty("visual").objectReferenceValue;
            Assert.That(reference == null || reference == visual.transform, Is.True, "Null references use the existing unambiguous runtime recovery");
            var profile = new SerializedObject(brain).FindProperty("difficultyProfile").objectReferenceValue;
            var expected = Resources.Load("DroneDifficulty/" + name, T("DroneDifficultyProfile"));
            Assert.That(expected, Is.Not.Null);
            Assert.That(profile == null || profile == expected, Is.True, "Null means the documented archetype default");
        }

        [TestCase("Scout")] [TestCase("Guard")] [TestCase("Interceptor")] [TestCase("Flanker")]
        public void SavedDronePrefab_HasValidReferencesAndSingleComponentOwners(string name)
        {
            string path = "Assets/Prefabs/Agents/" + name + ".prefab";
            byte[] before = File.ReadAllBytes(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { References(root); Drone(root, name); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [TestCase("Checkpoint", 0)] [TestCase("Server", 1)] [TestCase("Control", 2)] [TestCase("Storage", 3)]
        public void SavedSectionPrefab_HasValidReferencesAndCorrectConsoleIdentity(string name, int id)
        {
            string path = "Assets/Prefabs/Sections/" + name + ".prefab";
            byte[] before = File.ReadAllBytes(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                References(root);
                var console = Components(root, "ConsoleSwitch").Single();
                Assert.That(console.GetType().GetProperty("GateId").GetValue(console), Is.EqualTo(id));
                Assert.That(((Behaviour)console).enabled, Is.True);
                Assert.That(console.GetComponent<Collider>(), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test] public void SavedMainLab_ResolvesOverridesAndReferencesWithoutChangingAssets()
        {
            const string path = "Assets/Scenes/MainLab.unity";
            byte[] before = File.ReadAllBytes(path);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots) References(root);
                var brains = roots.SelectMany(r => Components(r, "DroneBrain")).ToArray();
                Assert.That(brains.Length, Is.EqualTo(4));
                string report = "Read-only Unity resolved scene/prefab audit\nMainLab GUID=" + AssetDatabase.AssetPathToGUID(path) + "\n";
                foreach (string name in new[] { "Scout", "Guard", "Interceptor", "Flanker" })
                {
                    var brain = brains.Single(b => b.GetType().Name == name + "Brain");
                    Drone(brain.gameObject, name);
                    report += name + " position=" + brain.transform.position.ToString("F3") + " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(brain) + "\n";
                }
                foreach (string type in new[] { "GameSession", "PlayerController", "ExitZone", "WaypointGraph" })
                    Assert.That(roots.Sum(r => Components(r, type).Length), Is.EqualTo(1), type);
                foreach (string type in new[] { "LabDoor", "ConsoleSwitch" })
                {
                    var all = roots.SelectMany(r => Components(r, type)).ToArray();
                    CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, all.Select(c => (int)c.GetType().GetProperty("GateId").GetValue(c)));
                }
                foreach (var instance in roots.Where(PrefabUtility.IsAnyPrefabInstanceRoot))
                {
                    report += "\nInstance " + instance.name + " source=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) + "\n";
                    foreach (var change in PrefabUtility.GetPropertyModifications(instance) ?? Array.Empty<PropertyModification>())
                        report += (change.target != null ? change.target.name : "missing target") + " | " + change.propertyPath + " | " + change.value + " | " + (change.objectReference != null ? change.objectReference.name : "null") + "\n";
                }
                Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), Is.True, "Restart must resolve MainLab in the build");
                Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "saved-assets.txt"), report);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
