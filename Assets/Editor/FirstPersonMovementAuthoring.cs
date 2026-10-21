using System;
using ShiftFour;
using UnityEditor;
using UnityEngine;

public static class FirstPersonMovementAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Add Pistol Movement Settings Once")]
    public static void Install()
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<PistolPresentation>() == null) throw new InvalidOperationException("The pistol presentation is missing.");
            if (root.GetComponent<FirstPersonMovementMotion>() != null)
            {
                Debug.Log("Preserved existing authored pistol movement settings."); return;
            }
            root.AddComponent<FirstPersonMovementMotion>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Saved presentation-only walking/jump/landing settings on the pistol prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
