using System;
using UnityEngine;

namespace ShiftFour
{
    // Runtime camera wiring and one root presentation layer; scene/imported assets stay untouched.
    [DisallowMultipleComponent]
    public sealed class FirstPersonWeaponMount : MonoBehaviour
    {
        private WeaponController weapons;
        private PlayerController player;
        private WeaponDefinition displayed;
        private FirstPersonEquipMotion motion;
        private FirstPersonViewmodelFraming framing;
        private readonly FirstPersonEquipTransition transition = new FirstPersonEquipTransition();
        private Vector3 modelRestPosition;
        private Quaternion modelRestRotation;
        private bool wantsVisible = true, wasOperational, refreshing, requesting, notifying;
        private WeaponPresentationState notifiedState = WeaponPresentationState.Hidden;
        public Transform Socket { get; private set; }
        public FirstPersonViewmodel Instance { get; private set; }
        public float PresentationRetreat { get; private set; }
        public WeaponPresentationState TransitionState => transition.State;
        public float EquipVisibility => transition.Visibility;
        public float EquipDuration => motion != null ? motion.EquipDuration : 0;
        public float UnequipDuration => motion != null ? motion.UnequipDuration : 0;
        public event Action<WeaponPresentationState> TransitionChanged;
        private bool Operational => isActiveAndEnabled && weapons != null && weapons.isActiveAndEnabled && player != null && player.CanPresentWeapon && player.ViewCamera != null;
        public bool IsReady => Operational && wantsVisible && displayed == weapons.ActiveWeapon?.Definition && TransitionState == WeaponPresentationState.Ready;
        public bool AllowsFiring => IsReady || (Operational && wantsVisible && displayed == weapons.ActiveWeapon?.Definition &&
            TransitionState == WeaponPresentationState.Equipping && motion != null && !motion.BlockFiringDuringEquip);
        public bool AllowsReload => IsReady;
        private readonly RaycastHit[] obstructionHits = new RaycastHit[16];

        public void Initialize(WeaponController controller, PlayerController actor)
        {
            if (weapons != null) return;
            weapons = controller; player = actor;
            weapons.WeaponEquipped += OnEquipped;
            Refresh();
        }

        // Presentation hooks for a future switching coordinator. They never replace runtime state.
        public bool RequestUnequip()
        {
            if (!Operational || !wantsVisible || requesting || notifying) return false;
            requesting = true;
            try { wantsVisible = false; weapons.CancelReload(); Refresh(); return true; }
            finally { requesting = false; }
        }

        public bool RequestEquip()
        {
            if (!Operational || wantsVisible || requesting || notifying) return false;
            wantsVisible = true; Refresh(); return true;
        }

        private void OnEquipped(WeaponDefinition _) { wantsVisible = true; Refresh(); }
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (refreshing || weapons == null || player == null) return;
            refreshing = true;
            try
            {
                double now = Time.timeAsDouble;
                if (!Operational)
                {
                    transition.ResetHidden(now); wasOperational = false;
                    if (Socket != null) Socket.gameObject.SetActive(false);
                    NotifyTransition(); return;
                }
                Camera camera = player.ViewCamera;
                WeaponDefinition definition = weapons.ActiveWeapon?.Definition;
                if (Socket == null && definition != null && definition.ViewmodelPrefab != null)
                {
                    Socket = new GameObject("WeaponSocket").transform; Socket.gameObject.layer = 2;
                }
                if (Socket != null && Socket.parent != camera.transform)
                {
                    Socket.SetParent(camera.transform, false);
                    Socket.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    Socket.localScale = Vector3.one;
                }
                if (!wasOperational) { transition.ResetHidden(now); wasOperational = true; }
                transition.Sample(now);
                bool changed = displayed != definition || (Instance == null && definition != null && definition.ViewmodelPrefab != null);
                if (changed)
                {
                    var destination = definition != null && definition.ViewmodelPrefab != null
                        ? definition.ViewmodelPrefab.GetComponent<FirstPersonEquipMotion>() : null;
                    // Model-less/legacy definitions retain their immediate API contract. Animated
                    // models lower the old instance before creating the latest requested model.
                    if (Instance != null && motion != null && destination != null && destination.enabled && TransitionState != WeaponPresentationState.Hidden)
                        transition.RequestUnequip(now, motion.UnequipDuration);
                    else ReplaceModel(definition, now);
                }
                if (displayed == definition)
                {
                    if (motion == null)
                    {
                        if (wantsVisible) transition.ResetReady(now); else transition.ResetHidden(now);
                    }
                    else if (wantsVisible) transition.RequestEquip(now, motion.EquipDuration);
                    else transition.RequestUnequip(now, motion.UnequipDuration);
                }
                if (Instance != null)
                {
                    float lowered = 1 - transition.Visibility;
                    Instance.transform.localPosition = modelRestPosition + (motion != null ? motion.LoweredPosition * lowered : Vector3.zero);
                    Instance.transform.localRotation = modelRestRotation * (motion != null ? Quaternion.Euler(motion.LoweredRotation * lowered) : Quaternion.identity);
                }
                if (Socket != null)
                {
                    Socket.gameObject.SetActive(Instance != null && TransitionState != WeaponPresentationState.Hidden);
                    if (Socket.gameObject.activeSelf) UpdateRetreat(camera);
                }
                NotifyTransition();
            }
            finally { refreshing = false; }
        }

        private void ReplaceModel(WeaponDefinition definition, double now)
        {
            if (Instance != null)
            {
                Instance.gameObject.SetActive(false);
                // Deferred destruction must not leave an old viewmodel under the player hierarchy.
                Instance.transform.SetParent(null, true); Destroy(Instance.gameObject);
            }
            Instance = null; motion = null; framing = null; displayed = definition; transition.ResetHidden(now);
            if (definition == null || definition.ViewmodelPrefab == null || Socket == null) return;
            Instance = Instantiate(definition.ViewmodelPrefab, Socket, false);
            Instance.name = definition.ViewmodelPrefab.name;
            modelRestPosition = Instance.transform.localPosition; modelRestRotation = Instance.transform.localRotation;
            framing = Instance.GetComponent<FirstPersonViewmodelFraming>();
            motion = Instance.GetComponent<FirstPersonEquipMotion>(); if (motion != null && !motion.enabled) motion = null;
            Instance.Bind(weapons);
        }

        private void UpdateRetreat(Camera camera)
        {
            float nearest = .6f;
            int count = Physics.SphereCastNonAlloc(camera.transform.position, .12f, camera.transform.forward,
                obstructionHits, nearest, WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (obstructionHits[i].collider != null && !obstructionHits[i].collider.transform.IsChildOf(player.transform))
                    nearest = Mathf.Min(nearest, obstructionHits[i].distance);
            float target = Mathf.Clamp(.58f - nearest, 0, framing != null ? framing.MaximumRetreat : .24f);
            // Retract immediately to avoid clipping; ease back after clearing cover.
            PresentationRetreat = target > PresentationRetreat ? target : Mathf.MoveTowards(PresentationRetreat, target, Time.deltaTime * .8f);
            Socket.localPosition = Vector3.back * PresentationRetreat;
        }

        private void NotifyTransition()
        {
            if (notifiedState == TransitionState) return;
            notifiedState = TransitionState; notifying = true;
            try { WeaponController.Notify(TransitionChanged, notifiedState); }
            finally { notifying = false; }
        }

        private void OnDisable()
        {
            if (weapons != null) weapons.CancelReload();
            transition.ResetHidden(Time.timeAsDouble); wasOperational = false;
            if (Socket != null) Socket.gameObject.SetActive(false);
            NotifyTransition();
        }
        private void OnDestroy()
        {
            if (weapons != null) weapons.WeaponEquipped -= OnEquipped;
            if (Socket != null) Destroy(Socket.gameObject);
        }
    }
}
