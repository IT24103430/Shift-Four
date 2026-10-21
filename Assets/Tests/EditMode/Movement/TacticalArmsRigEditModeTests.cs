using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class TacticalArmsRigEditModeTests
    {
        private const string Model = "Assets/Models/FPS/SF_TacticalArms.fbx";
        private const string Owned = "Assets/Resources/Weapons/FirstPersonArmsRig.prefab";
        private GameObject prefab;
        private SkinnedMeshRenderer skin;
        private Transform Bone(string name) => prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

        [SetUp] public void Setup()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Owned);
            Assert.That(prefab, Is.Not.Null);
            skin = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
        }

        [Test] public void Import_HasMetreScaleGenericBonesAndNoForeignSceneContent()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.bakeAxisConversion, Is.True);
            Assert.That(importer.addCollider || importer.importCameras || importer.importLights || importer.importAnimation, Is.False);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true), Is.Empty, "Only the derived skin belongs in the arms prefab.");
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/Resources/Weapons/FirstPersonArms.fbx"),
                Is.EqualTo("08b28dd902125f745a63616a6ed4ba7b"), "The legacy FBX/GUID must survive the upgrade.");
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(Vector3.Distance(node.localScale, Vector3.one), Is.LessThan(.00002f), node.name);
                Assert.That(node.name.Contains("REFERENCE_ONLY") || node.name.Contains(".control"), Is.False, node.name);
            }
        }

        [Test] public void Hierarchy_HasExplicitBindingsAndThreeSegmentsForEveryFinger()
        {
            Type type = Type.GetType("ShiftFour.FirstPersonArmsRig, Assembly-CSharp", true);
            Component bindings = prefab.GetComponent(type);
            Assert.That(bindings, Is.Not.Null);
            Assert.That(type.GetProperty("IsValid").GetValue(bindings), Is.True);
            Assert.That(skin.bones.Length, Is.EqualTo(48));
            Assert.That(skin.bones.All(b => b != null && b.IsChildOf(prefab.transform)), Is.True);
            foreach (string side in new[] { ".L", ".R" })
            {
                Assert.That(Bone("hand" + side).parent, Is.SameAs(Bone("forearm" + side)));
                Assert.That(Bone("forearm" + side).parent, Is.SameAs(Bone("upper_arm" + side)));
                foreach (string finger in new[] { "index", "middle", "ring", "pinky" })
                {
                    string prefix = "f_" + finger;
                    Assert.That(Bone(prefix + ".01" + side).parent, Is.SameAs(Bone("palm_" + finger + side)));
                    Assert.That(Bone(prefix + ".02" + side).parent, Is.SameAs(Bone(prefix + ".01" + side)));
                    Assert.That(Bone(prefix + ".03" + side).parent, Is.SameAs(Bone(prefix + ".02" + side)));
                }
                Assert.That(Bone("thumb.01" + side).parent, Is.SameAs(Bone("hand" + side)));
                Assert.That(Bone("thumb.02" + side).parent, Is.SameAs(Bone("thumb.01" + side)));
                Assert.That(Bone("thumb.03" + side).parent, Is.SameAs(Bone("thumb.02" + side)));
            }
        }

        [Test] public void Skin_EveryVertexHasNormalizedFiniteWeightsAndValidBindIndices()
        {
            Mesh mesh = skin.sharedMesh;
            Assert.That(mesh.vertexCount, Is.InRange(10000, 50000));
            Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            foreach (BoneWeight weight in mesh.boneWeights)
            {
                float[] values = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
                int[] indices = { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
                Assert.That(values.All(w => !float.IsNaN(w) && !float.IsInfinity(w) && w >= 0), Is.True);
                Assert.That(values.Sum(), Is.EqualTo(1).Within(.0001f));
                for (int i = 0; i < 4; i++)
                    if (values[i] > 0) Assert.That(indices[i], Is.InRange(0, skin.bones.Length - 1));
            }
            Assert.That(mesh.vertices.All(Finite), Is.True);
            Assert.That(mesh.normals.All(Finite), Is.True);
        }

        [Test] public void Proportions_PreserveUniformlyScaledSourceArmLengths()
        {
            foreach (string side in new[] { ".L", ".R" })
            {
                Assert.That(Vector3.Distance(Bone("forearm" + side).position, Bone("hand" + side).position), Is.EqualTo(.227094f).Within(.0002f));
                Assert.That(Vector3.Distance(Bone("upper_arm" + side).position, Bone("forearm" + side).position), Is.EqualTo(.220311f).Within(.0002f));
                Assert.That(Vector3.Distance(Bone("f_index.01" + side).position, Bone("f_index.02" + side).position), Is.EqualTo(.025399f).Within(.0002f));
            }
            // The imported anatomical rest fit stays immutable; the owned prefab may author a new grip pose.
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var sourceBones = original.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            Assert.That(Vector3.Distance(original.transform.InverseTransformPoint(sourceBones.Single(b => b.name == "hand.R").position), new Vector3(.030f, -.022f, -.126f)), Is.LessThan(.0002f));
            Assert.That(Vector3.Distance(original.transform.InverseTransformPoint(sourceBones.Single(b => b.name == "hand.L").position), new Vector3(-.040f, -.041f, -.118f)), Is.LessThan(.0002f));
        }

        [Test] public void Surfaces_AreOwnedSupportedAndHaveValidBaseAndNormalMaps()
        {
            Assert.That(skin.sharedMaterials.Length, Is.EqualTo(5));
            foreach (Material material in skin.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Materials/FPS/Arms/"));
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
                var normal = material.GetTexture("_BumpMap");
                Assert.That(normal, Is.Not.Null);
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal))).textureType, Is.EqualTo(TextureImporterType.NormalMap));
            }
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 2), Is.True);
        }

        [Test] public void ImportedBindPose_ReproducesSourceSurfaceWithoutStretchOrOffset()
        {
            // Validate the FBX bind pose separately from owned prefab pose overrides.
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            var baked = new Mesh();
            try
            {
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.BakeMesh(baked, true);
                Vector3[] expected = renderer.sharedMesh.vertices, actual = baked.vertices;
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                float worst = 0;
                for (int i = 0; i < actual.Length; i++) worst = Mathf.Max(worst, Vector3.Distance(actual[i], expected[i]));
                Assert.That(worst, Is.LessThan(.0001f), "A bad bind matrix must not deform wrists/fingers at rest.");
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(instance); }
        }

        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}
