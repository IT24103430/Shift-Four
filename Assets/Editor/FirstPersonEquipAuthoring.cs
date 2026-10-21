using ShiftFour;
using UnityEditor;
using UnityEngine;

public static class FirstPersonEquipAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Add Pistol Equip Settings Once")]
    public static void Install()
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<FirstPersonEquipMotion>() == null)
            {
                root.AddComponent<FirstPersonEquipMotion>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            Debug.Log("Owned pistol equip settings installed; source transforms, reload data and existing tuning preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
