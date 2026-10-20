using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftFour
{
    // Presentation only: observes accepted damage, never awards damage or changes combat timing.
    [DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        internal sealed class DroneBar
        {
            public DroneBrain Brain;
            public Health Health;
            public DroneBody Body;
            public Action<DamageRecord> Listener;
            public string Label;
            public float RevealUntil, Alpha, Fraction, Trail, Maximum;
            public Vector2 ScreenPoint;
            public bool Visible;
        }

        private GameSession session;
        private PlayerController player;
        private Health health;
        private WeaponController weapons;
        private readonly List<DroneBar> drones = new List<DroneBar>();
        private readonly CombatHudRenderer presentation = new CombatHudRenderer();
        private bool bound;
        private float lastHealth, maximum, damageAt = float.NegativeInfinity;
        private float recoveredAt = float.NegativeInfinity, hitUntil, defeatUntil, shotAt = float.NegativeInfinity;
        private Vector3 damageOrigin;
        private bool knownOrigin;

        public float DisplayedHealthFraction { get; private set; }
        public float DamageTrailFraction { get; private set; }
        public float PlayerHealthFraction => health != null ? Mathf.Clamp01(health.Current / Mathf.Max(1, health.Maximum)) : 0;
        public bool IsRecovering => health != null && health.IsAlive && PlayerHealthFraction < .999f && Time.unscaledTime - recoveredAt < .25f;
        public float HitMarkerStrength => Mathf.Clamp01((hitUntil - Time.unscaledTime) / .22f);
        public float DefeatMarkerStrength => Mathf.Clamp01((defeatUntil - Time.unscaledTime) / .65f);
        public float DamageFlashStrength => Mathf.Clamp01(1 - (Time.unscaledTime - damageAt) / .55f);
        public bool DamageDirectionKnown => knownOrigin && Time.unscaledTime - damageAt < .85f;
        public int VisibleDroneBarCount { get; private set; }

        public void Initialize(GameSession game, PlayerController actor, DroneBrain[] targets)
        {
            Unbind();
            session = game; player = actor;
            health = player != null ? player.GetComponent<Health>() : null;
            weapons = player != null ? player.Weapons : null;
            drones.Clear();
            if (targets != null) foreach (var target in targets)
            {
                if (target == null) continue;
                var targetHealth = target.GetComponent<Health>();
                if (targetHealth == null || drones.Exists(d => d.Health == targetHealth)) continue;
                var bar = new DroneBar { Brain = target, Health = targetHealth, Body = target.GetComponent<DroneBody>(),
                    Label = string.IsNullOrEmpty(target.AgentName) ? target.GetType().Name.Replace("Brain", "").ToUpperInvariant() : target.AgentName.ToUpperInvariant() };
                bar.Listener = damage => OnDroneDamage(bar, damage);
                drones.Add(bar);
            }
            ResetPresentation();
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() { ResetPresentation(); Bind(); }
        private void OnDisable() { Unbind(); ResetPresentation(); }
        private void OnDestroy() => Unbind();
        private void Bind()
        {
            if (bound || health == null) return;
            health.Damaged += OnPlayerDamage;
            if (weapons != null) weapons.ShotAccepted += OnShot;
            foreach (var bar in drones) if (bar.Health != null) bar.Health.Damaged += bar.Listener;
            bound = true;
        }
        private void Unbind()
        {
            if (!bound) return;
            if (health != null) health.Damaged -= OnPlayerDamage;
            if (weapons != null) weapons.ShotAccepted -= OnShot;
            foreach (var bar in drones) if (bar.Health != null) bar.Health.Damaged -= bar.Listener;
            bound = false;
        }
        private void ResetPresentation()
        {
            hitUntil = defeatUntil = 0; shotAt = damageAt = recoveredAt = float.NegativeInfinity;
            knownOrigin = false; VisibleDroneBarCount = 0;
            lastHealth = health != null ? health.Current : 0;
            maximum = health != null ? health.Maximum : 1;
            DisplayedHealthFraction = DamageTrailFraction = PlayerHealthFraction;
            foreach (var bar in drones)
            {
                bar.Visible = false; bar.Alpha = bar.RevealUntil = 0;
                bar.Maximum = bar.Health != null ? bar.Health.Maximum : 1;
                bar.Fraction = bar.Trail = bar.Health != null ? Mathf.Clamp01(bar.Health.Current / Mathf.Max(1, bar.Maximum)) : 0;
            }
        }
        private void OnPlayerDamage(DamageRecord damage)
        {
            if (damage.Before <= damage.After) return;
            damageAt = Time.unscaledTime;
            DamageTrailFraction = Mathf.Max(DamageTrailFraction, damage.Before / Mathf.Max(1, health.Maximum));
            knownOrigin = false;
            if (damage.Attacker is Component component) { damageOrigin = component.transform.position; knownOrigin = true; }
            else if (damage.Attacker is GameObject actor) { damageOrigin = actor.transform.position; knownOrigin = true; }
            if (knownOrigin && Vector3.ProjectOnPlane(damageOrigin - player.transform.position, Vector3.up).sqrMagnitude < .01f)
                knownOrigin = false;
        }
        private void OnDroneDamage(DroneBar bar, DamageRecord damage)
        {
            if (damage.Attacker != player || damage.Before <= damage.After || session == null || session.Ended) return;
            bar.RevealUntil = Time.unscaledTime + 3;
            bar.Trail = Mathf.Max(bar.Trail, damage.Before / Mathf.Max(1, bar.Health.Maximum));
            hitUntil = Time.unscaledTime + .22f;
            if (damage.Fatal) defeatUntil = Time.unscaledTime + .65f;
        }
        private void OnShot(WeaponShot _) => shotAt = Time.unscaledTime;

        private void Update()
        {
            if (session == null || player == null || health == null || !session.isActiveAndEnabled) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f), now = Time.unscaledTime;
            if (!Mathf.Approximately(maximum, health.Maximum))
            { maximum = health.Maximum; DisplayedHealthFraction = DamageTrailFraction = PlayerHealthFraction; }
            if (health.Current > lastHealth + .0001f) recoveredAt = now;
            lastHealth = health.Current;
            DisplayedHealthFraction = Mathf.Lerp(DisplayedHealthFraction, PlayerHealthFraction, 1 - Mathf.Exp(-24 * dt));
            if (now - damageAt > .45f) DamageTrailFraction = Mathf.MoveTowards(DamageTrailFraction, PlayerHealthFraction, dt * .65f);
            DamageTrailFraction = Mathf.Max(DamageTrailFraction, DisplayedHealthFraction);
            UpdateDroneBars(dt, now);
        }
        private void UpdateDroneBars(float dt, float now)
        {
            VisibleDroneBarCount = 0;
            var camera = player.ViewCamera;
            if (camera == null) return;
            Health aimed = null;
            if (!session.Ended && Physics.Raycast(camera.transform.position, camera.transform.forward, out var aim,
                30, WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore)) aimed = aim.collider.GetComponentInParent<Health>();
            foreach (var bar in drones)
            {
                bar.Visible = false;
                if (session.Ended || bar.Brain == null || bar.Health == null || !bar.Health.IsAlive)
                { bar.Alpha = 0; continue; }
                if (aimed == bar.Health) bar.RevealUntil = Mathf.Max(bar.RevealUntil, now + .65f);
                bool reveal = now < bar.RevealUntil;
                bar.Alpha = Mathf.MoveTowards(bar.Alpha, reveal ? 1 : 0, dt * (reveal ? 9 : 4));
                if (bar.Alpha <= 0) continue;
                Vector3 center = bar.Brain.transform.TransformPoint(bar.Body != null ? bar.Body.EnvelopeCenter : Vector3.up * 1.5f);
                Vector3 anchor = center + Vector3.up * (bar.Body != null ? bar.Body.EnvelopeExtents.y + .22f : .8f);
                var screen = camera.WorldToViewportPoint(anchor);
                Vector3 ray = center - camera.transform.position;
                if (screen.z <= camera.nearClipPlane || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1 || ray.sqrMagnitude > 900)
                    continue;
                // Check the solid body, not the floating label anchor. Other actors also occlude the bar.
                if (!Physics.Raycast(camera.transform.position, ray.normalized, out var hit, ray.magnitude + .1f,
                    WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<Health>() != bar.Health)
                    continue;
                if (!Mathf.Approximately(bar.Maximum, bar.Health.Maximum))
                { bar.Maximum = bar.Health.Maximum; bar.Fraction = bar.Trail = bar.Health.Current / Mathf.Max(1, bar.Maximum); }
                float fraction = Mathf.Clamp01(bar.Health.Current / Mathf.Max(1, bar.Health.Maximum));
                bar.Fraction = Mathf.Lerp(bar.Fraction, fraction, 1 - Mathf.Exp(-28 * dt));
                bar.Trail = Mathf.MoveTowards(bar.Trail, fraction, dt * .45f);
                bar.Trail = Mathf.Max(bar.Trail, bar.Fraction);
                bar.ScreenPoint = new Vector2(screen.x * Screen.width, (1 - screen.y) * Screen.height);
                bar.Visible = true; VisibleDroneBarCount++;
            }
        }
        public bool IsDroneBarVisible(DroneBrain target) => drones.Exists(d => d.Brain == target && d.Visible);

        private float DamageBearing()
        {
            Vector3 toward = damageOrigin - player.ViewCamera.transform.position; toward.y = 0;
            Vector3 forward = player.ViewCamera.transform.forward; forward.y = 0;
            if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
            forward.Normalize();
            return Mathf.Atan2(Vector3.Dot(toward, Vector3.Cross(Vector3.up, forward)), Vector3.Dot(toward, forward)) * Mathf.Rad2Deg;
        }
        private void OnGUI()
        {
            if (session == null || !session.isActiveAndEnabled || player == null || health == null || Event.current.type != EventType.Repaint) return;
            presentation.Draw(session, player, health, drones, DisplayedHealthFraction, DamageTrailFraction, IsRecovering,
                HitMarkerStrength, DefeatMarkerStrength, DamageFlashStrength,
                DamageDirectionKnown ? (float?)DamageBearing() : null, Mathf.Clamp01(1 - (Time.unscaledTime - shotAt) / .15f));
        }
    }
}
