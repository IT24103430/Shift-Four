using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonReloadEditModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, a);
        private static Vector3 Offset(object o) => (Vector3)Get(o, "MagazineOffset");

        [Test] public void AuthoredStages_GraspBeforeWithdrawal_SeatBeforeReturningToGrip_AndEndNeutral()
        {
            var root = new GameObject("Reload curves"); var motion = root.AddComponent(T("FirstPersonReloadMotion"));
            try
            {
                Call(motion, "Evaluate", 0f); Assert.That(Offset(motion), Is.EqualTo(Vector3.zero)); Assert.That(Get(motion, "ContactWeight"), Is.EqualTo(0f));
                Call(motion, "Evaluate", .26f); Assert.That(Get(motion, "ContactWeight"), Is.EqualTo(1f)); Assert.That(Offset(motion), Is.EqualTo(Vector3.zero));
                Call(motion, "Evaluate", .44f); Assert.That(Offset(motion).y, Is.LessThan(-.15f)); Assert.That(Get(motion, "ContactWeight"), Is.EqualTo(1f));
                Call(motion, "Evaluate", .56f); Assert.That(Offset(motion).y, Is.LessThan(-.19f));
                Call(motion, "Evaluate", .88f); Assert.That(Offset(motion), Is.EqualTo(Vector3.zero));
                Call(motion, "Evaluate", 1f); Assert.That(Offset(motion), Is.EqualTo(Vector3.zero));
                Assert.That(Get(motion, "PositionOffset"), Is.EqualTo(Vector3.zero)); Assert.That(Get(motion, "RotationOffset"), Is.EqualTo(Quaternion.identity));
                Assert.That(Get(motion, "MagazineRotation"), Is.EqualTo(Quaternion.identity)); Assert.That(Get(motion, "ContactWeight"), Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void Curves_AreContinuousBoundedAndDoNotAccumulateTransformOffsets()
        {
            var root = new GameObject("Reload continuity"); root.transform.position = new Vector3(2, 3, 4); var motion = root.AddComponent(T("FirstPersonReloadMotion"));
            try
            {
                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= 480; i++)
                {
                    Call(motion, "Evaluate", i / 480f); var p = Offset(motion);
                    Assert.That(Vector3.Distance(p, previous), Is.LessThan(.004f)); previous = p;
                    Assert.That(p.magnitude, Is.LessThan(.23f)); Assert.That((float)Get(motion, "ContactWeight"), Is.InRange(0, 1));
                }
                Call(motion, "Evaluate", .37f); var expected = Offset(motion);
                Call(motion, "Evaluate", .8f); Call(motion, "Evaluate", .1f); Call(motion, "Evaluate", .37f);
                Assert.That(Offset(motion), Is.EqualTo(expected)); Assert.That(root.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void InvalidAndOutOfRangeProgress_ReturnNeutralEndpoints()
        {
            var root = new GameObject("Reload range"); var motion = root.AddComponent(T("FirstPersonReloadMotion"));
            try
            {
                foreach (float p in new[] { float.NaN, float.PositiveInfinity, -1f, 2f })
                {
                    Call(motion, "Evaluate", p); Assert.That(Offset(motion), Is.EqualTo(Vector3.zero));
                    Assert.That(Get(motion, "ContactWeight"), Is.EqualTo(0f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void PistolPrefab_HasOneSourceMagazineAndAnOwnedGripTargetWithoutPhysics()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab");
            var motion = root.GetComponent(T("FirstPersonReloadMotion")); Assert.That(motion, Is.Not.Null);
            var magazine = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "FNFiveSeven_Magazine");
            var target = (Transform)Get(motion, "MagazineGripTarget"); Assert.That(target.parent, Is.SameAs(magazine));
            Assert.That(target.gameObject.layer, Is.EqualTo(2)); Assert.That(target.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(AssetDatabase.GetAssetPath(magazine.GetComponent<MeshFilter>().sharedMesh), Is.EqualTo("Assets/PolyOne/Free Gun/Model/FN_Five_Seven.fbx"));
            var presenter = new SerializedObject(root.GetComponent(T("PistolPresentation")));
            Assert.That(presenter.FindProperty("magazinePart").objectReferenceValue, Is.SameAs(magazine));
            var data = new SerializedObject(motion);
            foreach (string name in new[] { "access", "handContact", "withdrawal", "exchange", "seat" })
                Assert.That(data.FindProperty(name).animationCurveValue.keys.Length, Is.GreaterThanOrEqualTo(4));
        }

        [Test] public void SamplingTheEntireAnimation_DoesNotCompleteReloadOrTransferAnyAmmunition()
        {
            var definition = ScriptableObject.CreateInstance(T("WeaponDefinition"));
            var state = Activator.CreateInstance(T("WeaponRuntimeState"), new object[] { definition });
            var root = new GameObject("Reload has no ammo authority"); var motion = root.AddComponent(T("FirstPersonReloadMotion"));
            try
            {
                Assert.That(Call(state, "TryAcceptShot", 0f, .24f), Is.EqualTo(true)); Assert.That(Call(state, "BeginReload", 0f), Is.EqualTo(true));
                object ammo = Get(state, "RoundsInMagazine"), reserve = Get(state, "ReserveRounds");
                for (int i = 0; i <= 1000; i++) Call(motion, "Evaluate", i / 1000f);
                Assert.That(Get(state, "ReloadState").ToString(), Is.EqualTo("Reloading"));
                Assert.That(Get(state, "RoundsInMagazine"), Is.EqualTo(ammo)); Assert.That(Get(state, "ReserveRounds"), Is.EqualTo(reserve));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }
    }
}
