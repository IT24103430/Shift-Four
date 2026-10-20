using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    public readonly struct WeaponShot
    {
        public readonly WeaponDefinition Definition;
        public readonly PlayerController Attacker;
        public readonly Ray Ray;
        public readonly bool HasHit;
        public readonly RaycastHit Hit;
        public readonly float Time, Damage;

        public WeaponShot(WeaponDefinition definition, PlayerController attacker, Ray ray,
            bool hasHit, RaycastHit hit, float time, float damage)
        { Definition = definition; Attacker = attacker; Ray = ray; HasHit = hasHit; Hit = hit; Time = time; Damage = damage; }
    }

    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        // Layer 2 is Unity's built-in Ignore Raycast layer; no project layer edits are needed.
        public const int GameplayRaycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] private WeaponDefinition initialWeapon;

        private readonly Dictionary<WeaponDefinition, WeaponRuntimeState> states = new Dictionary<WeaponDefinition, WeaponRuntimeState>();
        private readonly List<FirstPersonViewmodel> viewmodels = new List<FirstPersonViewmodel>();
        private PlayerController owner;
        private Health health;
        private WeaponDefinition fallback;
        private bool firing, changingWeapon;

        public WeaponRuntimeState ActiveWeapon { get; private set; }
        public event Action<WeaponShot> ShotAccepted;
        public event Action<WeaponDefinition> WeaponEquipped;
        public event Action<WeaponReloadState> ReloadStateChanged;

        private bool CanOperate => isActiveAndEnabled && owner != null && owner.CanAct;

        public void Initialize(PlayerController player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.gameObject != gameObject) throw new ArgumentException("WeaponController must share its player's root.", nameof(player));
            if (owner != null)
            {
                if (owner != player) throw new InvalidOperationException("A weapon controller already belongs to another player.");
                return;
            }
            owner = player; health = player.GetComponent<Health>();
            health.Died += OnOwnerDied;
            if (initialWeapon == null) initialWeapon = Resources.Load<WeaponDefinition>("Weapons/FNFiveSeven");
            if (initialWeapon == null) { fallback = WeaponDefinition.CreateFallback(); initialWeapon = fallback; }
            Activate(initialWeapon);
            var mount = GetComponent<FirstPersonWeaponMount>();
            if (mount == null) mount = gameObject.AddComponent<FirstPersonWeaponMount>();
            mount.Initialize(this, player);
            foreach (var model in GetComponentsInChildren<FirstPersonViewmodel>(true))
                if (model.isActiveAndEnabled) model.Bind(this);
        }

        // Player calls this once after look/movement/interaction, preserving the original input order.
        public void ProcessInput()
        {
            ProcessFireInput(Input.GetMouseButton(0));
        }

        public bool ProcessFireInput(bool held)
            => held && Cursor.lockState == CursorLockMode.Locked && TryFire();

        public bool TryFire()
        {
            if (firing || changingWeapon || !CanOperate || ActiveWeapon == null) return false;
            WeaponRuntimeState state = ActiveWeapon;
            WeaponDefinition definition = state.Definition;
            float now = Time.time, damage = definition.Damage, range = definition.Range;
            if (!state.TryAcceptShot(now, definition.FireInterval)) return false;

            firing = true;
            try
            {
                PrepareViewmodelForQueries();
                Camera camera = owner.ViewCamera;
                Ray ray = new Ray(camera.transform.position, camera.transform.forward);
                // Preserve the producer position, null noise Source and sound-before-damage order.
                SoundEvents.ReportShot(owner.transform.position);
                bool hasHit = Physics.Raycast(ray, out RaycastHit hit, range, GameplayRaycastMask, QueryTriggerInteraction.Ignore);
                if (hasHit)
                {
                    Health victim = hit.collider.GetComponentInParent<Health>();
                    if (victim != null && victim != health) victim.ApplyDamage(damage, owner, "Player hitscan shot");
                }
                Notify(ShotAccepted, new WeaponShot(definition, owner, ray, hasHit, hit, now, damage));
                return true;
            }
            finally { firing = false; }
        }

        // Switching retains each weapon's cooldown rather than creating a fresh ready state.
        public bool Equip(WeaponDefinition definition)
        {
            if (firing || changingWeapon || !CanOperate || definition == null || ActiveWeapon?.Definition == definition) return false;
            Activate(definition);
            return true;
        }

        private void Activate(WeaponDefinition definition)
        {
            changingWeapon = true;
            try
            {
                CancelReload();
                if (!states.TryGetValue(definition, out WeaponRuntimeState state))
                {
                    state = new WeaponRuntimeState(definition);
                    states.Add(definition, state);
                }
                ActiveWeapon = state;
                Notify(WeaponEquipped, definition);
            }
            finally { changingWeapon = false; }
        }

        // Explicit lifecycle hooks for the later ammo/animation implementation; no new R binding.
        public bool RequestReload()
        {
            if (firing || changingWeapon || !CanOperate || ActiveWeapon == null || !ActiveWeapon.BeginReload()) return false;
            Notify(ReloadStateChanged, WeaponReloadState.Reloading);
            return true;
        }

        public bool CompleteReload()
        {
            if (firing || ActiveWeapon == null || !ActiveWeapon.EndReload()) return false;
            Notify(ReloadStateChanged, WeaponReloadState.Idle);
            return true;
        }

        public bool CancelReload() => CompleteReload();

        private void Update()
        {
            if (!CanOperate && !firing) CancelReload();
        }

        private void OnOwnerDied(Health _) => CancelReload();
        private void OnDisable() => CancelReload();
        private void OnDestroy()
        {
            if (health != null) health.Died -= OnOwnerDied;
            if (fallback != null) Destroy(fallback);
        }

        public void PrepareViewmodelForQueries()
        {
            for (int i = viewmodels.Count - 1; i >= 0; i--)
            {
                if (viewmodels[i] == null) viewmodels.RemoveAt(i);
                else viewmodels[i].PrepareForQueries();
            }
        }

        internal void Register(FirstPersonViewmodel model)
        {
            if (!viewmodels.Contains(model)) viewmodels.Add(model);
        }
        internal void Unregister(FirstPersonViewmodel model) => viewmodels.Remove(model);

        // Presentation observers cannot interrupt accepted damage or later observers.
        internal static void Notify<T>(Action<T> listeners, T value)
        {
            if (listeners == null) return;
            foreach (Action<T> listener in listeners.GetInvocationList())
            {
                if (listener.Target is UnityEngine.Object target && target == null) continue;
                try { listener(value); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
