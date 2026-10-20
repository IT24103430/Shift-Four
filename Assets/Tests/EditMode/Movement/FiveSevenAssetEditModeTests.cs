using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class FiveSevenAssetEditModeTests
    {
        private const string Wrapper = "Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab";
        private const string Source = "Assets/PolyOne/Free Gun/Prefabs/FN_Five_Seven.prefab";
        private const string Model = "Assets/PolyOne/Free Gun/Model/FN_Five_Seven.fbx";
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private GameObject prefab;
        [SetUp] public void Setup() { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Wrapper); Assert.That(prefab, Is.Not.Null); }

        [Test] public void Definition_PreservesTuningAndReferencesOwnedViewmodel()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Weapons/FNFiveSeven.asset");
            Assert.That(definition, Is.Not.Null);
            Assert.That(Get(definition, "DisplayName"), Is.EqualTo("FN Five-seveN"));
            Assert.That(Get(definition, "Damage"), Is.EqualTo(25f)); Assert.That(Get(definition, "Range"), Is.EqualTo(30f));
            Assert.That(Get(definition, "FireInterval"), Is.EqualTo(.24f));
            Assert.That(Get(definition, "ViewmodelPrefab"), Is.SameAs(prefab.GetComponent(T("FirstPersonViewmodel"))));
        }

        [Test] public void NestedVendorPrefab_RestoresFbxAssemblyAndHidesRound()
        {
            Transform visual = prefab.transform.Find("RecoilPivot/FN_Five_Seven");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(visual.gameObject), Is.EqualTo(Source));
            foreach (Transform part in model.transform)
            {
                Transform actual = visual.Find(part.name);
                Assert.That(Vector3.Distance(actual.localPosition, part.localPosition), Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(actual.localRotation, part.localRotation), Is.LessThan(.001f));
                Assert.That(actual.localScale, Is.EqualTo(part.localScale));
                Assert.That(actual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(part.GetComponent<MeshFilter>().sharedMesh));
            }
            Assert.That(visual.Find("FNFiveSeven__Bullet").gameObject.activeSelf, Is.False);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [Test] public void ImportedMaterialsAndAtlas_ArePreservedAndSupported()
        {
            var imported = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolyOne/Free Gun/Materials/Props_FreeGun_M.mat");
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("FNFiveSeven_")))
            {
                Material material = filter.GetComponent<Renderer>().sharedMaterial;
                Assert.That(material, Is.SameAs(imported));
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
            }
        }

        [Test] public void PresentationHasNoPhysicsAndEveryObjectIgnoresRaycasts()
        {
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true)) Assert.That(node.gameObject.layer, Is.EqualTo(2), node.name);
        }

        [Test] public void MuzzleIsAtFrontOfBodyAndFlashIsRestrainedAndInitiallyOff()
        {
            var presentation = prefab.GetComponent(T("PistolPresentation"));
            var muzzle = (Transform)Get(presentation, "MuzzlePoint");
            Bounds body = muzzle.parent.Find("FNFiveSeven_Body").GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(muzzle.localPosition.z, Is.GreaterThan(body.max.z));
            Assert.That(muzzle.localPosition.z - body.max.z, Is.LessThan(.004f));
            Assert.That(muzzle.localPosition.y, Is.EqualTo(.0566f).Within(.001f));
            Assert.That(Vector3.Dot(muzzle.forward, prefab.transform.forward), Is.GreaterThan(.999f));
            var flash = (Renderer)Get(presentation, "MuzzleFlash");
            Assert.That(flash.enabled, Is.False); Assert.That(flash.transform.localScale.x, Is.LessThan(.05f));
            Assert.That(flash.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(flash.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
        }

        [Test] public void UnitScalePose_KeepsEveryWeaponBoundsCornerBeyondCameraNearPlane()
        {
            Assert.That(prefab.transform.Find("RecoilPivot").localScale, Is.EqualTo(Vector3.one));
            foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds b = mesh.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Assert.That(prefab.transform.InverseTransformPoint(mesh.transform.TransformPoint(p)).z, Is.GreaterThan(.1f));
                }
            }
        }
    }
}
