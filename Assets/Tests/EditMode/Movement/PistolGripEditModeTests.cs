using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class PistolGripEditModeTests
    {
        private static Type GripType => Type.GetType("ShiftFour.FirstPersonPistolGrip, Assembly-CSharp", true);
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Weapons/FirstPersonArmsRig.prefab");
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        [Test] public void OwnedPrefab_HasConfigurableWristTargetsAndElbowHints()
        {
            var root = Prefab; var grip = root.GetComponent(GripType);
            Assert.That(grip, Is.Not.Null);
            foreach (string name in new[] { "RightHandTarget", "SupportHandTarget", "RightElbowHint", "SupportElbowHint" })
            {
                var target = (Transform)Get(grip, name);
                Assert.That(target, Is.Not.Null); Assert.That(target.parent, Is.SameAs(root.transform));
                Assert.That(target.gameObject.layer, Is.EqualTo(2));
            }
            var bones = root.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            foreach (var pair in new[] { ("RightHandTarget", "hand.R"), ("SupportHandTarget", "hand.L") })
            {
                var target = (Transform)Get(grip, pair.Item1); var wrist = bones.Single(b => b.name == pair.Item2);
                Assert.That(Vector3.Distance(target.position, wrist.position), Is.LessThan(.00002f));
                Assert.That(Quaternion.Angle(target.rotation, wrist.rotation), Is.LessThan(.05f));
            }
            Assert.That((float)Get(grip, "MaximumWristDeviation"), Is.InRange(15, 40));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        [Test] public void AuthoredPose_PreservesEveryBoneOffsetScaleAndFingerChain()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/FPS/SF_TacticalArms.fbx");
            var original = source.GetComponentInChildren<SkinnedMeshRenderer>().bones.ToDictionary(b => b.name);
            var skin = Prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (Transform bone in skin.bones)
            {
                Assert.That(Vector3.Distance(bone.localPosition, original[bone.name].localPosition), Is.LessThan(.000001f), bone.name);
                Assert.That(bone.localScale, Is.EqualTo(original[bone.name].localScale));
            }
            Assert.That(skin.sharedMesh, Is.SameAs(source.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh));
            foreach (string side in new[] { "L", "R" })
                foreach (string finger in new[] { "f_index", "f_middle", "f_ring", "f_pinky", "thumb" })
                {
                    var first = skin.bones.Single(b => b.name == finger + ".01." + side);
                    var second = skin.bones.Single(b => b.name == finger + ".02." + side);
                    var third = skin.bones.Single(b => b.name == finger + ".03." + side);
                    Assert.That(second.parent, Is.SameAs(first)); Assert.That(third.parent, Is.SameAs(second));
                    Assert.That(Vector3.Distance(first.position, second.position), Is.EqualTo(Vector3.Distance(original[first.name].position, original[second.name].position)).Within(.00001f));
                    Assert.That(Vector3.Distance(second.position, third.position), Is.EqualTo(Vector3.Distance(original[second.name].position, original[third.name].position)).Within(.00001f));
                }
        }

        [Serializable] private sealed class Profile { public Pose[] bones; }
        [Serializable] private sealed class Pose { public string name; public Quaternion rotation; }

        [Test] public void Profile_DoesNotLeaveStaleRotationsOnUnspecifiedFingerBones()
        {
            var profile = JsonUtility.FromJson<Profile>(System.IO.File.ReadAllText("ArtSource/TacticalArms/FNFiveSevenGripPose.json"));
            var poses = profile.bones.ToDictionary(p => p.name, p => p.rotation);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/FPS/SF_TacticalArms.fbx")
                .GetComponentInChildren<SkinnedMeshRenderer>().bones.ToDictionary(b => b.name);
            foreach (Transform bone in Prefab.GetComponentInChildren<SkinnedMeshRenderer>().bones)
            {
                Quaternion expected = poses.TryGetValue(bone.name, out var rotation) ? rotation : original[bone.name].localRotation;
                Assert.That(Quaternion.Angle(bone.localRotation, expected), Is.LessThan(.01f), bone.name + " must match the complete authored profile, including source defaults.");
            }
        }

        [Test] public void AuthoredSkin_DoesNotStretchSmallEdgesIntoReinforcementSpikes()
        {
            var root = Object.Instantiate(Prefab); var baked = new Mesh();
            try
            {
                var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>(); renderer.BakeMesh(baked, true);
                Vector3[] source = renderer.sharedMesh.vertices, posed = baked.vertices;
                for (int sub = 0; sub < renderer.sharedMesh.subMeshCount; sub++)
                {
                    int[] triangles = renderer.sharedMesh.GetTriangles(sub); float worst = 0;
                    for (int i = 0; i < triangles.Length; i += 3)
                        for (int edge = 0; edge < 3; edge++)
                        {
                            int a = triangles[i + edge], b = triangles[i + (edge + 1) % 3];
                            float before = Vector3.Distance(source[a], source[b]), after = Vector3.Distance(posed[a], posed[b]);
                            worst = Mathf.Max(worst, after - before);
                        }
                    Assert.That(worst, Is.LessThan(.006f), renderer.sharedMaterials[sub].name + " must not develop long skinning spikes.");
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(root); }
        }

        [Test] public void RuntimeInitialization_DoesNotMovePistolOrCreateExtraGripTargets()
        {
            var root = Object.Instantiate(Prefab);
            try
            {
                var grip = root.GetComponent(GripType); var method = GripType.GetMethod("Initialize");
                var targets = root.GetComponentsInChildren<Transform>().Where(t => t.parent == root.transform).ToArray();
                Assert.That(method.Invoke(grip, null), Is.EqualTo(true));
                Assert.That(method.Invoke(grip, null), Is.EqualTo(true));
                CollectionAssert.AreEqual(targets, root.GetComponentsInChildren<Transform>().Where(t => t.parent == root.transform).ToArray());
                Assert.That(root.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
