using System;
using ShiftFour;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Run ONCE after Unity imports the project. It creates editable prefabs and a scene,
// rather than hiding the level inside runtime generation code.
public static class BaselineSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainLab.unity";
    private static Material wall, floor, metal, screen, exit, scout, flanker, guard, interceptor;

    [MenuItem("Tools/Shift Four/Generate Greybox Once")]
    public static void Generate()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorUtility.DisplayDialog("Shift Four", "The scene already exists. Generation stopped to protect team edits.", "OK");
            return;
        }
        Ensure("Assets/Scenes");
        Ensure("Assets/Prefabs");
        Ensure("Assets/Prefabs/Sections");
        Ensure("Assets/Prefabs/Agents");
        Ensure("Assets/Materials");
        Materials();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(0.48f, 0.53f, 0.60f);
        RenderSettings.skybox = null;
        GameObject sunlight = new GameObject("Soft lab light");
        Light light = sunlight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        sunlight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // Prefabs are separate files: each student edits their section without touching this scene.
        Section("Checkpoint", new Vector3(-10f, 0f, -10f), floor, new[] {
            new Vector3(-4f, 0f, -1f), new Vector3(3f, 0f, 2f) });
        Section("Storage", new Vector3(10f, 0f, -10f), floor, new[] {
            new Vector3(-3f, 0f, -3f), new Vector3(0f, 0f, 2f), new Vector3(3f, 0f, -1f) });
        Section("Server", new Vector3(-10f, 0f, 10f), floor, new[] {
            new Vector3(-2f, 0f, 1f), new Vector3(2f, 0f, -2f) });
        Section("Control", new Vector3(10f, 0f, 10f), floor, new[] {
            new Vector3(-3f, 0f, 2f), new Vector3(3f, 0f, -2f) });
        BuildWalls();

        GameObject graphObject = new GameObject("Waypoint graph — A* across four sections");
        WaypointGraph graph = graphObject.AddComponent<WaypointGraph>();
        Transform[] nodes = new Transform[5];
        Vector3[] centers = {
            new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f),
            new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f), new Vector3(22f, 0f, -15f)
        };
        string[] labels = { "Checkpoint", "Storage", "Server", "Control", "Exit" };
        for (int i = 0; i < nodes.Length; i++)
        {
            GameObject node = new GameObject("Waypoint " + labels[i]);
            node.transform.SetParent(graphObject.transform);
            node.transform.position = centers[i];
            nodes[i] = node.transform;
        }
        graph.Configure(nodes, new[] {
            new GraphEdge(0, 2, SecurityGate.Checkpoint), new GraphEdge(2, 3, SecurityGate.Server),
            new GraphEdge(3, 1, SecurityGate.Control), new GraphEdge(1, 4, SecurityGate.Storage)
        });

        GameObject navRoot = new GameObject("NavMesh surface — baked greybox");
        NavMeshSurface surface = navRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        // The NavMesh asset manager needs a saved scene path for persistent data.
        EditorSceneManager.SaveScene(scene, ScenePath);
        surface.BuildNavMesh();
        if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
        {
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Scenes/MainLab_NavMesh.asset");
            EditorUtility.SetDirty(surface);
        }

        new GameObject("Game session").AddComponent<GameSession>();
        BuildExit();
        BuildPlayer();
        BuildDrone<ScoutBrain>("Scout", centers[0] + new Vector3(2f, 0f, 0f), centers[0], scout);
        BuildDrone<FlankerBrain>("Flanker", centers[1] + new Vector3(0f, 0f, 3f), centers[1], flanker);
        GameObject serverGuard = BuildDrone<GuardBrain>("Guard", centers[2] + new Vector3(3f, 0f, 3f), centers[2], guard);
        serverGuard.GetComponent<GuardBrain>().SetCover(
            centers[2] + new Vector3(-4f, 0f, 2f), centers[2] + new Vector3(4f, 0f, 2f));
        BuildDrone<InterceptorBrain>("Interceptor", centers[3] + new Vector3(0f, 0f, -4f), centers[3], interceptor);

        EditorSceneManager.SaveScene(scene, ScenePath);
        ShiftFourModelInstaller.UpdateSceneLayout(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Shift Four", "Greybox generated. Press Play. Keep every generated .meta file in Git.", "OK");
    }

    private static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    private static void Materials()
    {
        wall = Material("Wall", new Color(0.26f, 0.31f, 0.36f));
        floor = Material("Floor", new Color(0.38f, 0.44f, 0.47f));
        metal = Material("Cover", new Color(0.19f, 0.26f, 0.32f));
        screen = Material("Console offline", new Color(0.9f, 0.52f, 0.18f));
        exit = Material("Exit", new Color(0.23f, 0.85f, 0.56f));
        scout = Material("Scout blue", new Color(0.24f, 0.64f, 0.95f));
        flanker = Material("Flanker orange", new Color(0.95f, 0.51f, 0.23f));
        guard = Material("Guard violet", new Color(0.67f, 0.46f, 0.91f));
        interceptor = Material("Interceptor red", new Color(0.95f, 0.34f, 0.36f));
    }

    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material result = new Material(shader) { color = color, name = name };
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    private static GameObject Cube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        if (parent != null) cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static void Section(string name, Vector3 center, Material tile, Vector3[] cover)
    {
        GameObject root = new GameObject(name + " section");
        Cube("Floor", root.transform, new Vector3(0f, -0.16f, 0f),
            new Vector3(19.8f, 0.32f, 19.8f), tile);
        for (int i = 0; i < cover.Length; i++)
            Cube("Fixed cover " + (i + 1), root.transform,
                cover[i] + new Vector3(0f, 1.1f, 0f),
                name == "Checkpoint" ? new Vector3(2.4f, 2.2f, 1.2f) : new Vector3(2f, 2.2f, 1.5f), metal);
        GameObject console = Cube(name + " console", root.transform,
            new Vector3(6.5f, 0.7f, 6.4f), new Vector3(1.2f, 1.4f, 0.55f), metal);
        GameObject display = Cube("Indicator", console.transform,
            new Vector3(0f, 0.25f, -0.52f), new Vector3(0.65f, 0.38f, 0.13f), screen);
        UnityEngine.Object.DestroyImmediate(display.GetComponent<Collider>());
        console.AddComponent<ConsoleSwitch>().Configure(name, display.GetComponent<Renderer>());
        string path = "Assets/Prefabs/Sections/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = center;
    }

    private static void BuildWalls()
    {
        GameObject root = new GameObject("Fixed walls — main scene owner only");
        Cube("West", root.transform, new Vector3(-20f, 1.55f, 0f), new Vector3(0.5f, 3.1f, 40.5f), wall);
        Cube("East", root.transform, new Vector3(20f, 1.55f, 0f), new Vector3(0.5f, 3.1f, 40.5f), wall);
        Cube("North", root.transform, new Vector3(0f, 1.55f, 20f), new Vector3(40.5f, 3.1f, 0.5f), wall);
        Cube("South", root.transform, new Vector3(0f, 1.55f, -20f), new Vector3(40.5f, 3.1f, 0.5f), wall);
        Cube("Vertical south", root.transform, new Vector3(0f, 1.55f, -16f), new Vector3(0.45f, 3.1f, 8f), wall);
        Cube("Vertical middle", root.transform, new Vector3(0f, 1.55f, 0f), new Vector3(0.45f, 3.1f, 16f), wall);
        Cube("Vertical north", root.transform, new Vector3(0f, 1.55f, 16f), new Vector3(0.45f, 3.1f, 8f), wall);
        Cube("Horizontal west", root.transform, new Vector3(-16f, 1.55f, 0f), new Vector3(8f, 3.1f, 0.45f), wall);
        Cube("Horizontal middle", root.transform, new Vector3(0f, 1.55f, 0f), new Vector3(16f, 3.1f, 0.45f), wall);
        Cube("Horizontal east", root.transform, new Vector3(16f, 1.55f, 0f), new Vector3(8f, 3.1f, 0.45f), wall);
    }

    private static void BuildDoor()
    {
        GameObject root = new GameObject("Sliding shortcut door");
        root.transform.position = new Vector3(0f, 0f, -10f);
        Cube("Door placeholder — replace with Blender model", root.transform,
            new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 2.4f, 3.6f), metal)
            .GetComponent<BoxCollider>().enabled = false;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.2f, 0f);
        collider.size = new Vector3(0.4f, 2.4f, 3.6f);
        root.AddComponent<Rigidbody>().isKinematic = true;
        NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = collider.center;
        obstacle.size = collider.size;
        obstacle.carving = true;
        root.AddComponent<LabDoor>();
    }

    private static void BuildExit()
    {
        GameObject root = new GameObject("Exit — needs all consoles and drones");
        root.transform.position = new Vector3(16f, 0f, 16f);
        Cube("Green exit marker", root.transform,
            new Vector3(0f, 0.025f, 0f), new Vector3(2.2f, 0.05f, 2.2f), exit)
            .GetComponent<BoxCollider>().enabled = false;
        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.9f, 0f);
        trigger.size = new Vector3(2.2f, 1.8f, 2.2f);
        root.AddComponent<ExitZone>();
    }

    private static void BuildPlayer()
    {
        GameObject root = new GameObject("Player");
        root.transform.position = new Vector3(-15f, 0f, -15f);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        root.AddComponent<Health>().Configure(100f, true);
        GameObject cameraObject = new GameObject("First person camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        root.AddComponent<PlayerController>().Configure(camera);
    }

    private static GameObject BuildDrone<T>(string name, Vector3 position, Vector3 home, Material color)
        where T : DroneBrain
    {
        GameObject root = new GameObject(name + " drone");
        CapsuleCollider body = root.AddComponent<CapsuleCollider>();
        body.center = new Vector3(0f, 1f, 0f);
        body.height = 1.8f;
        body.radius = 0.5f;
        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.46f;
        agent.height = 1.8f;
        agent.speed = 3.5f;
        agent.stoppingDistance = 1f;
        GameObject visual = Cube("Drone placeholder — replace with Blender model", root.transform,
            new Vector3(0f, 1.05f, 0f), new Vector3(1f, 0.45f, 0.85f), color);
        UnityEngine.Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
        root.AddComponent<Health>().Configure(75f, false);
        root.AddComponent<DroneMotor>().Configure(visual.transform);
        root.AddComponent<T>();
        string path = "Assets/Prefabs/Agents/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        instance.GetComponent<DroneBrain>().Configure(name, home);
        return instance;
    }
}
