using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class RapidResponseAudioEditModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        [TestCase("PlayerPistol", 1.724f)] [TestCase("DroneShot", 1.097f)]
        [TestCase("DroneFlight", 44.04f)] [TestCase("LabDoorOpen", 4.824f)]
        [TestCase("PlayerReload", 1.512f)]
        public void GameplayEffects_ImportAsFiniteNonSilentMonoPCM(string name, float duration)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            Assert.That(clip, Is.Not.Null); Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.length, Is.EqualTo(duration).Within(.002f));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            var samples = new float[clip.samples]; Assert.That(clip.GetData(samples, 0), Is.True);
            double power = 0; float peak = 0; bool finite = true;
            foreach (float value in samples)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) finite = false;
                power += value * value; peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            Assert.That(finite, Is.True);
            Assert.That(peak, Is.InRange(.1f, .99f));
            Assert.That(Math.Sqrt(power / samples.Length), Is.GreaterThan(.005));
        }

        [Test] public void BackgroundMusic_PreservesStereoAndStreamsWithoutPreloadingWholeTrack()
        {
            const string path = "Assets/Resources/Audio/BackgroundMusic.mp3";
            var clip = Resources.Load<AudioClip>("Audio/BackgroundMusic");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.channels, Is.EqualTo(2));
            Assert.That(clip.length, Is.EqualTo(60.432f).Within(.1f));
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.False);
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
