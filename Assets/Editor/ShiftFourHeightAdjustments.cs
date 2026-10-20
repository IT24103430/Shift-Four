using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Applies only the named height properties. Never invokes an installer or NavMesh bake.
public sealed class ShiftFourHeightAdjustments : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/MainLab.unity";
    private const string WallRoot = "Fixed walls — main scene owner only";
    private const string InfillRoot = "Height adjustment gate infill";
    private const string BackupRoot = "Library/HeightAdjustments";
    private static readonly string[] Types = { "Scout", "Guard", "Interceptor", "Flanker" };
    private static readonly string[] WallNames = { "West", "East upper", "East lower", "North", "South",
        "Vertical south", "Vertical middle", "Vertical north", "Horizontal west", "Horizontal middle",
        "Horizontal east", "Sealed checkpoint-storage wall" };
    private static readonly HashSet<string> OverheadNames = new HashSet<string> {
        "Ceiling cassette", "Ceiling service beam", "Cable tray", "Upper wall fascia",
        "Practical housing", "Practical diffuser", "Neutral practical fill" };
    [SerializeField] private float visualHeight = 1.6f;
    [SerializeField] private float wallHeight = 4f;

    [Serializable] private sealed class Entry { public string path, beforeHash, afterHash; }
    [Serializable] private sealed class Backup
    {
        public string directory;
        public List<Entry> files = new List<Entry>();
    }

    [MenuItem("Tools/Shift Four/Height Adjustments")]
    public static void Open() => GetWindow<ShiftFourHeightAdjustments>("Height Adjustments");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Edit Mode only. Visual rest Y is stored on each visual Transform. " +
            "The motor keeps its existing bobbing and turning. Apply updates four drone prefabs and MainLab; " +
            "backups are stored under Library/HeightAdjustments.", MessageType.Info);
        visualHeight = EditorGUILayout.FloatField("Visual rest height", visualHeight);
        wallHeight = EditorGUILayout.FloatField("Fixed wall height", wallHeight);
        EditorGUILayout.LabelField("Capsule center / combat origin", (visualHeight - .05f).ToString("F2") +
            " m / " + (visualHeight + .2f).ToString("F2") + " m");
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Preview in Console")) Run(() => Preview(visualHeight, wallHeight));
            if (GUILayout.Button("Apply and Save with Backup")) Run(() => Apply(visualHeight, wallHeight));
            if (GUILayout.Button("Restore Last Backup")) Run(RestoreLastBackup);
        }
    }

    private static void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    // Explicit entry point for the approved defaults, also usable in an isolated batch project.
    public static void ApplyApprovedDefaults() => Apply(1.6f, 4f);

    public static void Preview(float restHeight, float targetWallHeight)
    {
        ValidateNumbers(restHeight, targetWallHeight);
        Scene scene = GetScene();
        Transform walls = ValidateScene(scene);
        Debug.Log($"Height preview: four visuals -> {restHeight:F2} m; capsules -> {restHeight - .05f:F2} m; " +
            $"combat origins -> {restHeight + .2f:F2} m; 12 walls -> {targetWallHeight:F2} m; " +
            $"overhead displacement -> {targetWallHeight - walls.GetChild(0).localScale.y:F2} m. " +
            "Four stationary gate infills will meet the existing headers. No values saved.");
    }

    public static void Apply(float restHeight, float targetWallHeight)
    {
        ValidateNumbers(restHeight, targetWallHeight);
        Scene scene = GetScene();
        RequireCleanScenes();
        Transform walls = ValidateScene(scene);
        foreach (string type in Types)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(type));
            if (asset == null) throw new InvalidOperationException("Missing drone prefab: " + type);
            ValidateDrone(asset);
        }
        Backup backup = CreateBackup();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Shift Four height adjustments");
        try
        {
            foreach (string type in Types)
            {
                string originalData = File.ReadAllText(PrefabPath(type));
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath(type));
                try
                {
                    ApplyDrone(root, restHeight);
                    if (!PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(type)))
                        throw new IOException("Could not save " + type + " prefab.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // This branch cannot deserialize some teammate/forward-compatible policy
                // fields. Preserve their existing prefab bytes, allowing only our new origin.
                ShiftFourVisualUpgrade.PreserveGameplayYaml(originalData, PrefabPath(type), true);
            }
            foreach (DroneMotor motor in Components<DroneMotor>(scene)) ApplyDrone(motor.gameObject, restHeight);
            float difference = targetWallHeight - walls.GetChild(0).localScale.y;
            foreach (string name in WallNames) SetWallHeight(walls.Find(name), targetWallHeight);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!new[] { "Checkpoint", "Server", "Control", "Storage" }.Contains(root.name)) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (IsOverhead(t))
                    {
                        Vector3 p = t.localPosition;
                        p.y += difference;
                        SetPosition(t, p);
                    }
            }
            ApplyInfills(scene, walls, targetWallHeight);
            Physics.SyncTransforms();
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MainLab.");
            FinishBackup(backup);
            // Interactive prefab/Undo callbacks can dirty the scene later in this Editor
            // update. Finish the explicitly requested save once those callbacks settle.
            if (!Application.isBatchMode) EditorApplication.delayCall += () =>
            {
                if (!scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Undo.FlushUndoRecordObjects();
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                { Debug.LogError("Could not finish saving MainLab height adjustments."); return; }
                FinishBackup(backup);
            };
            Debug.Log("HEIGHT_ADJUSTMENTS_APPLIED: visuals=" + restHeight + "; walls=" + targetWallHeight +
                "; backup=" + backup.directory + ". MainLab and four drone prefabs saved.");
            SceneView.RepaintAll();
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            RestoreFiles(backup);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            throw;
        }
    }

    // Public so an isolated fixture can validate height, binding and collision alignment together.
    public static void ApplyDrone(GameObject root, float restHeight)
    {
        if (!Finite(restHeight) || restHeight < .95f) throw new ArgumentOutOfRangeException(nameof(restHeight));
        Transform visual = ValidateDrone(root);
        Vector3 p = visual.localPosition;
        p.y = restHeight;
        SetPosition(visual, p);
        var motor = new SerializedObject(root.GetComponent<DroneMotor>());
        motor.FindProperty("visual").objectReferenceValue = visual;
        motor.ApplyModifiedProperties();
        Record(root.GetComponent<DroneMotor>());
        var collider = root.GetComponent<CapsuleCollider>();
        Undo.RecordObject(collider, "Align drone capsule");
        Vector3 center = collider.center;
        center.y = restHeight - .05f;
        collider.center = center;
        Record(collider);
        var brain = new SerializedObject(root.GetComponent<DroneBrain>());
        brain.FindProperty("combatOriginHeight").floatValue = restHeight + .2f;
        brain.ApplyModifiedProperties();
        Record(root.GetComponent<DroneBrain>());
    }

    public static void SetWallHeight(Transform wall, float height)
    {
        if (wall == null || !Finite(height) || height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));
        float bottom = wall.localPosition.y - wall.localScale.y * .5f;
        Undo.RecordObject(wall, "Raise fixed wall");
        Vector3 scale = wall.localScale;
        scale.y = height;
        wall.localScale = scale;
        Vector3 p = wall.localPosition;
        p.y = bottom + height * .5f;
        wall.localPosition = p;
        Record(wall);
    }

    private static Transform ValidateDrone(GameObject root)
    {
        var visuals = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("Drone visual - ")).ToArray();
        var capsule = root.GetComponent<CapsuleCollider>();
        var agent = root.GetComponent<NavMeshAgent>();
        if (visuals.Length != 1 || root.GetComponent<DroneMotor>() == null || root.GetComponent<DroneBrain>() == null ||
            capsule == null || agent == null || !Mathf.Approximately(agent.baseOffset, 0f) ||
            !Mathf.Approximately(capsule.height, 1.8f) || !Mathf.Approximately(capsule.radius, .5f) ||
            root.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("Unexpected drone hierarchy or collision settings: " + root.name);
        return visuals[0];
    }

    private static Transform ValidateScene(Scene scene)
    {
        GameObject wallRoot = scene.GetRootGameObjects().SingleOrDefault(g => g.name == WallRoot);
        if (wallRoot == null || wallRoot.transform.childCount != WallNames.Length ||
            wallRoot.transform.position != Vector3.zero || wallRoot.transform.rotation != Quaternion.identity ||
            wallRoot.transform.lossyScale != Vector3.one)
            throw new InvalidOperationException("Expected the 12 fixed-wall segments in MainLab.");
        float height = wallRoot.transform.GetChild(0).localScale.y;
        foreach (string name in WallNames)
        {
            Transform wall = wallRoot.transform.Find(name);
            if (wall == null || wall.GetComponent<BoxCollider>() == null || wall.localRotation != Quaternion.identity ||
                !Mathf.Approximately(wall.localScale.y, height) ||
                Mathf.Abs(wall.localPosition.y - height * .5f) > .001f)
                throw new InvalidOperationException("Wall needs manual review: " + name);
        }
        var drones = Components<DroneMotor>(scene).ToArray();
        if (drones.Length != 4) throw new InvalidOperationException("Expected four scene drones.");
        foreach (DroneMotor drone in drones) ValidateDrone(drone.gameObject);
        var gates = Components<LabDoor>(scene).ToArray();
        if (gates.Length != 4 || !gates.Select(g => g.GateId).OrderBy(i => i).SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidOperationException("Expected four independent security gates.");
        foreach (LabDoor gate in gates)
            if (gate.transform.Find("Door assembly/Header") == null)
                throw new InvalidOperationException("Gate header needs manual review: " + gate.name);
        return wallRoot.transform;
    }

    private static bool IsOverhead(Transform t)
    {
        if (t.parent != null && t.parent.name == "Visual polish") return OverheadNames.Contains(t.name);
        if (t.parent == null || t.parent.name != "Server finish") return false;
        return t.name == "Ceiling" || t.name.StartsWith("Ceiling beam ") ||
            t.name.StartsWith("Light strip ") || t.name.StartsWith("Server fill ");
    }

    private static void ApplyInfills(Scene scene, Transform walls, float height)
    {
        GameObject group = scene.GetRootGameObjects().SingleOrDefault(g => g.name == InfillRoot);
        if (group == null)
        {
            group = new GameObject(InfillRoot);
            SceneManager.MoveGameObjectToScene(group, scene);
            Undo.RegisterCreatedObjectUndo(group, "Create stationary gate infill");
        }
        foreach (LabDoor gate in Components<LabDoor>(scene))
        {
            Transform header = gate.transform.Find("Door assembly/Header");
            Renderer[] renderers = header.GetComponentsInChildren<Renderer>(true);
            float top = renderers.Max(r => r.bounds.max.y);
            if (height <= top) throw new InvalidOperationException("Wall top must clear the existing gate header.");
            string name = "Above " + gate.name;
            Transform existing = group.transform.Find(name);
            GameObject infill = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(infill, "Create stationary gate infill");
                infill.name = name;
                infill.transform.SetParent(group.transform, false);
            }
            Undo.RecordObject(infill.transform, "Align gate infill");
            Vector3 p = gate.transform.position;
            p.y = (height + top) * .5f;
            infill.transform.position = p;
            infill.transform.rotation = gate.transform.rotation;
            infill.transform.localScale = new Vector3(4.45f, height - top, .6f);
            infill.GetComponent<Renderer>().sharedMaterial = walls.GetChild(0).GetComponent<Renderer>().sharedMaterial;
        }
    }

    private static Scene GetScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            RequireCleanScenes();
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }
        return scene;
    }
    private static void RequireCleanScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene edits before applying or restoring heights.");
    }
    private static IEnumerable<T> Components<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    private static void ValidateNumbers(float restHeight, float height)
    {
        if (!Finite(restHeight) || restHeight < .95f || !Finite(height) || height <= 3.3f)
            throw new ArgumentOutOfRangeException("Heights must be finite; visual >= 0.95 m and wall > 3.3 m.");
    }
    private static void SetPosition(Transform t, Vector3 p)
    {
        Undo.RecordObject(t, "Adjust authored height");
        t.localPosition = p;
        Record(t);
    }
    private static void Record(Object obj)
    {
        EditorUtility.SetDirty(obj);
        if (PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
    }
    private static string PrefabPath(string type) => "Assets/Prefabs/Agents/" + type + ".prefab";
    private static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    private static Backup CreateBackup()
    {
        var backup = new Backup { directory = BackupRoot + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") };
        foreach (string path in Types.Select(PrefabPath).Prepend(ScenePath))
        {
            string dest = backup.directory + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(path, dest);
            backup.files.Add(new Entry { path = path, beforeHash = Hash(path) });
        }
        File.WriteAllText(backup.directory + "/manifest.json", JsonUtility.ToJson(backup, true));
        return backup;
    }
    private static void FinishBackup(Backup backup)
    {
        foreach (Entry file in backup.files) file.afterHash = Hash(file.path);
        string json = JsonUtility.ToJson(backup, true);
        File.WriteAllText(backup.directory + "/manifest.json", json);
        File.WriteAllText(BackupRoot + "/last.json", json);
    }
    public static void RestoreLastBackup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        RequireCleanScenes();
        var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupRoot + "/last.json"));
        foreach (Entry file in backup.files)
            if (Hash(file.path) != file.afterHash) throw new InvalidOperationException("Later edits detected; restore manually: " + file.path);
        RestoreFiles(backup);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("HEIGHT_ADJUSTMENTS_RESTORED: " + backup.directory);
    }
    private static void RestoreFiles(Backup backup)
    {
        foreach (Entry file in backup.files) File.Copy(backup.directory + "/" + file.path, file.path, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
