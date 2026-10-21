using System;
using System.IO;
using System.Linq;
using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Explicit, narrowly scoped migration. No scene generation, visual replacement or NavMesh baking.
public static class ShiftFourProximityFix
{
    private const string ScenePath = "Assets/Scenes/MainLab.unity";
    private static readonly string[] Names = { "Scout", "Guard", "Flanker", "Interceptor" };

    [MenuItem("Tools/Shift Four/Apply Camera and Drone Clearance Fix")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
        var paths = Names.Select(n => "Assets/Prefabs/Agents/" + n + ".prefab").Prepend(ScenePath).ToArray();
        string backup = "Library/ProximityFixBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        foreach (string path in paths)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup + "/" + path));
            File.Copy(path, backup + "/" + path);
        }
        try
        {
            foreach (string path in paths.Skip(1))
            {
                string original = File.ReadAllText(path);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureDrone(root);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new IOException("Could not save " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // Preserve serialized teammate policy fields unknown to this C# branch.
                ShiftFourVisualUpgrade.PreserveGameplayYaml(original, path, true);
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var player = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerController>(true)).Single();
            var playerData = new SerializedObject(player);
            playerData.FindProperty("eyeHeight").floatValue = 1.3f;
            playerData.FindProperty("nearClip").floatValue = .05f;
            playerData.ApplyModifiedPropertiesWithoutUndo();
            player.ApplyCameraPose();
            EditorUtility.SetDirty(player);
            foreach (var motor in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DroneMotor>(true)))
                ConfigureDrone(motor.gameObject);
            Physics.SyncTransforms();
            // The authored Flanker spawn intersects Fixed cover 2 after the flying model upgrade.
            // Move only this known spawn; leave home, cover points and waypoint data intact.
            var flanker = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FlankerBrain>(true)).Single();
            if ((flanker.transform.position - new Vector3(10f, 0f, -7f)).sqrMagnitude < .001f)
            {
                Vector3 clearSpawn = new Vector3(10f, 0f, -5.6f);
                if (!NavMesh.SamplePosition(clearSpawn, out NavMeshHit hit, .1f, NavMesh.AllAreas) ||
                    !flanker.GetComponent<DroneBody>().CanOccupy(hit.position))
                    throw new InvalidOperationException("Flanker replacement spawn is not clear.");
                flanker.transform.position = hit.position;
                Record(flanker.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("MainLab save failed.");
            AssetDatabase.SaveAssets();
            Debug.Log("PROXIMITY_FIX_APPLIED backup=" + backup);
        }
        catch
        {
            foreach (string path in paths) File.Copy(backup + "/" + path, path, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            throw;
        }
    }

    private static void Record(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }

    public static void ConfigureDrone(GameObject root)
    {
        var motor = root.GetComponent<DroneMotor>();
        var motorData = new SerializedObject(motor);
        Transform visual = (Transform)motorData.FindProperty("visual").objectReferenceValue;
        if (visual == null || root.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("Unexpected drone visual or scale: " + root.name);
        motorData.FindProperty("combatStandoff").floatValue = 2.8f;
        motorData.ApplyModifiedPropertiesWithoutUndo();
        Record(motor);
        var brain = root.GetComponent<DroneBrain>();
        var brainData = new SerializedObject(brain);
        brainData.FindProperty("combatOriginHeight").floatValue = visual.localPosition.y + .2f;
        brainData.ApplyModifiedPropertiesWithoutUndo();
        Record(brain);
        // Measure actual imported vertices in root space; the rotational envelope includes rotor sweep.
        Bounds bounds = default;
        bool first = true;
        float radius = 0f;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Vector3 rotorCenter = root.transform.InverseTransformPoint(filter.transform.TransformPoint(filter.sharedMesh.bounds.center));
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
                float reach = new Vector2(point.x, point.z).magnitude;
                if (filter.name.StartsWith("Rotor"))
                    reach = new Vector2(rotorCenter.x, rotorCenter.z).magnitude +
                        new Vector2(point.x - rotorCenter.x, point.z - rotorCenter.z).magnitude;
                radius = Mathf.Max(radius, reach);
            }
            var hitbox = filter.GetComponent<MeshCollider>();
            if (hitbox == null) hitbox = filter.gameObject.AddComponent<MeshCollider>();
            hitbox.sharedMesh = filter.sharedMesh;
            Record(hitbox);
        }
        if (first) throw new InvalidOperationException("Drone has no mesh: " + root.name);
        // Include hover travel and a small rotor/physics margin without moving the NavMesh root.
        var body = root.GetComponent<DroneBody>();
        if (body == null) body = root.AddComponent<DroneBody>();
        var bodyData = new SerializedObject(body);
        bodyData.FindProperty("envelopeCenter").vector3Value = new Vector3(0f, bounds.center.y, 0f);
        bodyData.FindProperty("envelopeExtents").vector3Value = new Vector3(radius + .05f, bounds.extents.y + .10f, radius + .05f);
        bodyData.ApplyModifiedPropertiesWithoutUndo();
        Record(body);
        var capsule = root.GetComponent<CapsuleCollider>();
        capsule.center = bounds.center;
        float visualScale = visual.localScale.x;
        capsule.radius = .5f * visualScale;
        capsule.height = Mathf.Max(visualScale, bounds.size.y + .16f * visualScale);
        Record(capsule);
        var agent = root.GetComponent<NavMeshAgent>();
        agent.radius = radius + .05f; // Native agent-to-agent avoidance reflects the visible wingspan.
        Record(agent);
        Debug.Log($"CLEARANCE {root.name}: visualY={visual.localPosition.y:F3} center={bounds.center} size={bounds.size} radius={agent.radius:F3}");
    }
}
