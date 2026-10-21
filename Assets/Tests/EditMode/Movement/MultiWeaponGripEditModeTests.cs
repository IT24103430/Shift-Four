using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace ShiftFour.Tests
{
    public sealed class MultiWeaponGripEditModeTests
    {
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        [TestCase("FNFiveSeven")] [TestCase("HKMP5")] [TestCase("M16A1")] [TestCase("AK47")] [TestCase("BarrettM82A1")]
        public void Profiles_ReferenceActualImportedBodyAndContainNoRuntimeBoneReferences(string id)
        {
            var data=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/FPS/WeaponGrips/"+id+".asset");Assert.That(data,Is.Not.Null);
            Assert.That(Get(data,"IsPrepared"),Is.EqualTo(true));var source=(GameObject)Get(data,"SourceModel");var mesh=(Mesh)Get(data,"BodyMesh");
            Assert.That(source.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh==mesh),Is.True);
            Assert.That(AssetDatabase.GetAssetPath(mesh),Does.StartWith("Assets/PolyOne/Free Gun/Model/"));
            foreach(var f in data.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic))Assert.That(f.FieldType!=typeof(Transform),Is.True);
            Assert.That(Get(data,"WeaponId"),Is.EqualTo(id));
            if(id!="FNFiveSeven") { Assert.That(Get(data,"ReloadPresentation").ToString(),Is.EqualTo("NonePrepared"));Assert.That(Get(data,"ReloadClip"),Is.Null); }
        }
        [TestCase("HKMP5")] [TestCase("M16A1")] [TestCase("AK47")] [TestCase("BarrettM82A1")]
        public void PreparedWrappers_AreExplicitPreviewsWithoutGameplayOrASecondAnimationSystem(string id)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/GripPreviews/"+id+"GripPreview.prefab");Assert.That(root,Is.Not.Null);
            var binding=root.GetComponent(T("FirstPersonWeaponGripBinding"));Assert.That(Get(binding,"PreviewOnly"),Is.EqualTo(true));
            Assert.That(root.GetComponent(T("WeaponController")),Is.Null);Assert.That(root.GetComponent(T("FirstPersonViewmodel")),Is.Null);
            Assert.That(root.GetComponent(T("PistolPresentation")),Is.Null);Assert.That(root.GetComponent(T("FirstPersonArms")),Is.Not.Null);
            Assert.That(root.GetComponent(T("FirstPersonIdleMotion")),Is.Not.Null);
            var pivot=(Transform)Get(binding,"PosePivot");var geometry=(GameObject)Get(binding,"ModelGeometry");Assert.That(geometry.transform.IsChildOf(pivot),Is.True);
            Assert.That(geometry.transform.localScale,Is.EqualTo(Vector3.one));
            foreach(var renderer in geometry.GetComponentsInChildren<MeshRenderer>(true))foreach(var material in renderer.sharedMaterials)
            { Assert.That(material,Is.Not.Null);Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));var imported=AssetDatabase.LoadAssetAtPath<Material>("Assets/PolyOne/Free Gun/Materials/Props_FreeGun_M.mat");Assert.That(material.GetTexture("_BaseMap"),Is.SameAs(imported.GetTexture("_BaseMap")));Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null); }
        }
    }
}
