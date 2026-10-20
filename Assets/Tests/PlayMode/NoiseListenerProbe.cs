#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace ShiftFour.Tests
{
    // Runtime test observer. Can deliberately omit cleanup to exercise the bus safety net.
    public sealed class NoiseListenerProbe : MonoBehaviour
    {
        public bool UnsubscribeOnDisable = true;
        public int Noises, Shots;
        public object LastNoise;
        private Delegate noiseHandler;
        private Action<Vector3> shotHandler;
        private static Type Bus => Type.GetType("ShiftFour.SoundEvents, Assembly-CSharp", true);

        private void OnEnable() => Register();
        private void OnDisable() { if (UnsubscribeOnDisable) Unregister(); }

        public void Register()
        {
            var emitted = Bus.GetEvent("NoiseEmitted");
            if (noiseHandler == null)
            {
                Type value = emitted.EventHandlerType.GetGenericArguments()[0];
                MethodInfo method = GetType().GetMethod(nameof(OnNoise), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(value);
                noiseHandler = Delegate.CreateDelegate(emitted.EventHandlerType, this, method);
                shotHandler = OnShot;
            }
            emitted.AddEventHandler(null, noiseHandler);
            Bus.GetEvent("ShotFired").AddEventHandler(null, shotHandler);
        }

        public void Unregister()
        {
            Bus.GetEvent("NoiseEmitted").RemoveEventHandler(null, noiseHandler);
            Bus.GetEvent("ShotFired").RemoveEventHandler(null, shotHandler);
        }

        private void OnNoise<T>(T noise) { Noises++; LastNoise = noise; }
        private void OnShot(Vector3 position) => Shots++;
    }
}
#endif
