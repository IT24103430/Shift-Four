using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class PlayerCameraEditModeTests
    {
        [Test]
        public void FlyingHeight_RefitsHitboxAndClearanceWithoutRaisingNavigationRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/Scout.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var motor = root.GetComponent(Type.GetType("ShiftFour.DroneMotor, Assembly-CSharp", true));
                var visual = (Transform)new SerializedObject(motor).FindProperty("visual").objectReferenceValue;
                var body = root.GetComponent(Type.GetType("ShiftFour.DroneBody, Assembly-CSharp", true));
                float oldCenter = new SerializedObject(body).FindProperty("envelopeCenter").vector3Value.y;
                Vector3 scale = visual.localScale;
                visual.localPosition += Vector3.up * .5f;
                Type.GetType("ShiftFourProximityFix, Assembly-CSharp-Editor", true)
                    .GetMethod("ConfigureDrone").Invoke(null, new object[] { root });
                Assert.That(new SerializedObject(body).FindProperty("envelopeCenter").vector3Value.y, Is.EqualTo(oldCenter + .5f).Within(.001f));
                Assert.That(root.GetComponent<CapsuleCollider>().center.y, Is.EqualTo(oldCenter + .5f).Within(.001f));
                Assert.That(root.GetComponent<UnityEngine.AI.NavMeshAgent>().baseOffset, Is.Zero);
                Assert.That(root.transform.position.y, Is.Zero);
                Assert.That(visual.localScale, Is.EqualTo(scale));
                var brain = root.GetComponent(Type.GetType("ShiftFour.DroneBrain, Assembly-CSharp", true));
                Assert.That(new SerializedObject(brain).FindProperty("combatOriginHeight").floatValue, Is.EqualTo(2.3f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EyeHeight_IsCapsuleRelativeAndConfigureRestoresCameraPose()
        {
            var root = new GameObject("Eye fixture");
            try
            {
                root.SetActive(false);
                var cc = root.AddComponent<CharacterController>();
                cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f;
                var player = root.AddComponent(Type.GetType("ShiftFour.PlayerController, Assembly-CSharp", true));
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.localPosition = Vector3.one * 5f;
                player.GetType().GetMethod("Configure").Invoke(player, new object[] { camera });
                Assert.That(camera.transform.localPosition, Is.EqualTo(Vector3.up * 1.3f));
                Assert.That(camera.nearClipPlane, Is.EqualTo(.05f).Within(.0001f));
                cc.center += Vector3.up * .7f;
                player.GetType().GetMethod("ApplyCameraPose").Invoke(player, null);
                Assert.That(camera.transform.localPosition.y, Is.EqualTo(2f).Within(.0001f));
                var data = new SerializedObject(player);
                data.FindProperty("eyeHeight").floatValue = 99f;
                data.ApplyModifiedPropertiesWithoutUndo();
                player.GetType().GetMethod("ApplyCameraPose").Invoke(player, null);
                Assert.That(camera.transform.localPosition.y, Is.LessThanOrEqualTo(cc.center.y + cc.height * .5f - cc.radius));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
