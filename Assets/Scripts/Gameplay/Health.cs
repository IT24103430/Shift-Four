using UnityEngine;

namespace ShiftFour
{
    public readonly struct DamageRecord
    {
        public readonly Object Attacker;
        public readonly string Reason;
        public readonly float Amount, Before, After, Time;
        public bool Fatal => After <= 0f;

        public DamageRecord(Object attacker, string reason, float amount, float before, float after, float time)
        { Attacker = attacker; Reason = reason; Amount = amount; Before = before; After = after; Time = time; }
    }

    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maximum = 100f;
        [SerializeField] private bool regenerate;
        [SerializeField] private float recoveryDelay = 3f;
        [SerializeField] private float recoveryPerSecond = 8f;
        [SerializeField, Tooltip("Log accepted damage and fatal hits in the Editor or Development Build only.")]
        private bool logDamageDiagnostics;

        private float current;
        private float lastHitTime = float.NegativeInfinity;

        public float Current => current;
        public float Maximum => maximum;
        public bool IsAlive => current > 0f;
        public DamageRecord LastDamage { get; private set; }
        public event System.Action<Health> Died;
        public event System.Action<DamageRecord> Damaged;

        public void Configure(float max, bool playerRecovery)
        {
            maximum = Mathf.Max(1f, max);
            regenerate = playerRecovery;
            current = maximum;
            lastHitTime = float.NegativeInfinity;
            LastDamage = default;
        }

        private void Awake() => current = maximum;

        private void Update()
        {
            if (regenerate && IsAlive && Time.time - lastHitTime >= recoveryDelay)
                current = Mathf.Min(maximum, current + recoveryPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount) => ApplyDamage(amount, null, "Unattributed scripted damage");

        public void ApplyDamage(float amount, Object attacker, string reason)
        {
            if (!IsAlive || amount <= 0f) return;
            float before = current;
            current = Mathf.Max(0f, current - amount);
            lastHitTime = Time.time;
            LastDamage = new DamageRecord(attacker, reason, amount, before, current, Time.time);
            LogDamage();
            if (!IsAlive) Died?.Invoke(this);
            Damaged?.Invoke(LastDamage);
        }

        private void LogDamage()
        {
            if (!Debug.isDebugBuild || !logDamageDiagnostics) return;
            var hit = LastDamage;
            string source = hit.Attacker != null ? $"{hit.Attacker.name}#{hit.Attacker.GetEntityId()}" : "<unattributed>";
            Debug.Log($"[Damage] target={name}#{GetEntityId()} attacker={source} damage={hit.Amount:0.###} " +
                $"HP={hit.Before:0.###}->{hit.After:0.###} time={hit.Time:0.000} frame={UnityEngine.Time.frameCount} " +
                $"reason={hit.Reason} death={(hit.Fatal ? hit.Reason : "none")}", this);
        }
    }
}
