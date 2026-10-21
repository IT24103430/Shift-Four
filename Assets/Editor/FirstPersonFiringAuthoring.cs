using System;
using ShiftFour;
using UnityEditor;
using UnityEngine;

public static class FirstPersonFiringAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Save Pistol Firing Settings")]
    public static void Save()
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<PistolPresentation>() == null) throw new InvalidOperationException("The pistol presentation is missing.");
            // Materialize new serialized defaults while preserving all existing artist tuning.
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Saved firing presentation settings; existing tuning and prefab transforms preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
