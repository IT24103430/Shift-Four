using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewmodel : MonoBehaviour
    {
        private readonly List<Transform> parts = new List<Transform>();
        private readonly List<Collider> colliders = new List<Collider>();
        private WeaponController owner;

        public WeaponDefinition EquippedWeapon { get; private set; }
        public WeaponReloadState ReloadState { get; private set; }
        public WeaponShot LastShot { get; private set; }
        public event Action<WeaponShot> ShotReceived;
        public event Action<WeaponDefinition> WeaponChanged;
        public event Action<WeaponReloadState> ReloadChanged;

        private void OnEnable() => Bind(GetComponentInParent<WeaponController>());
        private void OnDisable() => Unbind();
        private void OnTransformChildrenChanged() { if (owner != null) PrepareForQueries(); }

        public bool Bind(WeaponController weapons)
        {
            // A presentation root must never include the player's collision capsule.
            if (weapons == null || transform == weapons.transform || !transform.IsChildOf(weapons.transform) ||
                GetComponentInChildren<PlayerController>(true) != null) return false;
            if (owner == weapons) { PrepareForQueries(); return true; }
            Unbind(); owner = weapons; owner.Register(this);
            owner.ShotAccepted += OnShot;
            owner.WeaponEquipped += OnEquipped;
            owner.ReloadStateChanged += OnReload;
            EquippedWeapon = owner.ActiveWeapon?.Definition;
            ReloadState = owner.ActiveWeapon != null ? owner.ActiveWeapon.ReloadState : WeaponReloadState.Idle;
            PrepareForQueries();
            return true;
        }

        private void Unbind()
        {
            if (owner == null) return;
            owner.ShotAccepted -= OnShot;
            owner.WeaponEquipped -= OnEquipped;
            owner.ReloadStateChanged -= OnReload;
            owner.Unregister(this); owner = null;
        }

        // Also runs immediately before gameplay queries, covering dynamically added descendants.
        public void PrepareForQueries()
        {
            if (owner == null) return;
            parts.Clear(); GetComponentsInChildren(true, parts);
            foreach (Transform part in parts) part.gameObject.layer = 2;
            colliders.Clear(); GetComponentsInChildren(true, colliders);
            foreach (Collider collider in colliders) collider.enabled = false;
        }

        private void OnShot(WeaponShot shot) { LastShot = shot; WeaponController.Notify(ShotReceived, shot); }
        private void OnEquipped(WeaponDefinition definition) { EquippedWeapon = definition; WeaponController.Notify(WeaponChanged, definition); }
        private void OnReload(WeaponReloadState state) { ReloadState = state; WeaponController.Notify(ReloadChanged, state); }
    }
}
