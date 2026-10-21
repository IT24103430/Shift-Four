using System;
using System.IO;
using System.Linq;
using ShiftFour;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Creates only the owned arms assets. Never saves a scene or edits a vendor/legacy asset.
public static class TacticalArmsAssetBuilder
{
    public const string ModelPath = "Assets/Models/FPS/SF_TacticalArms.fbx";
    public const string PrefabPath = "Assets/Resources/Weapons/FirstPersonArmsRig.prefab";
    public const string SurfaceFolder = "Assets/Materials/FPS/Arms";

    [MenuItem("Tools/Shift Four/FPS/Create Tactical Arms Prefab Once")]
    public static void Create()
    {
        if (File.Exists(PrefabPath))
            throw new InvalidOperationException("The owned arms prefab already exists. Preserve its authored changes.");
        if (!File.Exists(ModelPath))
            throw new FileNotFoundException("Export the derived tactical arms FBX first.", ModelPath);
        Directory.CreateDirectory(SurfaceFolder);
        AssetDatabase.Refresh();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP/Lit is required for the arms.");
        string[] names = { "GloveShell", "SleeveRipstop", "CuffElastic", "GloveReinforcement", "KnuckleRubber" };
        Color[] colors = {
            new Color(.035f, .045f, .035f), new Color(.16f, .20f, .15f),
            new Color(.045f, .055f, .04f), new Color(.075f, .09f, .06f), new Color(.025f, .03f, .025f)
        };
        float[] smoothness = { .24f, .12f, .1f, .3f, .22f };
        var materials = new Material[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string path = SurfaceFolder + "/" + names[i] + ".mat";
            if (File.Exists(path)) throw new InvalidOperationException("Preserve the existing material: " + path);
            bool fabric = i == 1 || i == 2;
            string surface = fabric ? "Ripstop" : "Leather";
            var albedo = Texture(surface + "Base", false);
            var normal = Texture(surface + "Normal", true);
            var mat = new Material(shader) { name = names[i] };
            mat.SetColor("_BaseColor", colors[i]);
            mat.SetFloat("_Smoothness", smoothness[i]);
            mat.SetFloat("_Metallic", 0);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", fabric ? .24f : .18f);
            mat.SetTextureScale("_BaseMap", fabric ? new Vector2(3, 5) : new Vector2(4, 4));
            mat.SetTextureScale("_BumpMap", mat.GetTextureScale("_BaseMap"));
            mat.EnableKeyword("_NORMALMAP");
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, path);
            TacticalArmsMaterialAuthoring.Configure(mat);
            materials[i] = mat;
        }
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.preserveHierarchy = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.optimizeGameObjects = false;
        importer.addCollider = false;
        // The modest mesh remains readable for focused weight/deformation verification and tooling.
        importer.isReadable = true;
        importer.skinWeights = ModelImporterSkinWeights.Standard;
        importer.maxBonesPerVertex = 4;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        for (int i = 0; i < names.Length; i++)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), names[i]), materials[i]);
        importer.SaveAndReimport();

        var root = new GameObject("FirstPersonArmsRig");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            model.transform.SetParent(root.transform, false);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length != 1) throw new InvalidOperationException("The arms must export as one skin.");
            var skin = skins[0];
            if (skin.sharedMaterials.Any(m => m == null || !materials.Contains(m)))
                throw new InvalidOperationException("Every arms surface must use a project-owned material.");
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
            skin.updateWhenOffscreen = true;
            skin.quality = SkinQuality.Bone4;
            skin.localBounds = skin.sharedMesh.bounds;
            var bindings = root.AddComponent<FirstPersonArmsRig>();
            var data = new SerializedObject(bindings);
            foreach (string side in new[] { "Left", "Right" })
            {
                string suffix = side == "Left" ? ".L" : ".R";
                string prefix = side == "Left" ? "left" : "right";
                data.FindProperty(prefix + "UpperArm").objectReferenceValue = Bone(root, "upper_arm" + suffix);
                data.FindProperty(prefix + "Forearm").objectReferenceValue = Bone(root, "forearm" + suffix);
                data.FindProperty(prefix + "Hand").objectReferenceValue = Bone(root, "hand" + suffix);
            }
            data.FindProperty("skin").objectReferenceValue = skin;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (!bindings.IsValid) throw new InvalidOperationException("The fitted bone hierarchy is invalid.");
            if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Presentation arms cannot contain physics components.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created the owned tactical arms rig: " + PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static Transform Bone(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

    private static Texture2D Texture(string name, bool normal)
    {
        string path = SurfaceFolder + "/" + name + ".tga";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Generate the owned surface maps first.", path);
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) throw new InvalidOperationException("The arms surface did not import as a Texture2D: " + path);
        return texture;
    }

    // Read-only export for Blender fitting: use Unity's actual imported part positions and axes.
    public static void ExportPistolReference()
    {
        string output = Environment.GetEnvironmentVariable("SHIFTFOUR_ARMS_REFERENCE");
        if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set SHIFTFOUR_ARMS_REFERENCE to a temporary JSON path.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Free Gun/Model/FN_Five_Seven.fbx");
        var reference = new PistolReference { parts = model.GetComponentsInChildren<MeshFilter>()
            .Where(f => !f.name.Contains("Bullet")).Select(f => new ReferencePart {
                name = f.name, vertices = f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray(),
                uv = f.sharedMesh.uv, triangles = f.sharedMesh.triangles
            }).ToArray() };
        File.WriteAllText(output, JsonUtility.ToJson(reference));
    }

    [Serializable] private sealed class PistolReference { public ReferencePart[] parts; }
    [Serializable] private sealed class ReferencePart { public string name; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
}
