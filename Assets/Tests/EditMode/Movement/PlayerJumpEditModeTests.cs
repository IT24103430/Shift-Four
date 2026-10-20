using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class PlayerJumpEditModeTests
    {
        private GameObject root;
        private Component player;
        private CharacterController capsule;
        private static Type PlayerType => Type.GetType("ShiftFour.PlayerController, Assembly-CSharp", true);

        [SetUp] public void Setup()
        {
            root = new GameObject("Jump configuration fixture");
            root.SetActive(false);
            capsule = root.AddComponent<CharacterController>();
            capsule.height = 1.8f; capsule.radius = .35f; capsule.center = Vector3.up * .9f;
            player = root.AddComponent(PlayerType);
        }

        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);

        [Test] public void Defaults_PreserveMovementCapsuleAndCamera()
        {
            var data = new SerializedObject(player);
            Assert.That(data.FindProperty("jumpHeight").floatValue, Is.EqualTo(1.1f));
            Assert.That(data.FindProperty("gravity").floatValue, Is.EqualTo(20f));
            Assert.That(data.FindProperty("walkSpeed").floatValue, Is.EqualTo(5f));
            Assert.That(data.FindProperty("eyeHeight").floatValue, Is.EqualTo(1.3f));
            Assert.That(data.FindProperty("nearClip").floatValue, Is.EqualTo(.05f));
            Assert.That(capsule.height, Is.EqualTo(1.8f));
            Assert.That(capsule.radius, Is.EqualTo(.35f));
            Assert.That(capsule.center, Is.EqualTo(Vector3.up * .9f));
            Assert.That(root.GetComponent<Rigidbody>(), Is.Null);
        }

        [TestCase(.7f, 12f)]
        [TestCase(1.1f, 20f)]
        public void ValidSettings_RemainConfigurable(float height, float acceleration)
        {
            Set("jumpHeight", height); Set("gravity", acceleration); Validate();
            var data = new SerializedObject(player);
            Assert.That(data.FindProperty("jumpHeight").floatValue, Is.EqualTo(height));
            Assert.That(data.FindProperty("gravity").floatValue, Is.EqualTo(acceleration));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteSettings_RestoreFiniteDefaults(float value)
        {
            Set("jumpHeight", value); Set("gravity", value); Validate();
            var data = new SerializedObject(player);
            Assert.That(data.FindProperty("jumpHeight").floatValue, Is.EqualTo(1.1f));
            Assert.That(data.FindProperty("gravity").floatValue, Is.EqualTo(20f));
        }

        [TestCase(0f)]
        [TestCase(-20f)]
        public void NonPositiveSettings_CannotInvertGravityOrProduceInvalidLaunch(float value)
        {
            Set("jumpHeight", value); Set("gravity", value); Validate();
            var data = new SerializedObject(player);
            Assert.That(data.FindProperty("jumpHeight").floatValue, Is.GreaterThanOrEqualTo(.1f));
            Assert.That(data.FindProperty("gravity").floatValue, Is.GreaterThanOrEqualTo(.1f));
        }

        [Test] public void ExistingLegacyJumpBinding_IncludesSpace()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset").First());
            var axes = settings.FindProperty("m_Axes");
            bool space = false;
            for (int i = 0; i < axes.arraySize; i++)
            {
                var axis = axes.GetArrayElementAtIndex(i);
                if (axis.FindPropertyRelative("m_Name").stringValue == "Jump" &&
                    axis.FindPropertyRelative("positiveButton").stringValue == "space") space = true;
            }
            Assert.That(space, Is.True, "Use the existing Jump input without changing InputManager.");
        }

        private void Set(string name, float value)
            => PlayerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, value);
        private void Validate()
            => PlayerType.GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
    }
}
