using System;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class RapidResponseAudioEditModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        [TestCase("PlayerPistol", .28f)] [TestCase("DroneShot", .23f)] [TestCase("LabDoorOpen", 1.6f)]
        public void OwnedEffects_ImportAsFiniteNonSilentMonoPCM(string name, float duration)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            Assert.That(clip, Is.Not.Null); Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(duration).Within(.002f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            var samples = new float[clip.samples]; Assert.That(clip.GetData(samples, 0), Is.True);
            double power = 0; float peak = 0;
            foreach (float value in samples)
            {
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                power += value * value; peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            Assert.That(peak, Is.InRange(.1f, .99f));
            Assert.That(Math.Sqrt(power / samples.Length), Is.GreaterThan(.005));
        }

        [Test] public void FastAudibleEvidence_InvestigatesImmediatelyWithoutCombatOrDeadlineRefresh()
        {
            object settings = Activator.CreateInstance(T("AwarenessSettings"));
            settings.GetType().GetProperty("ImmediateSoundInvestigation").SetValue(settings, true);
            settings.GetType().GetField("minimumNoiseStrength").SetValue(settings, .01f);
            var difficulty = T("DroneDifficultySettings").GetProperty("Default").GetValue(null);
            object model = Activator.CreateInstance(T("DroneAwareness"), settings, difficulty);
            object sound = Activator.CreateInstance(T("SoundObservation"), Vector3.right * 4,
                Enum.Parse(T("NoiseCategory"), "Footstep"), .02f, 1f, 0f, 5f);
            void Step(float at) => model.GetType().GetMethod("Step").Invoke(model, new[] { (object)at, false, Vector3.zero, true, sound, false });
            string State() => model.GetType().GetProperty("State").GetValue(model).ToString();
            Step(0); Assert.That(State(), Is.EqualTo("Investigate"));
            Step(.1f); Assert.That(State(), Is.EqualTo("Investigate"));
            Assert.That(model.GetType().GetProperty("HasVisualConfirmation").GetValue(model), Is.False);
            Step(2.6f); Assert.That(State(), Is.EqualTo("Search"));
            Assert.That(model.GetType().GetProperty("LastHeardAt").GetValue(model), Is.EqualTo(0f));
        }
    }
}
