using System;
using System.IO;
using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Creates only new project-owned assets. Never saves MainLab or the PolyOne source prefab.
public static class FiveSevenAssetBuilder
{
    public const string PrefabPath = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
    public const string DefinitionPath = "Assets/Resources/Weapons/FNFiveSeven.asset";
    public const string RangePath = "Assets/Scenes/FPS/FiveSevenRange.unity";
    private const string Source = "Assets/PolyOne/Free Gun/Prefabs/FN_Five_Seven.prefab";
    private const string Model = "Assets/PolyOne/Free Gun/Model/FN_Five_Seven.fbx";
    private const string Effects = "Assets/Materials/FPS";

    [MenuItem("Tools/Shift Four/Create Five-seveN Assets Once")]
    public static void Create()
    {
        if (File.Exists(PrefabPath) || File.Exists(DefinitionPath) || File.Exists(RangePath))
            throw new InvalidOperationException("Five-seveN assets already exist. Stopped to preserve authored changes and GUIDs.");
        foreach (string path in new[] { "Assets/Prefabs/FPS", "Assets/Resources/Weapons", "Assets/Scenes/FPS", Effects }) Ensure(path);
        Material flash = FlashMaterial();
        Scene staging = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("FNFiveSevenViewmodel");
        SceneManager.MoveGameObjectToScene(root, staging);
        try
        {
            var viewmodel = root.AddComponent<FirstPersonViewmodel>();
            Transform pivot = Child(root.transform, "RecoilPivot", new Vector3(.12f, -.12f, .30f));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source), staging);
            visual.transform.SetParent(pivot, false);
            // Restore the source FBX's actual part placement using nested-prefab overrides.
            Transform reference = AssetDatabase.LoadAssetAtPath<GameObject>(Model).transform;
            visual.transform.localPosition = reference.localPosition;
            foreach (Transform part in visual.transform)
            {
                Transform sourcePart = reference.Find(part.name);
                if (sourcePart == null) throw new InvalidOperationException("Unexpected imported pistol child: " + part.name);
                part.SetLocalPositionAndRotation(sourcePart.localPosition, sourcePart.localRotation);
                part.localScale = sourcePart.localScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
            }
            // A chamber round is not a projectile or a casing effect.
            visual.transform.Find("FNFiveSeven__Bullet").gameObject.SetActive(false);
            Transform muzzle = Child(visual.transform, "MuzzlePoint", new Vector3(0f, .0566f, .105f));
            var flashObject = GameObject.CreatePrimitive(PrimitiveType.Quad); flashObject.name = "MuzzleFlash";
            UnityEngine.Object.DestroyImmediate(flashObject.GetComponent<Collider>());
            flashObject.transform.SetParent(muzzle, false); flashObject.transform.localPosition = Vector3.forward * .025f;
            flashObject.transform.localScale = Vector3.one * .045f;
            var renderer = flashObject.GetComponent<MeshRenderer>(); renderer.sharedMaterial = flash; renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var presentation = root.AddComponent<PistolPresentation>();
            var data = new SerializedObject(presentation);
            data.FindProperty("recoilPivot").objectReferenceValue = pivot;
            data.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
            data.FindProperty("muzzleFlash").objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
            foreach (MeshRenderer mesh in root.GetComponentsInChildren<MeshRenderer>(true))
            { mesh.shadowCastingMode = ShadowCastingMode.Off; mesh.receiveShadows = false; }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            var tuning = new SerializedObject(definition);
            tuning.FindProperty("displayName").stringValue = "FN Five-seveN";
            tuning.FindProperty("viewmodelPrefab").objectReferenceValue = saved.GetComponent<FirstPersonViewmodel>();
            tuning.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(staging); }
        CreateRangeOnce(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Shift Four/Create Five-seveN Range Once")]
    public static void CreateRangeOnce()
    {
        if (File.Exists(RangePath)) throw new InvalidOperationException("The isolated range already exists; preserving it.");
        Ensure("Assets/Scenes/FPS"); Ensure(Effects);
        Scene previous = SceneManager.GetActiveScene();
        bool emptyBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            emptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .55f, .65f);
            var light = new GameObject("Range light").AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Material floor = RangeMaterial("RangeFloor", new Color(.17f, .22f, .28f));
            Material wall = RangeMaterial("RangeWall", new Color(.34f, .4f, .48f));
            Material orange = RangeMaterial("RangeTarget", new Color(.9f, .35f, .1f));
            Box("Floor", new Vector3(0, -.5f, 3), new Vector3(12, 1, 18), floor);
            Box("Backstop", new Vector3(0, 2, 11), new Vector3(12, 5, .5f), wall);
            Box("Left wall", new Vector3(-6, 2, 3), new Vector3(.3f, 5, 18), wall);
            Box("Right wall", new Vector3(6, 2, 3), new Vector3(.3f, 5, 18), wall);
            var target = Box("Target - 25 damage per shot", new Vector3(0, 1.3f, 7), new Vector3(.7f, 1f, .25f), orange).AddComponent<Health>();
            target.Configure(100f, false);
            Box("Jump platform", new Vector3(2, .2f, 3), new Vector3(2, .4f, 2), wall);
            Box("Solid cover", new Vector3(-2, .7f, 4), new Vector3(1.2f, 1.4f, .4f), wall);
            var player = new GameObject("Player");
            var capsule = player.AddComponent<CharacterController>(); capsule.height = 1.8f; capsule.radius = .35f;
            capsule.center = Vector3.up * .9f; capsule.skinWidth = .05f; capsule.minMoveDistance = 0f;
            var camera = Child(player.transform, "PlayerCamera", Vector3.up * 1.3f).gameObject.AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 60f; camera.nearClipPlane = .05f;
            camera.backgroundColor = new Color(.08f, .11f, .15f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            player.AddComponent<PlayerController>().Configure(camera);
            var hud = new GameObject("Range HUD").AddComponent<FiveSevenRangeHud>();
            var data = new SerializedObject(hud); data.FindProperty("target").objectReferenceValue = target;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, RangePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
    }

    private static Material FlashMaterial()
    {
        string texturePath = Effects + "/MuzzleFlash.png";
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f)));
        }
        texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", new Color(1f, .7f, .22f, .9f)); material.SetFloat("_Cull", 0f);
        material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 2f); material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
        AssetDatabase.CreateAsset(material, Effects + "/MuzzleFlash.mat"); return material;
    }

    private static Material RangeMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, Effects + "/" + name + ".mat"); return material;
    }
    private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position;
        go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    private static Transform Child(Transform parent, string name, Vector3 position)
    {
        var child = new GameObject(name).transform; child.SetParent(parent, false); child.localPosition = position; return child;
    }
    private static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); Ensure(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
