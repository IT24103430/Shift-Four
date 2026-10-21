using System;
using ShiftFour;
using UnityEditor;
using UnityEngine;

public static class FirstPersonIdleAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Add Pistol Idle Settings Once")]
    public static void Install()
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<PistolPresentation>() == null) throw new InvalidOperationException("The pistol presentation is missing.");
            if (root.GetComponent<FirstPersonIdleMotion>() != null)
            {
                Debug.Log("Preserved the existing authored pistol idle settings."); return;
            }
            root.AddComponent<FirstPersonIdleMotion>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Saved configurable pistol idle settings; all pistol transforms and gameplay components preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
