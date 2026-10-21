using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonIdleEditModeTests
    {
        private static Type T => Type.GetType("ShiftFour.FirstPersonIdleMotion, Assembly-CSharp", true);
        private static object Get(object o, string n) => T.GetProperty(n).GetValue(o);
        private static void Advance(object o, double dt, bool suppressed = false) => T.GetMethod("Advance").Invoke(o, new object[] { dt, suppressed });
        private static Vector3 Position(object o) => (Vector3)Get(o, "PositionOffset");
        private static Quaternion Rotation(object o) => (Quaternion)Get(o, "RotationOffset");
        private static double Degrees(Quaternion q) => 2 * Math.Asin(Math.Min(1, Math.Sqrt(q.x * (double)q.x + q.y * (double)q.y + q.z * (double)q.z))) * 180 / Math.PI;

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ThirtySeconds_AtEachFrameRate_LoopsWithoutAccumulatingTransformDrift(int fps)
        {
            var root = new GameObject("Idle sampling test"); var motion = root.AddComponent(T);
            try
            {
                Vector3 previous = Position(motion); float maxStep = 0;
                for (int i = 0; i < fps * 30; i++)
                {
                    Advance(motion, 1d / fps);
                    Vector3 p = Position(motion);
                    Assert.That(p.magnitude, Is.LessThan(.001f));
                    Assert.That(Degrees(Rotation(motion)), Is.LessThan(.1f));
                    maxStep = Mathf.Max(maxStep, Vector3.Distance(p, previous)); previous = p;
                }
                Assert.That((double)Get(motion, "ElapsedSeconds"), Is.EqualTo(30).Within(.0000001));
                Assert.That(Position(motion).magnitude, Is.LessThan(.0000001f), "Six five-second cycles must return to the neutral offset.");
                Assert.That(Degrees(Rotation(motion)), Is.LessThan(.001f));
                Assert.That(maxStep, Is.LessThan(.00012f), "No visible per-frame translation jump at 30/60/120 FPS.");
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void SamplingAndActivityBlends_MatchAtThirtySixtyAndOneTwentyFps()
        {
            Vector3[] expectedPosition = null; Quaternion[] expectedRotation = null; float[] expectedWeight = null;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var root = new GameObject("Idle rate comparison"); var motion = root.AddComponent(T);
                try
                {
                    var positions = new Vector3[5]; var rotations = new Quaternion[5]; var weights = new float[5];
                    for (int i = 1; i <= fps * 5 / 2; i++)
                    {
                        bool suppressed = i > fps && i <= fps * 3 / 2;
                        Advance(motion, 1d / fps, suppressed);
                        if (i % (fps / 2) == 0)
                        {
                            int sample = i / (fps / 2) - 1;
                            positions[sample] = Position(motion); rotations[sample] = Rotation(motion); weights[sample] = (float)Get(motion, "Weight");
                        }
                    }
                    Assert.That(weights[2], Is.LessThan(.1f)); Assert.That(weights[4], Is.GreaterThan(.99f));
                    if (expectedPosition != null)
                        for (int i = 0; i < positions.Length; i++)
                        {
                            Assert.That(Vector3.Distance(positions[i], expectedPosition[i]), Is.LessThan(.00000001f));
                            Assert.That(Degrees(Quaternion.Inverse(rotations[i]) * expectedRotation[i]), Is.LessThan(.001f));
                            Assert.That(weights[i], Is.EqualTo(expectedWeight[i]).Within(.000001f));
                        }
                    expectedPosition = positions; expectedRotation = rotations; expectedWeight = weights;
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test] public void LoopBoundary_HasContinuousPositionAndVelocity()
        {
            var root = new GameObject("Idle loop boundary"); var motion = root.AddComponent(T);
            try
            {
                Advance(motion, 30); Advance(motion, 5 - .002);
                Vector3 a = Position(motion); Advance(motion, .001); Vector3 b = Position(motion);
                Advance(motion, .001); Vector3 c = Position(motion); Advance(motion, .001); Vector3 d = Position(motion);
                Assert.That(c.magnitude, Is.LessThan(.0000001f));
                Assert.That(Vector3.Distance((b - a) / .001f, (d - c) / .001f), Is.LessThan(.000001f));
                Assert.That(Vector3.Distance(b, d), Is.LessThan(.000004f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void ZeroDeltaAndInvalidTime_DoNotAdvanceOrCorruptThePose()
        {
            var root = new GameObject("Idle pause test"); var motion = root.AddComponent(T);
            try
            {
                Advance(motion, .8); var p = Position(motion); var q = Rotation(motion); var t = Get(motion, "ElapsedSeconds");
                foreach (double dt in new[] { 0d, -1d, double.NaN, double.PositiveInfinity }) Advance(motion, dt, true);
                Assert.That(Position(motion), Is.EqualTo(p)); Assert.That(Rotation(motion), Is.EqualTo(q));
                Assert.That(Get(motion, "ElapsedSeconds"), Is.EqualTo(t));
                T.GetMethod("ResetMotion").Invoke(motion, null);
                Assert.That(Position(motion), Is.EqualTo(Vector3.zero)); Assert.That(Rotation(motion), Is.EqualTo(Quaternion.identity));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void PistolPrefab_PersistsRestrainedConfigurableIdleSettings()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab"); var idle = root.GetComponent(T);
            Assert.That(idle, Is.Not.Null);
            Assert.That((float)Get(idle, "BreathsPerMinute"), Is.InRange(6, 24));
            Assert.That((float)Get(idle, "VerticalAmplitude"), Is.InRange(.0001f, .0015f));
            Assert.That((float)Get(idle, "LateralAmplitude"), Is.InRange(0, .0005f));
            Assert.That((float)Get(idle, "DepthAmplitude"), Is.InRange(0, .0005f));
            Assert.That((float)Get(idle, "BlendSeconds"), Is.InRange(.05f, .5f));
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                Assert.That(animator.applyRootMotion, Is.False, "Idle must not enable root motion on existing animation components.");
        }
    }
}
