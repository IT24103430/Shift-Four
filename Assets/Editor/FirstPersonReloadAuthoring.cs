using System;
using ShiftFour;
using UnityEditor;
using UnityEngine;

public static class FirstPersonReloadAuthoring
{
    [MenuItem("Tools/Shift Four/FPS/Add Detailed Pistol Reload Once")]
    public static void Install() => Save(false);

    [MenuItem("Tools/Shift Four/FPS/Apply Authored Magazine Grip")]
    public static void ApplyGripPose() => Save(true);

    private static void Save(bool applyGrip)
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var presentation = root.GetComponent<PistolPresentation>();
            if (presentation == null) throw new InvalidOperationException("Pistol presentation is missing.");
            Transform magazine = null;
            foreach (var part in root.GetComponentsInChildren<Transform>(true)) if (part.name == "FNFiveSeven_Magazine") { magazine = part; break; }
            if (magazine == null) throw new InvalidOperationException("Separate source magazine is missing.");
            var reload = root.GetComponent<FirstPersonReloadMotion>();
            if (reload == null) reload = root.AddComponent<FirstPersonReloadMotion>();
            var data = new SerializedObject(reload);
            var target = data.FindProperty("magazineGripTarget").objectReferenceValue as Transform;
            bool create = target == null;
            if (create || applyGrip)
            {
                if (create) { target = new GameObject("MagazineGripTarget").transform; target.SetParent(magazine, false); target.gameObject.layer = 2; }
                // Authored fit to the existing thumb/index surfaces at the magazine baseplate.
                Vector3 position = new Vector3(-.125165105f, -.076134369f, -.049704909f);
                Quaternion rotation = new Quaternion(.312664151f, .363212526f, -.766748428f, .427100599f).normalized;
                target.position = presentation.RecoilPivot.TransformPoint(position);
                target.rotation = presentation.RecoilPivot.rotation * rotation;
                data.FindProperty("magazineGripTarget").objectReferenceValue = target;
                data.FindProperty("forearmTwistLimit").floatValue = 75; data.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("handClearance").floatValue = .05f; data.ApplyModifiedPropertiesWithoutUndo();
            }
            var presenterData = new SerializedObject(presentation);
            presenterData.FindProperty("magazinePart").objectReferenceValue = magazine; presenterData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("Saved curve-driven reload and owned magazine wrist target; existing source mesh, pose, tuning and transforms preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
