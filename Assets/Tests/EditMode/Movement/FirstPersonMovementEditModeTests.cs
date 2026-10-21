using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonMovementEditModeTests
    {
        private static Type T => Type.GetType("ShiftFour.FirstPersonMovementMotion, Assembly-CSharp", true);
        private static object Get(object o, string n) => T.GetProperty(n).GetValue(o);
        private static void Advance(object o, double dt, Vector3 velocity, bool grounded, bool suppressed = false)
            => T.GetMethod("Advance").Invoke(o, new object[] { dt, velocity, grounded, suppressed });
        private static Vector3 Position(object o) => (Vector3)Get(o, "PositionOffset");
        private static Quaternion Rotation(object o) => (Quaternion)Get(o, "RotationOffset");
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;

        [TestCase(0, 5)] [TestCase(0, -5)] [TestCase(5, 0)] [TestCase(-5, 0)]
        public void ActualTravel_ProducesSmallDirectionalWalkingMotion(int x, int z)
        {
            var root = new GameObject("Movement sample"); var motion = root.AddComponent(T);
            try
            {
                for (int i = 0; i < 60; i++) Advance(motion, 1d / 60, new Vector3(x, 0, z), true);
                Assert.That((float)Get(motion, "WalkingWeight"), Is.GreaterThan(.99f));
                Assert.That(Position(motion).magnitude, Is.InRange(.0001f, .006f));
                Assert.That(Degrees(Rotation(motion)), Is.LessThan(1));
                if (x != 0) Assert.That(Position(motion).x * Math.Sign(x), Is.LessThan(0), "Strafe inertia opposes actual travel.");
                if (z != 0) Assert.That(Math.Sign(Rotation(motion).x), Is.EqualTo(Math.Sign(z)));
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(0)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void Stopping_FreezesStepPhaseAndEasesToNeutralWithoutStationaryBobbing()
        {
            var root = new GameObject("Movement stopping"); var motion = root.AddComponent(T);
            try
            {
                for (int i = 0; i < 77; i++) Advance(motion, 1d / 60, Vector3.forward * 5, true);
                double phase = (double)Get(motion, "Phase"); float previous = Position(motion).magnitude;
                for (int i = 0; i < 180; i++)
                {
                    Advance(motion, 1d / 60, new Vector3(0, -1, 0), true);
                    Assert.That(Get(motion, "Phase"), Is.EqualTo(phase));
                    Assert.That(Position(motion).magnitude, Is.LessThanOrEqualTo(previous + .0000001f));
                    previous = Position(motion).magnitude;
                }
                Assert.That(Get(motion, "WalkingWeight"), Is.EqualTo(0f));
                Assert.That(Position(motion), Is.EqualTo(Vector3.zero));
                Assert.That(Degrees(Rotation(motion)), Is.LessThan(.0001));
                Assert.That(Get(motion, "IdleWeight"), Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void JumpHoldAndLanding_AreBoundedAndRecoverWithoutRepeatedEvents()
        {
            var root = new GameObject("Movement jump"); var motion = root.AddComponent(T);
            try
            {
                Advance(motion, 1d / 60, Vector3.zero, true);
                Advance(motion, 1d / 60, Vector3.up * 6, false);
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(1));
                Assert.That(Position(motion).y, Is.LessThan(0));
                for (int i = 0; i < 180; i++) Advance(motion, 1d / 60, new Vector3(5, -5, 0), false);
                var hold = Position(motion); var rotation = Rotation(motion);
                for (int i = 0; i < 60; i++) Advance(motion, 1d / 60, new Vector3(-5, -5, 0), false);
                Assert.That(Vector3.Distance(hold, Position(motion)), Is.LessThan(.000001f));
                Assert.That(Degrees(Quaternion.Inverse(rotation) * Rotation(motion)), Is.LessThan(.001));
                Assert.That(Get(motion, "WalkingWeight"), Is.EqualTo(0f));
                float minimum = 0;
                for (int i = 0; i < 150; i++)
                {
                    Advance(motion, 1d / 60, Vector3.zero, true);
                    minimum = Mathf.Min(minimum, Position(motion).y);
                    Assert.That(Position(motion).magnitude, Is.LessThan(.009f));
                    Assert.That(Degrees(Rotation(motion)), Is.LessThan(1.5));
                }
                Assert.That(minimum, Is.LessThan(-.003f));
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(1)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(1));
                Assert.That(Position(motion).magnitude, Is.LessThan(.000001f));
                Assert.That(Degrees(Rotation(motion)), Is.LessThan(.001));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void StepOffOrBriefGroundChatter_DoesNotInventAJumpOrLargeLanding()
        {
            var root = new GameObject("Ground transition"); var motion = root.AddComponent(T);
            try
            {
                Advance(motion, .1, Vector3.zero, true);
                Advance(motion, 1d / 120, Vector3.down, false);
                Advance(motion, 1d / 120, Vector3.zero, true);
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(0)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(0));
                for (int i = 0; i < 20; i++) Advance(motion, 1d / 60, Vector3.down * 3, false);
                Advance(motion, 1d / 60, Vector3.zero, true);
                Assert.That(Get(motion, "TakeoffCount"), Is.EqualTo(0)); Assert.That(Get(motion, "LandingCount"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void ThirtySixtyAndOneTwentyFps_MatchWalkingJumpLandingAndActivitySamples()
        {
            List<Vector3> expected = null; List<Quaternion> expectedRotations = null;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var root = new GameObject("Movement rates"); var motion = root.AddComponent(T);
                try
                {
                    var positions = new List<Vector3>(); var rotations = new List<Quaternion>(); Vector3 previous = Vector3.zero;
                    for (int frame = 1; frame <= fps * 7; frame++)
                    {
                        int stage = (frame - 1) / fps;
                        Vector3 velocity = stage == 1 ? Vector3.forward * 5 : stage == 2 ? Vector3.right * 5 :
                            stage == 3 ? new Vector3(0, 4, 5) : stage == 4 ? new Vector3(0, -5, 5) : Vector3.zero;
                        Advance(motion, 1d / fps, velocity, stage != 3 && stage != 4, stage == 2 || stage == 4);
                        Assert.That(Vector3.Distance(previous, Position(motion)), Is.LessThan(.005f)); previous = Position(motion);
                        if (frame % (fps / 2) == 0) { positions.Add(Position(motion)); rotations.Add(Rotation(motion)); }
                    }
                    if (expected != null)
                        for (int i = 0; i < positions.Count; i++)
                        {
                            Assert.That(Vector3.Distance(positions[i], expected[i]), Is.LessThan(.000001f));
                            Assert.That(Degrees(Quaternion.Inverse(rotations[i]) * expectedRotations[i]), Is.LessThan(.001));
                        }
                    expected = positions; expectedRotations = rotations;
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test] public void PausedInvalidAndResetSamples_DoNotCorruptStateOrWriteTransforms()
        {
            var root = new GameObject("Movement ownership"); root.transform.position = new Vector3(1, 2, 3);
            root.transform.rotation = Quaternion.Euler(12, 34, 56); var motion = root.AddComponent(T);
            try
            {
                var start = root.transform.position; var rotation = root.transform.rotation;
                Advance(motion, .5, Vector3.forward * 5, true); var p = Position(motion); var q = Rotation(motion);
                foreach (double dt in new[] { 0d, -1d, double.NaN, double.PositiveInfinity }) Advance(motion, dt, Vector3.up, false);
                Advance(motion, .1, new Vector3(float.NaN, 0, 0), false);
                Assert.That(Position(motion), Is.EqualTo(p)); Assert.That(Rotation(motion), Is.EqualTo(q));
                Assert.That(root.transform.position, Is.EqualTo(start)); Assert.That(root.transform.rotation, Is.EqualTo(rotation));
                T.GetMethod("ResetMotion").Invoke(motion, null);
                Assert.That(Position(motion), Is.EqualTo(Vector3.zero)); Assert.That(Rotation(motion), Is.EqualTo(Quaternion.identity));
                Assert.That(Get(motion, "Phase"), Is.EqualTo(0d));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void PistolPrefab_PersistsMovementSettingsWithoutRootMotion()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab");
            var motion = root.GetComponent(T); Assert.That(motion, Is.Not.Null);
            var settings = new SerializedObject(motion);
            Assert.That(settings.FindProperty("walkPositionAmplitude").vector3Value.magnitude, Is.LessThan(.003f));
            Assert.That(settings.FindProperty("airbornePosition").vector3Value.magnitude, Is.LessThan(.005f));
            Assert.That(settings.FindProperty("landingDip").floatValue, Is.InRange(.001f, .006f));
            Assert.That(settings.FindProperty("blendSeconds").floatValue, Is.InRange(.05f, .3f));
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true)) Assert.That(animator.applyRootMotion, Is.False);
        }
    }
}
