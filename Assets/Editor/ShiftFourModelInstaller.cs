using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Migrates the known baseline once; preserves existing gate visuals and graph edits on rerun.
public static class ShiftFourModelInstaller
{
    private const string DroneFolder = "Assets/Models/";
    private const string DoorPath = "Assets/Models/SF_SlidingLabDoor.obj";
    private const string ScenePath = "Assets/Scenes/MainLab.unity";

    public static GameObject AddDroneVisual(Transform root, string type)
        => AddDroneVisual(root, type, root.Find("Drone visual - " + type) != null
            ? root.Find("Drone visual - " + type).localPosition.y : 1.05f);

    public static GameObject AddDroneVisual(Transform root, string type, float restHeight)
    {
        if (float.IsNaN(restHeight) || float.IsInfinity(restHeight) || restHeight < 0f)
            throw new System.ArgumentOutOfRangeException(nameof(restHeight));
        string path = DroneFolder + "SF_" + type + "Drone.obj";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DroneFolder + "SF_" + type + "Drone_GV.fbx") != null)
            path = DroneFolder + "SF_" + type + "Drone_GV.fbx";
        GameObject result = ImportVisual(path, root, "Drone visual - " + type);
        if (result == null) return null;
        result.transform.localPosition = new Vector3(0f, restHeight, 0f);
        result.transform.localRotation = Quaternion.identity;
        result.transform.localScale = Vector3.one;
        if (result.GetComponent<DroneRotorVisuals>() == null)
            result.AddComponent<DroneRotorVisuals>();
        return result;
    }

    public static GameObject AddDoorVisual(Transform root)
    {
        Transform existing = root.Find("Door assembly");
        if (existing != null) return existing.gameObject;
        RemoveLegacyDoorVisuals(root);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(DroneFolder + "SF_HeavySecurityDoor_GV.fbx");
        if (model != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform assembly = System.Array.Find(instance.GetComponentsInChildren<Transform>(), t => t.name == "Door assembly" || t.name == "Door_assembly");
            if (assembly == null && instance.transform.Find("LeftPanel") != null) assembly = instance.transform;
            if (assembly != null)
            {
                // FBX may place centimetre scale and axis conversion on its root. Preserve
                // evaluated geometry while providing metre-scale, local-X animation pivots.
                GameObject normalized = new GameObject("Door assembly");
                var parts = new System.Collections.Generic.List<Transform>();
                foreach (Transform part in assembly) parts.Add(part);
                foreach (Transform part in parts)
                {
                    GameObject pivot = new GameObject(part.name);
                    pivot.transform.SetParent(normalized.transform, false);
                    pivot.transform.position = part.position;
                    part.name += " geometry";
                    part.SetParent(pivot.transform, true);
                }
                Object.DestroyImmediate(instance);
                normalized.transform.SetParent(root, false);
                return normalized;
            }
            Object.DestroyImmediate(instance);
            throw new System.InvalidOperationException("GV door export is missing Door assembly");
        }
        return BuildHeavySecurityDoorVisual(root);
    }

    private static GameObject ImportVisual(string assetPath, Transform parent, string name)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null)
        {
            Debug.LogError("Missing model: " + assetPath);
            return null;
        }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider, true);
        return instance;
    }

    [MenuItem("Tools/Shift Four/Install Drone + Door Models")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (System.Array.Exists(new[] { "Scout", "Flanker", "Guard", "Interceptor" },
                type => AssetDatabase.LoadAssetAtPath<GameObject>(DroneFolder + "SF_" + type + "Drone.obj") == null))
        {
            EditorUtility.DisplayDialog("Missing models", "Reimport Assets/Models and retry.", "OK");
            return;
        }
        string[] types = { "Scout", "Flanker", "Guard", "Interceptor" };
        foreach (string type in types)
        {
            string path = "Assets/Prefabs/Agents/" + type + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (ReplaceDrone(contents, type)) PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            UpdateSceneLayout(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!Application.isBatchMode) EditorUtility.DisplayDialog("Shift Four", "Four room objective gates installed. Re-bake the NavMesh, save MainLab, and press Play.", "OK");
    }

    [MenuItem("Tools/Shift Four/Apply Nimthara GV Art to MainLab")]
    public static void ApplyGVArt()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DroneFolder + "SF_GuardDrone_GV.fbx") == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(DroneFolder + "SF_HeavySecurityDoor_GV.fbx") == null)
            throw new System.InvalidOperationException("Generate/import both Nimthara GV FBX files first.");
        string path = "Assets/Prefabs/Agents/Guard.prefab";
        GameObject prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform old = prefab.transform.Find("Drone visual - Guard");
            float restHeight = old != null ? old.localPosition.y : 1.05f;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            ReplaceDrone(prefab, "Guard", restHeight);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (LabDoor door in root.GetComponentsInChildren<LabDoor>(true))
            {
                RemoveLegacyDoorVisuals(door.transform);
                AddDoorVisual(door.transform);
            }
            if (root.GetComponent<GuardBrain>() != null)
            {
                Transform old = root.transform.Find("Drone visual - Guard");
                float restHeight = old != null ? old.localPosition.y : 1.05f;
                if (old != null) Object.DestroyImmediate(old.gameObject);
                ReplaceDrone(root, "Guard", restHeight);
            }
        }
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
    }

    private static bool ReplaceDrone(GameObject root, string type, float restHeight = 1.05f)
    {
        if (root.transform.Find("Drone visual - " + type) != null) return false;
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = root.transform.GetChild(i);
            if (child.name.Contains("placeholder") || child.name.StartsWith("Drone visual -"))
                Object.DestroyImmediate(child.gameObject);
        }
        GameObject visual = AddDroneVisual(root.transform, type, restHeight);
        if (visual != null)
        {
            DroneMotor motor = root.GetComponent<DroneMotor>();
            if (motor != null)
            {
                motor.Configure(visual.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(motor))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            }
        }
        return visual != null;
    }

    public static void UpdateSceneLayout(Scene scene)
    {
        GameObject graphRoot = FindRoot(scene, "Waypoint graph — A* across four sections");
        WaypointGraph graph = graphRoot != null ? graphRoot.GetComponent<WaypointGraph>() : null;
        if (graph != null && graph.ContractVersion == 0)
        {
            // Refuse an unknown legacy layout instead of destroying custom node/edge data.
            string[] labels = { "Checkpoint", "Storage", "Server", "Control" };
            Transform[] nodes = new Transform[5];
            for (int i = 0; i < 4; i++)
            {
                nodes[i] = graphRoot.transform.Find("Waypoint " + labels[i]);
                if (nodes[i] == null) throw new System.InvalidOperationException("Unknown graph layout; coordinate migration with Pamudi.");
            }
            if (!graph.IsKnownLegacyLayout) throw new System.InvalidOperationException("Custom graph detected; migrate edges explicitly before installing.");
            GameObject exitNode = new GameObject("Waypoint Exit");
            exitNode.transform.SetParent(graphRoot.transform, false);
            exitNode.transform.position = new Vector3(22f, 0f, -15f);
            nodes[4] = exitNode.transform;
            graph.Configure(nodes, new[] { new GraphEdge(0, 2, SecurityGate.Checkpoint),
                new GraphEdge(2, 3, SecurityGate.Server), new GraphEdge(3, 1, SecurityGate.Control),
                new GraphEdge(1, 4, SecurityGate.Storage) });
        }

        foreach (GameObject section in scene.GetRootGameObjects())
        {
            int id = MissionProgress.RoomId(section.name);
            if (id < 0 || section.transform.Find("Mission room zone") != null) continue;
            GameObject zone = new GameObject("Mission room zone");
            zone.transform.SetParent(section.transform, false);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 1.5f, 0f);
            trigger.size = new Vector3(19.5f, 3f, 19.5f);
            trigger.isTrigger = true;
            zone.AddComponent<RoomZone>().Configure((SecurityGate)id);
        }

        GameObject walls = FindRoot(scene, "Fixed walls — main scene owner only");
        EnsureCheckpointStorageWall(walls);
        if (walls != null)
        {
            Transform originalEast = walls.transform.Find("East");
            if (originalEast != null)
            {
                originalEast.name = "East upper";
                originalEast.localPosition = new Vector3(20f, 1.55f, 3.5f);
                originalEast.localScale = new Vector3(.5f, 3.1f, 33f);
            }
            if (walls.transform.Find("East lower") == null)
                CreateWall("East lower", walls.transform, new Vector3(20f, 1.55f, -18.5f), new Vector3(.5f, 3.1f, 3f));
        }
        if (FindRoot(scene, "Exit landing") == null)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Exit landing";
            platform.transform.position = new Vector3(21.65f, -.16f, -15f);
            platform.transform.localScale = new Vector3(4f, .32f, 4f);
            ApplySharedMaterial(platform.GetComponent<Renderer>(), new Color(.38f, .44f, .47f), "Floor");
        }

        GameObject old = FindRoot(scene, "Sliding shortcut door") ?? FindRoot(scene, "North security bulkhead door");
        if (old != null) Object.DestroyImmediate(old);
        EnsureGate(scene, "Checkpoint to Server gate", new Vector3(-10f, 0f, 0f), 0f, 0, "Checkpoint", "Scout");
        EnsureGate(scene, "Server to Control gate", new Vector3(0f, 0f, 10f), 90f, 1, "Server", "Guard");
        EnsureGate(scene, "Control to Storage gate", new Vector3(10f, 0f, 0f), 0f, 2, "Control", "Interceptor");
        EnsureGate(scene, "Storage exit gate", new Vector3(20f, 0f, -15f), 90f, 3, "Storage", "Flanker");

        GameObject exit = FindRoot(scene, "Exit — needs all consoles and drones");
        if (exit != null) exit.transform.position = new Vector3(22f, 0f, -15f);
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name.EndsWith(" drone") && root.GetComponent<DroneMotor>() != null)
                ReplaceDrone(root, root.name.Split(' ')[0]);
        Debug.Log("Shift Four: four sequential objective gates installed. Re-bake the NavMesh and save MainLab.");
    }

    private static void EnsureGate(Scene scene, string name, Vector3 position, float yaw, int id, string room, string drone)
    {
        GameObject root = FindRoot(scene, name);
        if (root == null)
        {
            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        if (root.GetComponent<LabDoor>() != null && root.GetComponent<LabDoor>().GateId == id) return;
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = Vector3.one;
        AddDoorVisual(root.transform);
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null) collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.35f, 0f);
        collider.size = new Vector3(4.2f, 3.1f, .6f);
        collider.enabled = true;
        collider.isTrigger = false;
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null) body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = collider.center;
        obstacle.size = collider.size;
        obstacle.carving = true;
        LabDoor controller = root.GetComponent<LabDoor>();
        if (controller == null) controller = root.AddComponent<LabDoor>();
        controller.Configure(id, room, drone);
    }

    private static GameObject CreateWall(string name, Transform parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = size;
        ApplySharedMaterial(wall.GetComponent<Renderer>(), new Color(.26f, .31f, .36f), "Wall");
        return wall;
    }

    private static void EnsureCheckpointStorageWall(GameObject wallsRoot)
    {
        if (wallsRoot == null || wallsRoot.transform.Find("Sealed checkpoint-storage wall") != null) return;
        CreateWall("Sealed checkpoint-storage wall", wallsRoot.transform,
            new Vector3(0f, 1.55f, -10f), new Vector3(.45f, 3.1f, 4.2f));
    }

    private static void RemoveLegacyDoorVisuals(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.name.StartsWith("Door visual -") || child.name == "Door assembly")
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static GameObject BuildHeavySecurityDoorVisual(Transform root)
    {
        GameObject assembly = new GameObject("Door assembly");
        assembly.transform.SetParent(root, false);
        assembly.transform.localPosition = Vector3.zero;

        Material metal = GetOrCreateMaterial("HeavyDoor_Graphite", new Color(0.17f, 0.20f, 0.24f));
        Material trim = GetOrCreateMaterial("HeavyDoor_Trim", new Color(0.30f, 0.34f, 0.39f));
        Material warning = GetOrCreateMaterial("HeavyDoor_Warning", new Color(0.95f, 0.55f, 0.17f));
        Material lightRed = GetOrCreateMaterial("HeavyDoor_StatusRed", new Color(0.97f, 0.26f, 0.24f));
        Material lightAmber = GetOrCreateMaterial("HeavyDoor_StatusAmber", new Color(1f, 0.72f, 0.18f));

        CreateCube("Header", assembly.transform, new Vector3(0f, 3.15f, 0f), new Vector3(4.45f, 0.4f, 0.55f), trim);
        CreateCube("Threshold", assembly.transform, new Vector3(0f, 0.08f, 0f), new Vector3(3.55f, 0.16f, 0.55f), trim);
        CreateCube("LeftFrame", assembly.transform, new Vector3(-2.1f, 1.5f, 0f), new Vector3(0.25f, 2.7f, 0.55f), trim);
        CreateCube("RightFrame", assembly.transform, new Vector3(2.1f, 1.5f, 0f), new Vector3(0.25f, 2.7f, 0.55f), trim);
        CreateCube("LintelLightBar", assembly.transform, new Vector3(0f, 2.55f, -0.25f), new Vector3(2.2f, 0.08f, 0.08f), warning);

        CreatePanel(assembly.transform, "LeftPanel", new Vector3(-1f, 1.5f, 0f), metal, warning, true);
        CreatePanel(assembly.transform, "RightPanel", new Vector3(1f, 1.5f, 0f), metal, warning, false);
        CreateCube("LeftBolt", assembly.transform, new Vector3(-0.25f, 1.4f, -0.16f), new Vector3(0.22f, 0.18f, 0.14f), trim);
        CreateCube("RightBolt", assembly.transform, new Vector3(0.25f, 1.4f, -0.16f), new Vector3(0.22f, 0.18f, 0.14f), trim);
        CreateCube("StatusLightLeft", assembly.transform, new Vector3(-1.15f, 2.4f, -0.2f), new Vector3(0.22f, 0.16f, 0.05f), lightRed);
        CreateCube("StatusLightRight", assembly.transform, new Vector3(1.15f, 2.4f, -0.2f), new Vector3(0.22f, 0.16f, 0.05f), lightRed);

        GameObject terminal = new GameObject("AccessTerminal");
        terminal.transform.SetParent(assembly.transform, false);
        terminal.transform.localPosition = new Vector3(2.15f, 1.1f, 0f);
        CreateCube("Body", terminal.transform, Vector3.zero, new Vector3(0.45f, 1.45f, 0.35f), trim);
        CreateCube("Screen", terminal.transform, new Vector3(0f, 0.18f, -0.18f), new Vector3(0.25f, 0.34f, 0.04f), lightAmber);
        CreateCube("Beacon", terminal.transform, new Vector3(0f, 0.62f, -0.18f), new Vector3(0.16f, 0.10f, 0.04f), lightRed);
        CreateCube("Reader", terminal.transform, new Vector3(0f, -0.22f, -0.18f), new Vector3(0.18f, 0.12f, 0.04f), warning);

        return assembly;
    }

    private static void CreatePanel(Transform parent, string name, Vector3 localPosition, Material metal, Material warning, bool left)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        CreateCube("Core", root.transform, Vector3.zero, new Vector3(2f, 3f, 0.22f), metal);
        CreateCube("ArmourUpper", root.transform, new Vector3(0f, 0.72f, -0.02f), new Vector3(0.95f, 0.5f, 0.08f), metal);
        CreateCube("ArmourLower", root.transform, new Vector3(0f, -0.72f, -0.02f), new Vector3(0.95f, 0.5f, 0.08f), metal);
        CreateCube("RibA", root.transform, new Vector3(0f, 0.0f, -0.08f), new Vector3(0.90f, 0.14f, 0.06f), warning);
        CreateCube("RibB", root.transform, new Vector3(0f, 0.45f, -0.08f), new Vector3(0.75f, 0.12f, 0.06f), warning);
        CreateCube("RibC", root.transform, new Vector3(0f, -0.45f, -0.08f), new Vector3(0.75f, 0.12f, 0.06f), warning);
        CreateCube(left ? "InnerChamfer" : "OuterChamfer", root.transform, new Vector3(left ? 0.42f : -0.42f, 0f, -0.04f), new Vector3(0.14f, 2.0f, 0.08f), warning);
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = localScale;
        ApplySharedMaterial(cube.GetComponent<Renderer>(), material);
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube;
    }

    private static void ApplySharedMaterial(Renderer renderer, Material material)
    {
        if (renderer != null) renderer.sharedMaterial = material;
    }

    private static void ApplySharedMaterial(Renderer renderer, Color color, string baseName)
    {
        ApplySharedMaterial(renderer, GetOrCreateMaterial(baseName + "_Shared", color));
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        const string materialFolder = "Assets/Materials/";
        string path = materialFolder + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder(materialFolder.TrimEnd('/')))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}
