using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShiftFour;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit environment authoring only. Does not regenerate MainLab or change gameplay/navigation.
public static class WorldBuilderEnvironmentBuilder
{
    private const string ScenePath = "Assets/Scenes/MainLab.unity";
    private const string Folder = "Assets/Environment/WorldBuilder";
    private const string GroupName = "World Builder finish";
    private static readonly string[] Rooms = { "Checkpoint", "Server", "Control", "Storage" };
    private static readonly Vector3[] Centres = {
        new Vector3(-10, 0, -10), new Vector3(-10, 0, 10),
        new Vector3(10, 0, 10), new Vector3(10, 0, -10) };
    private static readonly Color[] Accents = {
        new Color(.68f, .45f, .20f), new Color(.22f, .56f, .61f),
        new Color(.65f, .28f, .23f), new Color(.73f, .49f, .22f) };
    private static readonly string[] Subtitles = { "SECURITY INTAKE", "DATA SERVICES", "OPERATIONS", "CARGO / DISPATCH" };
    private static readonly string Output = "Logs/WorldBuilderGV";

    private sealed class Batch
    {
        public readonly List<CombineInstance> Pieces = new List<CombineInstance>();
        public readonly Material Material;
        public Batch(Material material) { Material = material; }
        public void Box(Vector3 position, Vector3 size, Quaternion rotation)
        {
            Pieces.Add(new CombineInstance { mesh = CubeMesh, transform = Matrix4x4.TRS(position, rotation, size) });
        }
    }
    private static Mesh cube;
    private static Font signageFont;
    private static Mesh CubeMesh
    {
        get
        {
            if (cube != null) return cube;
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            return cube;
        }
    }

    [MenuItem("Tools/Shift Four/Finish World Builder Environment")]
    public static void Apply()
    {
        RequireSavedEditor();
        string originalScene = File.ReadAllText(ScenePath);
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Physics.SyncTransforms();
        EnsureFolder(Folder);
        PrepareSignage();
        var walls = All<BoxCollider>(scene).Where(c => !c.isTrigger && c.enabled &&
            c.GetComponentInParent<LabDoor>() == null && c.bounds.size.y > 3.4f &&
            (c.bounds.size.x < .85f || c.bounds.size.z < .85f)).Select(c => c.bounds).ToArray();
        if (walls.Length < 8) throw new InvalidOperationException("Expected existing full-height lab walls; no changes applied.");
        string backup = "Library/WorldBuilderEnvironmentBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        foreach (var path in Rooms.Select(r => "Assets/Prefabs/Sections/" + r + ".prefab").Append(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup + "/" + path));
            File.Copy(path, backup + "/" + path);
        }
        for (int room = 0; room < Rooms.Length; room++)
        {
            string path = "Assets/Prefabs/Sections/" + Rooms[room] + ".prefab";
            string originalPrefab = File.ReadAllText(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = root.transform.Find(GroupName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                BuildRoom(root.transform, room, walls);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PreserveGameplay(originalPrefab, path);
        }
        // A skyless indoor scene needs a deliberate ambient probe rather than an empty skybox.
        SceneManager.SetActiveScene(scene);
        var surface = All<NavMeshSurface>(scene).Single();
        var agents = All<DroneBrain>(scene).Select(b => b.GetComponent<NavMeshAgent>()).ToArray();
        if (agents.Length != 4 || agents.Any(a => a == null) || surface.navMeshData == null)
            throw new InvalidOperationException("Expected four authored agents and a saved NavMesh surface.");
        var startup = surface.GetComponent<LabNavigationStartup>();
        if (startup == null) startup = surface.gameObject.AddComponent<LabNavigationStartup>();
        startup.Configure(agents);
        EditorUtility.SetDirty(startup);
        foreach (var agent in agents)
        {
            agent.enabled = false;
            EditorUtility.SetDirty(agent);
            if (PrefabUtility.IsPartOfPrefabInstance(agent)) PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.38f, .43f, .48f);
        RenderSettings.ambientEquatorColor = new Color(.25f, .29f, .32f);
        RenderSettings.ambientGroundColor = new Color(.13f, .15f, .17f);
        RenderSettings.ambientIntensity = 1f;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("MainLab save failed.");
        PreserveGameplay(originalScene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("WORLD_BUILDER_FINISH_APPLIED backup=" + backup);
    }

    private static void PreserveGameplay(string before, string path)
    {
        // Resaving older Unity assets can materialize new defaults or discard unknown teammate fields.
        // Environment authoring must preserve every existing production-script block verbatim.
        before = before.Replace("\r\n", "\n");
        string after = File.ReadAllText(path).Replace("\r\n", "\n");
        foreach (string block in System.Text.RegularExpressions.Regex.Split(before, @"(?=^--- !u!)",
            System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            var script = System.Text.RegularExpressions.Regex.Match(block, @"m_Script:.*guid: (\w+)");
            if (!script.Success || !AssetDatabase.GUIDToAssetPath(script.Groups[1].Value).StartsWith("Assets/Scripts/")) continue;
            string header = block.Split('\n')[0];
            string pattern = System.Text.RegularExpressions.Regex.Escape(header) + @"\n.*?(?=^--- !u!|\z)";
            if (!System.Text.RegularExpressions.Regex.IsMatch(after, pattern,
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline))
                throw new InvalidOperationException("Cannot preserve existing gameplay block: " + header);
            after = System.Text.RegularExpressions.Regex.Replace(after, pattern, _ => block,
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline);
        }
        if (after != File.ReadAllText(path))
        {
            File.WriteAllText(path, after);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void BuildRoom(Transform root, int room, Bounds[] walls)
    {
        var group = new GameObject(GroupName).transform;
        group.SetParent(root, false);
        var batches = new[] {
            new Batch(Material("Painted ivory", new Color(.51f, .55f, .54f), .08f)),
            new Batch(Material("Graphite plinth", new Color(.12f, .16f, .19f), .18f)),
            new Batch(Material("Brushed frame", new Color(.30f, .36f, .39f), .40f)),
            new Batch(Material(Rooms[room] + " identification", Accents[room], .10f)),
            new Batch(Material("Service recess", new Color(.055f, .073f, .087f), .05f)),
            new Batch(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Room lettering.mat")) };
        int bays = 0;
        for (int side = 0; side < 4; side++)
        {
            bool xWall = side < 2;
            int sign = side % 2 == 0 ? -1 : 1;
            Vector3 normal = xWall ? Vector3.right * -sign : Vector3.forward * -sign;
            Vector3 tangent = Vector3.Cross(Vector3.up, normal);
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            var segments = new List<Vector3>();
            foreach (Bounds wall in walls)
            {
                Vector3 local = wall.center - Centres[room];
                if (Mathf.Abs((xWall ? local.x : local.z) - sign * 10f) > .7f) continue;
                if ((xWall ? wall.size.x : wall.size.z) > .85f) continue;
                float half = (xWall ? wall.extents.z : wall.extents.x);
                float along = xWall ? local.z : local.x;
                float start = Mathf.Max(-9.70f, along - half + .06f);
                float end = Mathf.Min(9.70f, along + half - .06f);
                if (end - start < .40f) continue;
                // Follow the real inner wall face, including asymmetric gate/exit openings.
                float face = (xWall ? local.x : local.z) - sign * (xWall ? wall.extents.x : wall.extents.z);
                Vector3 origin = xWall ? new Vector3(face, 0, 0) : new Vector3(0, 0, face);
                float direction = xWall ? tangent.z : tangent.x;
                float middle = (start + end) * .5f * direction;
                float length = end - start;
                segments.Add(new Vector3(middle, length, face));
                int count = Mathf.CeilToInt(length / 2.5f);
                float width = length / count;
                for (int bay = 0; bay < count; bay++)
                {
                    float t = middle - length * .5f + (bay + .5f) * width;
                    Action<int, float, float, float, float, float> box = (mat, y, w, h, depth, lift) =>
                        batches[mat].Box(origin + tangent * t + Vector3.up * y + normal * lift,
                            new Vector3(w, h, depth), rotation);
                    box(4, 1.61f, width - .015f, 2.86f, .012f, .012f);
                    box(1, .55f, width - .075f, .70f, .018f, .026f);
                    box(0, 2.04f, width - .085f, 2.15f, .022f, .029f);
                    box(2, .17f, width - .025f, .12f, .035f, .040f);
                    box(2, .965f, width - .025f, .045f, .032f, .043f);
                    box(3, 2.90f, width - .09f, .075f, .012f, .047f);
                    // Recessed service vents in selected bays; restrained, repeated industrial detail.
                    if ((bay + side) % 3 == 0 && width > 1.3f)
                    {
                        box(2, 2.40f, .74f, .36f, .025f, .052f);
                        for (int slit = 0; slit < 4; slit++)
                            box(4, 2.29f + slit * .07f, .62f, .025f, .012f, .071f);
                    }
                    bays++;
                }
            }
            // One plaque on each of two solid walls, never across a gate opening.
            if (side % 2 == 0 && segments.Count > 0)
            {
                Vector3 segment = segments.OrderByDescending(s => s.y).First();
                if (segment.y > 3.3f)
                {
                    Vector3 origin = xWall ? new Vector3(segment.z, 0, 0) : new Vector3(0, 0, segment.z);
                    Vector3 plaque = origin + tangent * segment.x + Vector3.up * 1.45f + normal * .085f;
                    batches[4].Box(plaque, new Vector3(3.10f, .76f, .035f), rotation);
                    batches[3].Box(plaque - tangent * 1.46f + normal * .03f,
                        new Vector3(.06f, .58f, .02f), rotation);
                    Vector3 lettering = plaque + tangent * 1.31f + normal * .032f;
                    Quaternion face = Quaternion.LookRotation(-normal, Vector3.up);
                    AddText(batches[5], "0" + (room + 1) + " / " + Rooms[room].ToUpperInvariant(),
                        lettering + Vector3.up * .045f, face, .28f);
                    AddText(batches[5], Subtitles[room], lettering - Vector3.up * .22f, face, .17f);
                }
            }
        }
        for (int i = 0; i < batches.Length; i++)
        {
            Batch batch = batches[i];
            var generated = new Mesh { name = Rooms[room] + " " + batch.Material.name };
            generated.CombineMeshes(batch.Pieces.ToArray(), true, true);
            foreach (var piece in batch.Pieces)
                if (piece.mesh != CubeMesh) Object.DestroyImmediate(piece.mesh);
            generated.RecalculateBounds();
            string path = Folder + "/" + Rooms[room] + "_" + i + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { AssetDatabase.CreateAsset(generated, path); mesh = generated; }
            else { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); }
            EditorUtility.SetDirty(mesh);
            var item = new GameObject(batch.Material.name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(group, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(item, StaticEditorFlags.BatchingStatic);
        }
        Debug.Log("WORLD_BUILDER_ROOM " + Rooms[room] + " panelBays=" + bays + " meshRenderers=6");
    }

    private static void PrepareSignage()
    {
        signageFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        signageFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /", 64, FontStyle.Normal);
        var source = signageFont.material.mainTexture;
        var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) {
            name = "Room lettering atlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        try
        {
            Graphics.Blit(source, target); RenderTexture.active = target;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply();
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        string path = Folder + "/Room lettering atlas.asset";
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (atlas == null) { AssetDatabase.CreateAsset(copy, path); atlas = copy; }
        else { EditorUtility.CopySerialized(copy, atlas); Object.DestroyImmediate(copy); }
        EditorUtility.SetDirty(atlas);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Room lettering.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("ShiftFour/World Builder Signage")) { name = "Room lettering" };
            AssetDatabase.CreateAsset(material, Folder + "/Room lettering.mat");
        }
        material.SetTexture("_BaseMap", atlas);
        EditorUtility.SetDirty(material);
    }
    private static void AddText(Batch batch, string text, Vector3 position, Quaternion rotation, float emHeight)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        float advance = 0, scale = emHeight / 64f;
        foreach (char letter in text)
        {
            if (!signageFont.GetCharacterInfo(letter, out CharacterInfo glyph, 64, FontStyle.Normal))
                throw new InvalidOperationException("Missing signage glyph: " + letter);
            int first = vertices.Count;
            vertices.Add(new Vector3(advance + glyph.minX * scale, glyph.minY * scale, 0));
            vertices.Add(new Vector3(advance + glyph.maxX * scale, glyph.minY * scale, 0));
            vertices.Add(new Vector3(advance + glyph.maxX * scale, glyph.maxY * scale, 0));
            vertices.Add(new Vector3(advance + glyph.minX * scale, glyph.maxY * scale, 0));
            uv.Add(glyph.uvBottomLeft); uv.Add(glyph.uvBottomRight); uv.Add(glyph.uvTopRight); uv.Add(glyph.uvTopLeft);
            triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            advance += glyph.advance * scale;
        }
        if (advance > 2.65f) throw new InvalidOperationException("Sign exceeds its backing plate: " + text);
        var mesh = new Mesh { name = "Baked lettering" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        batch.Pieces.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, Vector3.one) });
    }

    private static Material Material(string name, Color color, float metallic)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
        material = new Material(shader) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .24f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    private static void RequireSavedEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before finishing the environment.");
    }

    // Batch entry point: render the actual scene before/after and prove the physics data stays identical.
    public static void FinishAndVerify()
    {
        try
        {
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            string beforePhysics = PhysicsSignature(scene);
            string beforeNav = JsonUtility.ToJson(NavMesh.CalculateTriangulation());
            CaptureRooms(scene, "before");
            Apply();
            scene = EditorSceneManager.OpenScene(ScenePath);
            Physics.SyncTransforms();
            if (PhysicsSignature(scene) != beforePhysics) throw new Exception("Environment finish changed existing colliders.");
            if (JsonUtility.ToJson(NavMesh.CalculateTriangulation()) != beforeNav) throw new Exception("Environment finish changed baked navigation.");
            foreach (var room in Rooms)
            {
                var root = scene.GetRootGameObjects().Single(r =>
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == "Assets/Prefabs/Sections/" + room + ".prefab");
                var group = root.transform.Find(GroupName);
                if (group == null || group.GetComponentsInChildren<Collider>().Length != 0 ||
                    group.GetComponentsInChildren<MeshFilter>().Length != 6)
                    throw new Exception("Invalid render-only finish for " + room);
            }
            if (All<LabDoor>(scene).Length != 4 || All<DroneBrain>(scene).Length != 4)
                throw new Exception("Expected four gates and four drone policies.");
            foreach (var renderer in All<Renderer>(scene))
                foreach (var mat in renderer.sharedMaterials)
                    if (mat != null && (mat.shader == null || mat.shader.name.Contains("InternalError")))
                        throw new Exception("Broken material: " + renderer.name);
            CaptureRooms(scene, "after");
            CaptureLayout(scene);
            int floorChecks = VerifyFloorCoverage();
            File.WriteAllText(Output + "/environment-audit.json", JsonUtility.ToJson(new Audit {
                unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName,
                physicsUnchanged = true, navMeshUnchanged = true,
                gates = All<LabDoor>(scene).Length, drones = All<DroneBrain>(scene).Length, wallRenderers = 24,
                navVertices = NavMesh.CalculateTriangulation().vertices.Length, floorChecks = floorChecks }, true));
            Debug.Log("WORLD_BUILDER_ENVIRONMENT_AUDIT_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    [Serializable] private class Audit
    {
        public string unity, gpu;
        public bool physicsUnchanged, navMeshUnchanged;
        public int gates, drones, wallRenderers, navVertices, floorChecks;
    }
    private static int VerifyFloorCoverage()
    {
        int count = 0;
        foreach (var centre in Centres)
            for (int x = -9; x <= 9; x += 3)
                for (int z = -9; z <= 9; z += 3)
                {
                    var from = centre + new Vector3(x, .12f, z);
                    if (!Physics.Raycast(from, Vector3.down, out var hit, .6f, ~0, QueryTriggerInteraction.Ignore) ||
                        hit.normal.y < .9f || Mathf.Abs(hit.point.y) > .08f)
                        throw new Exception("Missing level floor near " + from);
                    count++;
                }
        return count;
    }
    private static string PhysicsSignature(Scene scene)
    {
        Physics.SyncTransforms();
        return string.Join("\n", All<Collider>(scene).Select(c => {
            string path = c.name;
            for (var p = c.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path + "|" + c.GetType().Name + "|" + c.enabled + "|" + c.isTrigger + "|" +
                c.bounds.center.ToString("F4") + "|" + c.bounds.size.ToString("F4");
        }).OrderBy(s => s));
    }
    private static void CaptureRooms(Scene scene, string prefix)
    {
        var cameraObject = new GameObject("Environment evidence camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.nearClipPlane = .05f; camera.farClipPlane = 100;
        camera.fieldOfView = 74; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.05f, .06f, .07f);
        try
        {
            for (int i = 0; i < Rooms.Length; i++)
            {
                camera.transform.position = Centres[i] + new Vector3(-7.2f, 1.5f, -6.5f);
                camera.transform.LookAt(Centres[i] + new Vector3(2.3f, 1.65f, 3.7f));
                Render(camera, Output + "/" + prefix + "-" + Rooms[i] + ".png");
                camera.transform.position = Centres[i] + new Vector3(5.8f, 1.5f, 4.3f);
                camera.transform.LookAt(Centres[i] + new Vector3(-5f, 1.8f, -7.5f));
                Render(camera, Output + "/" + prefix + "-" + Rooms[i] + "-walls.png");
            }
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }
    private static void Render(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(1440, 900, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(texture);
        }
    }
    private static void CaptureLayout(Scene scene)
    {
        var hidden = All<Renderer>(scene).Where(r => r.enabled && r.bounds.min.y > 2.85f).ToArray();
        foreach (var renderer in hidden) renderer.enabled = false;
        var cameraObject = new GameObject("Layout evidence camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 24;
        camera.transform.position = new Vector3(0, 60, 0);
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .055f, .065f);
        GameObject overlay = null;
        Mesh navMesh = null;
        Material navMaterial = null;
        try
        {
            Render(camera, Output + "/layout-roof-hidden-for-evidence.png");
            var triangulation = NavMesh.CalculateTriangulation();
            navMesh = new Mesh { name = "Temporary navigation evidence" };
            navMesh.vertices = triangulation.vertices.Select(v => v + Vector3.up * .045f).ToArray();
            // Show the playable ground layer; disconnected overhead baked surfaces would hide the room below.
            var ground = new List<int>();
            for (int i = 0; i < triangulation.indices.Length; i += 3)
                if (triangulation.vertices[triangulation.indices[i]].y < .5f &&
                    triangulation.vertices[triangulation.indices[i + 1]].y < .5f &&
                    triangulation.vertices[triangulation.indices[i + 2]].y < .5f)
                    ground.AddRange(new[] { triangulation.indices[i], triangulation.indices[i + 1], triangulation.indices[i + 2] });
            navMesh.triangles = ground.ToArray();
            navMesh.RecalculateNormals();
            navMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            navMaterial.SetColor("_BaseColor", new Color(.16f, .65f, .54f));
            overlay = new GameObject("Temporary NavMesh evidence overlay", typeof(MeshFilter), typeof(MeshRenderer));
            overlay.GetComponent<MeshFilter>().sharedMesh = navMesh;
            overlay.GetComponent<MeshRenderer>().sharedMaterial = navMaterial;
            Render(camera, Output + "/navigation-roof-hidden-for-evidence.png");
        }
        finally
        {
            foreach (var renderer in hidden) renderer.enabled = true;
            if (overlay != null) Object.DestroyImmediate(overlay);
            if (navMesh != null) Object.DestroyImmediate(navMesh);
            if (navMaterial != null) Object.DestroyImmediate(navMaterial);
            Object.DestroyImmediate(cameraObject);
        }
    }

    public static void BuildDesktop()
    {
        try
        {
            Directory.CreateDirectory("Builds/WorldBuilderGV");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/WorldBuilderGV/ShiftFour.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/build-result.txt", report.summary.result + "\n" +
                report.summary.totalErrors + " errors\n" + report.summary.totalWarnings + " warnings\n" + report.summary.totalTime);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
