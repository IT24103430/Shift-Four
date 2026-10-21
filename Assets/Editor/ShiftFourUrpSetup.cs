using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Unity 6 URP configuration: menu-driven so Unity serializes correct asset formats/GUIDs.
// Run after the Universal RP package finishes resolving in Package Manager.
public static class ShiftFourUrpSetup
{
    private const string SettingsFolder = "Assets/Settings";
    private const string PipelinePath = SettingsFolder + "/ShiftFour_URP.asset";
    private const string RendererPath = SettingsFolder + "/ShiftFour_URP_Renderer.asset";

    [MenuItem("Tools/Shift Four/Configure URP Project")]
    public static void Configure()
    {
        Type pipelineType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
        Type rendererType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
        if (pipelineType == null || rendererType == null)
        {
            EditorUtility.DisplayDialog("URP missing", "Install Universal RP using Package Manager; wait for compilation and retry.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");

        // Prefer the user's existing URP asset. Only create if none is available.
        RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
        if (asset == null)
        {
            string[] candidates = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" });
            if (candidates.Length > 0)
            {
                string first = AssetDatabase.GUIDToAssetPath(candidates[0]);
                asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(first);
                Debug.Log("Shift Four: using existing URP asset: " + first);
            }
        }
        if (asset == null)
        {
            ScriptableObject renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance(rendererType);
                renderer.name = "ShiftFour_URP_Renderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            ScriptableObject pipeline = ScriptableObject.CreateInstance(pipelineType);
            pipeline.name = "ShiftFour_URP";
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            SerializedObject serialized = new SerializedObject(pipeline);
            SerializedProperty list = serialized.FindProperty("m_RendererDataList");
            if (list == null)
            {
                AssetDatabase.DeleteAsset(PipelinePath);
                EditorUtility.DisplayDialog("URP asset requires setup", "Create a URP Asset (with Universal Renderer) in Assets/Settings using Unity's Create menu and rerun this command.", "OK");
                return;
            }
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (defaultIndex != null) defaultIndex.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            asset = pipeline as RenderPipelineAsset;
        }
        if (asset == null)
        {
            Debug.LogError("Shift Four: failed to construct a valid URP RenderPipelineAsset.");
            return;
        }
        GraphicsSettings.defaultRenderPipeline = asset;
        int selectedQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        QualitySettings.SetQualityLevel(selectedQuality, false);
        // ProjectSettings are Unity-owned and are saved by the Editor automatically.
        UpgradeMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shift Four: URP applied to Graphics and every Quality level. Restart Unity and check scene materials.");
        EditorUtility.DisplayDialog("Shift Four URP", "URP assigned across Graphics and Quality levels. Materials upgraded. Verify MainLab in Game view and restart Unity once.", "OK");
    }

    [MenuItem("Tools/Shift Four/Upgrade Materials to URP")]
    public static void UpgradeMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { Debug.LogError("Shift Four: URP/Lit shader not found. Resolve URP package first."); return; }
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || (material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/"))) continue;
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") :
                material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Undo.RecordObject(material, "Convert material to URP");
            material.shader = lit;
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            changed++;
        }
        Debug.Log("Shift Four: upgraded " + changed + " materials to URP/Lit. Imported model materials may also require manual inspection.");
    }

    private static Type FindType(string fullName)
    {
        foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            if (type.FullName == fullName) return type;
        return null;
    }
}
