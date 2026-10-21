using ShiftFour;
using UnityEditor;
using UnityEngine;
public static class FirstPersonViewmodelFramingAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Add Reviewed Pistol Framing Once")]
    public static void Install()
    {
        const string path="Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{if(root.GetComponent<FirstPersonViewmodelFraming>()==null){root.AddComponent<FirstPersonViewmodelFraming>();PrefabUtility.SaveAsPrefabAsset(root,path);}}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
