#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class WeaponEventProbe : MonoBehaviour
    {
        public int Shots, Equips, ReloadChanges, Damages;
        public object LastShot, LastReload, LastDamage;
        public bool ReenterOnShot, ReentryAccepted, ThrowOnShot;
        private Component weapons, health;
        private Delegate shot, equip, reload, damaged;

        public void Bind(Component controller, Component target = null)
        {
            Unbind(); weapons = controller; health = target;
            shot = Subscribe(weapons, "ShotAccepted", nameof(OnShot));
            equip = Subscribe(weapons, "WeaponEquipped", nameof(OnEquip));
            reload = Subscribe(weapons, "ReloadStateChanged", nameof(OnReload));
            if (health != null) damaged = Subscribe(health, "Damaged", nameof(OnDamaged));
        }

        private Delegate Subscribe(Component publisher, string name, string method)
        {
            EventInfo info = publisher.GetType().GetEvent(name);
            MethodInfo callback = GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(info.EventHandlerType.GetGenericArguments()[0]);
            Delegate handler = Delegate.CreateDelegate(info.EventHandlerType, this, callback);
            info.AddEventHandler(publisher, handler); return handler;
        }

        private void OnShot<T>(T value)
        {
            Shots++; LastShot = value;
            if (ReenterOnShot) ReentryAccepted = (bool)weapons.GetType().GetMethod("TryFire").Invoke(weapons, null);
            if (ThrowOnShot) throw new InvalidOperationException("Fixture shot observer");
        }
        private void OnEquip<T>(T _) => Equips++;
        private void OnReload<T>(T state) { ReloadChanges++; LastReload = state; }
        private void OnDamaged<T>(T record) { Damages++; LastDamage = record; }
        private void OnDisable() => Unbind();
        private void Unbind()
        {
            if (weapons != null)
            {
                weapons.GetType().GetEvent("ShotAccepted").RemoveEventHandler(weapons, shot);
                weapons.GetType().GetEvent("WeaponEquipped").RemoveEventHandler(weapons, equip);
                weapons.GetType().GetEvent("ReloadStateChanged").RemoveEventHandler(weapons, reload);
            }
            if (health != null) health.GetType().GetEvent("Damaged").RemoveEventHandler(health, damaged);
            weapons = null; health = null;
        }
    }
}
#endif
