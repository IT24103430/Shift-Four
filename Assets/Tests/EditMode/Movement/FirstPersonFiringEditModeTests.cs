using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class FirstPersonFiringEditModeTests
    {
        private static Type T => Type.GetType("ShiftFour.FirstPersonRecoilMotion, Assembly-CSharp", true);
        private static object Create() => Activator.CreateInstance(T);
        private static float Get(object o, string name) => (float)T.GetProperty(name).GetValue(o);
        private static void Shot(object o, double time, float rate = 22) => T.GetMethod("AcceptShot").Invoke(o, new object[] { time, rate });
        private static void Sample(object o, double time, float rate = 22) => T.GetMethod("Sample").Invoke(o, new object[] { time, rate });

        [Test] public void SingleAcceptedImpulse_HasImmediateRearwardTravelThenControlledMuzzleRiseAndRecovery()
        {
            var motion = Create(); Shot(motion, 10);
            Assert.That(Get(motion, "BackwardWeight"), Is.EqualTo(1f)); Assert.That(Get(motion, "MuzzleWeight"), Is.EqualTo(.65f));
            Sample(motion, 10.03);
            Assert.That(Get(motion, "BackwardWeight"), Is.InRange(.8f, .9f));
            Assert.That(Get(motion, "MuzzleWeight"), Is.InRange(.97f, 1f));
            Sample(motion, 10.45); Assert.That(Get(motion, "Strength"), Is.LessThan(.002f));
            Sample(motion, 11); Assert.That(Get(motion, "Strength"), Is.EqualTo(0f));
        }

        [Test] public void ShotCallbackAndLateUpdateAtTheSameTimestamp_DoNotAgeTheNewImpulse()
        {
            var motion = Create(); Sample(motion, 100); Shot(motion, 100.0333333333);
            for (int i = 0; i < 10; i++) Sample(motion, 100.0333333333);
            Assert.That(Get(motion, "BackwardWeight"), Is.EqualTo(1f)); Assert.That(Get(motion, "MuzzleWeight"), Is.EqualTo(.65f));
            Sample(motion, 100.05); Assert.That(Get(motion, "MuzzleWeight"), Is.GreaterThan(.9f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void RepeatedAcceptedShots_MatchCommonTimestampsAcrossFrameRatesWithoutAccumulatingRestPose(int fps)
        {
            var motion = Create(); var reference = Create();
            double[] shots = { 0, .24, .48, .72 }; int next = 0, nextReference = 0;
            for (int frame = 0; frame <= fps * 2; frame++)
            {
                double time = frame / (double)fps;
                while (next < shots.Length && shots[next] <= time + 1e-12) Shot(motion, shots[next++]);
                Sample(motion, time);
                Assert.That(Get(motion, "Strength"), Is.InRange(0, 1.5f));
                if (frame % (fps / 10) != 0) continue;
                while (nextReference < shots.Length && shots[nextReference] <= time + 1e-12) Shot(reference, shots[nextReference++]);
                Sample(reference, time);
                Assert.That(Get(motion, "BackwardWeight"), Is.EqualTo(Get(reference, "BackwardWeight")).Within(.000001f));
                Assert.That(Get(motion, "MuzzleWeight"), Is.EqualTo(Get(reference, "MuzzleWeight")).Within(.000001f));
            }
            Assert.That(Get(motion, "Strength"), Is.EqualTo(0f));
        }

        [Test] public void DenseImpulses_RemainBoundedAndSettleAfterFiringStops()
        {
            var motion = Create();
            for (int i = 0; i < 1000; i++) Shot(motion, 0);
            for (int frame = 0; frame < 240; frame++)
            {
                Sample(motion, frame / 120d);
                Assert.That(Get(motion, "BackwardWeight"), Is.InRange(0, 1.5f));
                Assert.That(Get(motion, "MuzzleWeight"), Is.InRange(0, 1.5f));
            }
            Assert.That(Get(motion, "Strength"), Is.EqualTo(0f));
        }

        [Test] public void PausedInvalidAndBackwardClocks_HoldThePose_AndResetClearsAllResidualMotion()
        {
            var motion = Create(); Shot(motion, 2); Sample(motion, 2.03);
            float back = Get(motion, "BackwardWeight"), muzzle = Get(motion, "MuzzleWeight");
            foreach (double time in new[] { 2.03, 1d, -1d, double.NaN, double.PositiveInfinity }) Sample(motion, time);
            foreach (double time in new[] { 1d, -1d, double.NaN, double.PositiveInfinity }) Shot(motion, time);
            Assert.That(Get(motion, "BackwardWeight"), Is.EqualTo(back)); Assert.That(Get(motion, "MuzzleWeight"), Is.EqualTo(muzzle));
            T.GetMethod("Reset").Invoke(motion, null); Assert.That(Get(motion, "Strength"), Is.EqualTo(0f));
            Shot(motion, 0); Assert.That(Get(motion, "BackwardWeight"), Is.EqualTo(1f));
            Sample(motion, double.MaxValue); Assert.That(Get(motion, "Strength"), Is.EqualTo(0f));
        }

        [Test] public void PistolPrefab_PersistsRestrainedRecoilAndOriginalFlashTiming()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab");
            var presenter = root.GetComponent(Type.GetType("ShiftFour.PistolPresentation, Assembly-CSharp", true));
            var s = new SerializedObject(presenter);
            Assert.That(s.FindProperty("recoilDistance").floatValue, Is.EqualTo(.012f));
            Assert.That(s.FindProperty("recoilDegrees").floatValue, Is.EqualTo(2f));
            Assert.That(s.FindProperty("recovery").floatValue, Is.EqualTo(22f));
            Assert.That(s.FindProperty("recoilLift").floatValue, Is.InRange(0, .001f));
            Assert.That(s.FindProperty("recoilRoll").floatValue, Is.InRange(0, .15f));
            Assert.That(s.FindProperty("flashDuration").floatValue, Is.EqualTo(.035f));
            Assert.That(s.FindProperty("muzzleFlash").objectReferenceValue, Is.Not.Null);
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true)) Assert.That(animator.applyRootMotion, Is.False);
        }
    }
}
