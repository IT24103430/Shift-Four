using System;
using System.IO;
using System.Linq;
using ShiftFour;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FirstPersonWeaponGripAuthoring
{
    public const string DirectoryPath = "Assets/Data/FPS/WeaponGrips";
    public const string PreviewPath = "Assets/Prefabs/FPS/GripPreviews";
    private static readonly string[] Ids = { "FNFiveSeven", "HKMP5", "M16A1", "AK47", "BarrettM82A1" };
    private static readonly string[] Models = { "FN_Five_Seven", "HK_MP5", "M16A1", "Ak47", "Barrett_M82A1" };
    private static readonly Vector3[] Main = { Vector3.zero, new Vector3(.06f,-.033f,-.005f), new Vector3(.205f,-.045f,-.004f), new Vector3(.14f,-.027f,.004f), new Vector3(.32f,-.067f,-.007f) };
    private static readonly Vector3[] Support = { Vector3.zero, new Vector3(-.16f,.025f,-.005f), new Vector3(-.01f,.015f,-.004f), new Vector3(-.12f,.025f,.004f), new Vector3(.05f,-.01f,-.007f) };
    private static readonly float[] Advance = { 0,.25f,.25f,.29f,.30f };
    [MenuItem("Tools/Shift Four/FPS/Create Missing Weapon Grip Profiles")]
    public static void CreateMissing()
    {
        Ensure(DirectoryPath); Ensure(PreviewPath); Ensure("Assets/Materials/FPS/WeaponPreviews");
        for (int i = 0; i < Ids.Length; i++)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Free Gun/Model/" + Models[i] + ".fbx");
            if (source == null) throw new InvalidOperationException("Missing imported model: " + Models[i]);
            string path = DirectoryPath + "/" + Ids[i] + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<FirstPersonWeaponGripConfiguration>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FirstPersonWeaponGripConfiguration>(); var data = new SerializedObject(profile);
                data.FindProperty("weaponId").stringValue = Ids[i]; data.FindProperty("sourceModel").objectReferenceValue = source;
                data.FindProperty("bodyMesh").objectReferenceValue = source.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.EndsWith("_Body")).sharedMesh;
                data.FindProperty("weaponClass").enumValueIndex = i == 0 ? 0 : i == 1 ? 1 : i == 4 ? 3 : 2;
                Vector3 right = new Vector3(.0300000943f,-.022000011f,-.147999972f);
                Quaternion r = new Quaternion(.545035481f,.405575484f,.508793175f,.528748155f);
                Quaternion l = new Quaternion(.430233061f,-.323440284f,-.575801432f,.615417421f);
                data.FindProperty("rightWristPosition").vector3Value = right; data.FindProperty("rightWristRotation").quaternionValue = r;
                data.FindProperty("rightElbowHint").vector3Value = new Vector3(.204037234f,-.165246531f,-.175625414f);
                data.FindProperty("leftElbowHint").vector3Value = new Vector3(-.223871663f,-.209403649f,-.173080176f);
                if (i == 0)
                {
                    data.FindProperty("leftWristPosition").vector3Value = new Vector3(-.071999989f,-.052000046f,-.111999966f);
                    data.FindProperty("leftWristRotation").quaternionValue = l;
                    data.FindProperty("reloadPresentation").enumValueIndex = 1;
                    data.FindProperty("validationNotes").stringValue = "Existing playable pistol pose retained. Known finger wrap/weight and reload pinky clearance limits remain; see GRIP.md and RELOAD.md.";
                }
                else
                {
                    Quaternion modelRotation = Quaternion.Euler(0,90,0);
                    Vector3 offset = new Vector3(0,-.028f,-.063f) - modelRotation * Main[i];
                    Vector3 support = offset + modelRotation * Support[i];
                    data.FindProperty("modelPosition").vector3Value = offset;
                    data.FindProperty("modelEuler").vector3Value = new Vector3(0,90,0);
                    data.FindProperty("armsRootPosition").vector3Value = new Vector3(.04f,-.02f,Advance[i]);
                    data.FindProperty("leftWristPosition").vector3Value = support + new Vector3(.025f,-.055f,-.054f);
                    data.FindProperty("leftWristRotation").quaternionValue = Quaternion.AngleAxis(90,Vector3.forward) * l;
                    data.FindProperty("dominantGripCenter").vector3Value = Main[i]; data.FindProperty("supportGripCenter").vector3Value = Support[i];
                    data.FindProperty("forearmPronation").floatValue = 80;
                    data.FindProperty("idleAmplitude").floatValue = i == 4 ? .0004f : .0006f;
                    data.FindProperty("validationNotes").stringValue = "Prepared geometry-based holding draft, not a playable weapon or accepted finger-contact pose. Source forward is -X; owned preview rotates +90 yaw. Source arm weights and individual finger FK are preserved. Reload is unimplemented; authored clip slots are intentionally empty.";
                }
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(profile,path);
            }
            if (i == 0) BindPistol(profile);
            else CreatePreview(i,profile,source);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Prepared five shared-rig grip profiles; only the existing Five-seveN is playable.");
    }
    private static void BindPistol(FirstPersonWeaponGripConfiguration profile)
    {
        const string path = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<FirstPersonWeaponGripBinding>() != null) return;
            var presenter = root.GetComponent<PistolPresentation>();
            var source = root.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh == profile.BodyMesh).transform.parent;
            var binding = root.AddComponent<FirstPersonWeaponGripBinding>(); Configure(binding,profile,presenter.RecoilPivot,source.gameObject,false);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void CreatePreview(int i, FirstPersonWeaponGripConfiguration profile, GameObject source)
    {
        string path = PreviewPath + "/" + Ids[i] + "GripPreview.prefab";
        if (File.Exists(path))
        {
            var existing = PrefabUtility.LoadPrefabContents(path);
            try { if (existing.GetComponent<FirstPersonIdleMotion>() == null) { existing.AddComponent<FirstPersonIdleMotion>(); PrefabUtility.SaveAsPrefabAsset(existing,path); } }
            finally { PrefabUtility.UnloadPrefabContents(existing); } return;
        }
        var root = new GameObject(Ids[i] + "GripPreview_NOT_PLAYABLE");
        try
        {
            var pivot = new GameObject("PosePivot").transform; pivot.SetParent(root.transform,false); pivot.localPosition = new Vector3(.12f,-.105f,.42f);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source); visual.transform.SetParent(pivot,false);
            visual.transform.localPosition = profile.ModelPosition; visual.transform.localRotation = profile.ModelRotation;
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 2;
                if (child.name.IndexOf("Bullet",StringComparison.OrdinalIgnoreCase)>=0) child.gameObject.SetActive(false);
            }
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Material material = PreviewMaterial(i,source);
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
            { renderer.sharedMaterials = renderer.sharedMaterials.Select(_=>material).ToArray(); renderer.shadowCastingMode = ShadowCastingMode.Off; }
            root.AddComponent<FirstPersonArms>(); root.AddComponent<FirstPersonIdleMotion>(); var binding = root.AddComponent<FirstPersonWeaponGripBinding>(); Configure(binding,profile,pivot,visual,true);
            root.layer = pivot.gameObject.layer = 2; PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static void Configure(FirstPersonWeaponGripBinding binding, FirstPersonWeaponGripConfiguration profile, Transform pivot, GameObject model, bool preview)
    {
        var data = new SerializedObject(binding); data.FindProperty("configuration").objectReferenceValue = profile;
        data.FindProperty("posePivot").objectReferenceValue = pivot; data.FindProperty("modelGeometry").objectReferenceValue = model;
        data.FindProperty("previewOnly").boolValue = preview; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Material PreviewMaterial(int i, GameObject source)
    {
        string path = "Assets/Materials/FPS/WeaponPreviews/" + Ids[i] + ".mat"; var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolyOne/Free Gun/Materials/Props_FreeGun_M.mat");
        if(original == null || original.shader.name != "Universal Render Pipeline/Lit" || original.GetTexture("_BaseMap") == null)
            throw new System.InvalidOperationException("The imported weapon atlas material must be available before creating previews.");
        result = new Material(original);
        AssetDatabase.CreateAsset(result,path); return result;
    }
    [MenuItem("Tools/Shift Four/FPS/Restore Preview Materials From Imported Atlas")]
    public static void RestorePreviewAtlas()
    {
        var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/PolyOne/Free Gun/Materials/Props_FreeGun_M.mat");
        if(original==null||original.GetTexture("_BaseMap")==null)throw new System.InvalidOperationException("Imported weapon atlas missing.");
        foreach(string id in Ids.Skip(1))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FPS/WeaponPreviews/"+id+".mat");
            if(material==null)continue;
            material.shader=original.shader;material.CopyPropertiesFromMaterial(original);EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
    private static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return; int slash = path.LastIndexOf('/'); Ensure(path.Substring(0,slash)); AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
}
