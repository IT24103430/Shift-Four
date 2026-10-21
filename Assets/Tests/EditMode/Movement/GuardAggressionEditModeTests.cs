using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class GuardAggressionEditModeTests
    {
        [Test]
        public void UnavailableNavigation_HoldsInsteadOfPursuingVisibleOrHiddenPlayerPosition()
        {
            var go = new GameObject("Guard policy fixture"); go.SetActive(false);
            try
            {
                var type = Type.GetType("ShiftFour.GuardBrain, Assembly-CSharp", true);
                var brain = go.AddComponent(type);
                var decide = type.GetMethod("Decide", BindingFlags.NonPublic | BindingFlags.Instance);
                object choice = decide.Invoke(brain, new object[] { true, Vector3.forward * 10, 10f });
                Assert.That(choice.GetType().GetField("Name").GetValue(choice), Is.EqualTo("Guard server room"));
                Assert.That(choice.GetType().GetField("Destination").GetValue(choice), Is.EqualTo(Vector3.zero));
                choice = decide.Invoke(brain, new object[] { false, new Vector3(999, 0, 999), 1400f });
                Assert.That(choice.GetType().GetField("Destination").GetValue(choice), Is.EqualTo(Vector3.zero));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
