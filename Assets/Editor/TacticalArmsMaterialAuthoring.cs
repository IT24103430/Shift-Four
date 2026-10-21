using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit editor authoring only. Runtime arms loading preserves these saved assets unchanged.
public static class TacticalArmsMaterialAuthoring
{
    private const string Folder = "Assets/Materials/FPS/Arms/";
    private static readonly string[] Names = { "GloveShell", "SleeveRipstop", "CuffElastic", "GloveReinforcement", "KnuckleRubber" };

    [MenuItem("Tools/Shift Four/FPS/Apply Tactical Arms Material Finish")]
    public static void ApplyFinish()
    {
        foreach (string name in Names)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");
            if (material == null) throw new FileNotFoundException("The owned arms material is missing: " + name);
            Configure(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        Debug.Log("Saved the tactical arms material finish; source maps, FBX, prefab bindings and scenes were preserved.");
    }

    public static void Configure(Material material)
    {
        int slot = Array.IndexOf(Names, material.name);
        if (slot < 0 || AssetDatabase.GetAssetPath(material) != Folder + material.name + ".mat")
            throw new InvalidOperationException("Material finishing is limited to the five owned arms assets.");
        if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit" ||
            material.GetTexture("_BaseMap") == null || material.GetTexture("_BumpMap") == null)
            throw new InvalidOperationException("Preserve and supply the existing URP base/normal maps before finishing " + material.name);

        bool fabric = slot == 1 || slot == 2;
        string mapPath = Folder + (fabric ? "Ripstop" : "Leather") + "MetallicSmoothness.tga";
        var importer = AssetImporter.GetAtPath(mapPath) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Pack smoothness from the existing base maps first.", mapPath);
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
        if (mask == null) throw new InvalidOperationException("The arms smoothness mask did not import: " + mapPath);

        Color[] colors = {
            new Color(.060f, .066f, .073f), new Color(.105f, .135f, .150f),
            new Color(.027f, .035f, .042f), new Color(.082f, .090f, .098f), new Color(.028f, .033f, .038f)
        };
        float[] smoothnessCaps = { .26f, .135f, .09f, .28f, .20f };
        float[] normalStrength = { .14f, .16f, .07f, .10f, .06f };
        Vector2 tiling = fabric ? new Vector2(3, 5) : new Vector2(6, 6);
        material.SetColor("_BaseColor", colors[slot]);
        // Keep URP's hidden lightmapping/legacy aliases synchronized with the authored base surface.
        material.SetColor("_Color", colors[slot]);
        material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
        material.SetTextureScale("_MainTex", tiling);
        material.SetTextureOffset("_MainTex", material.GetTextureOffset("_BaseMap"));
        material.SetFloat("_WorkflowMode", 1); // Metallic workflow; mask R is zero throughout.
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", smoothnessCaps[slot]);
        material.SetFloat("_SmoothnessTextureChannel", 0);
        material.SetFloat("_BumpScale", normalStrength[slot]);
        material.SetFloat("_SpecularHighlights", 1);
        material.SetFloat("_EnvironmentReflections", 1);
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetTextureScale("_BaseMap", tiling);
        material.SetTextureScale("_BumpMap", tiling);
        material.SetTextureScale("_MetallicGlossMap", tiling);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        EditorUtility.SetDirty(material);
    }
}
