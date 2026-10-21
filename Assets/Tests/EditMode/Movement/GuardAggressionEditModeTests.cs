using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class GuardAggressionEditModeTests
    {
        [TestCase(true)] [TestCase(false)]
        public void ConfiguredPressure_PursuesVisibleThreatButNeverHiddenPlayerPosition(bool aggressive)
        {
            var go = new GameObject("Guard policy fixture"); go.SetActive(false);
            try
            {
                var type = Type.GetType("ShiftFour.GuardBrain, Assembly-CSharp", true);
                var brain = go.AddComponent(type);
                type.GetField("aggressivePursuit", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(brain, aggressive);
                var decide = type.GetMethod("Decide", BindingFlags.NonPublic | BindingFlags.Instance);
                object choice = decide.Invoke(brain, new object[] { true, Vector3.forward * 10, 10f });
                Assert.That(choice.GetType().GetField("Name").GetValue(choice), Is.EqualTo(aggressive ? "Pressure visible player" : "Guard server room"));
                choice = decide.Invoke(brain, new object[] { false, new Vector3(999, 0, 999), 1400f });
                Assert.That(choice.GetType().GetField("Destination").GetValue(choice), Is.EqualTo(Vector3.zero));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
