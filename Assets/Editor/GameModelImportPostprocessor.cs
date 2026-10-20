using UnityEditor;
using UnityEngine;

public sealed class GameModelImportPostprocessor : AssetPostprocessor
{
    public override uint GetVersion() => 2;
    private bool IsGV => assetPath.EndsWith("_GV.fbx");
    private void OnPreprocessModel()
    {
        if (!IsGV) return;
        var importer = (ModelImporter)assetImporter;
        importer.preserveHierarchy = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.isReadable = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    private void OnPostprocessMaterial(Material material)
    {
        if (!IsGV) return;
        Color color = material.HasProperty("_Color") ? material.color : Color.gray;
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", .55f);
        material.SetFloat("_Smoothness", .45f);
        if (material.name.Contains("Glow") || material.name.Contains("Status") || material.name.Contains("Terminal"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
        }
    }
}
