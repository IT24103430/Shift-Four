using System;
using System.IO;
using System.Linq;
using ShiftFour;
using UnityEditor;
using UnityEngine;

// Explicit pose authoring for the owned prefab. Source meshes, pistol, materials and scenes stay intact.
public static class FirstPersonPistolGripAuthoring
{
    private const string Prefab = "Assets/Resources/Weapons/FirstPersonArmsRig.prefab";
    private const string Profile = "ArtSource/TacticalArms/FNFiveSevenGripPose.json";

    [MenuItem("Tools/Shift Four/FPS/Apply Authored Two Hand Pistol Grip")]
    public static void Apply()
    {
        var profile = JsonUtility.FromJson<GripProfile>(File.ReadAllText(Profile));
        if (profile?.bones == null || profile.targets == null || profile.targets.Length != 2)
            throw new InvalidDataException("The authored grip must contain both wrist targets and bone rotations.");
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var bindings = root.GetComponent<FirstPersonArmsRig>();
            if (bindings == null || !bindings.IsValid) throw new InvalidOperationException("The owned rig has invalid bindings.");
            var bones = bindings.Skin.bones.ToDictionary(b => b.name);
            // An omitted rotation means the immutable source rest pose, not a previous profile's override.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(TacticalArmsAssetBuilder.ModelPath)
                .GetComponentInChildren<SkinnedMeshRenderer>().bones.ToDictionary(b => b.name);
            foreach (var bone in bones) bone.Value.localRotation = source[bone.Key].localRotation;
            foreach (var pose in profile.bones)
            {
                if (!(pose.name.StartsWith("f_") || pose.name.StartsWith("thumb.") || pose.name.StartsWith("palm_") || pose.name.StartsWith("hand.") ||
                    pose.name.StartsWith("forearm.") || pose.name.StartsWith("upper_arm.")) || !bones.ContainsKey(pose.name))
                    throw new InvalidDataException("Only existing arm/hand/finger rotations may be authored: " + pose.name);
                if (Quaternion.Dot(pose.rotation, pose.rotation) < .99f || Quaternion.Dot(pose.rotation, pose.rotation) > 1.01f)
                    throw new InvalidDataException("The bone rotation is not a unit quaternion: " + pose.name);
                bones[pose.name].localRotation = pose.rotation.normalized;
            }
            var grip = root.GetComponent<FirstPersonPistolGrip>();
            if (grip == null) grip = root.AddComponent<FirstPersonPistolGrip>();
            var data = new SerializedObject(grip);
            foreach (var target in profile.targets)
            {
                bool right = target.side == "R";
                if (!right && target.side != "L") throw new InvalidDataException("Unknown grip side.");
                Transform wrist = Target(root.transform, right ? "RightHandGrip" : "SupportHandGrip");
                wrist.localPosition = target.position; wrist.localRotation = target.rotation.normalized;
                Transform hint = Target(root.transform, right ? "RightElbowHint" : "SupportElbowHint");
                hint.localPosition = target.elbowHint;
                data.FindProperty(right ? "rightHandTarget" : "supportHandTarget").objectReferenceValue = wrist;
                data.FindProperty(right ? "rightElbowHint" : "supportElbowHint").objectReferenceValue = hint;
            }
            data.FindProperty("maximumWristDeviation").floatValue = 35;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            Debug.Log("Authored the owned pistol grip: rotations, wrist targets and elbow hints only.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Transform Target(Transform parent, string name)
    {
        var target = parent.Find(name);
        if (target == null) { target = new GameObject(name).transform; target.SetParent(parent, false); }
        target.gameObject.layer = 2;
        return target;
    }

    [Serializable] private sealed class GripProfile { public BonePose[] bones; public GripTarget[] targets; }
    [Serializable] private sealed class BonePose { public string name; public Quaternion rotation; }
    [Serializable] private sealed class GripTarget { public string side; public Vector3 position; public Quaternion rotation; public Vector3 elbowHint; }
}
