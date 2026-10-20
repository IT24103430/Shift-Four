using UnityEngine;

namespace ShiftFour
{
    [CreateAssetMenu(menuName = "Shift Four/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Baseline Hitscan";
        [SerializeField, Min(.01f)] private float damage = 25f;
        [SerializeField, Min(.1f)] private float range = 30f;
        [SerializeField, Min(.01f)] private float fireInterval = .24f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Baseline Hitscan" : displayName;
        public float Damage => Positive(damage, 25f, .01f);
        public float Range => Positive(range, 30f, .1f);
        public float FireInterval => Positive(fireInterval, .24f, .01f);

        private void OnValidate()
        {
            displayName = DisplayName; damage = Damage; range = Range; fireInterval = FireInterval;
        }

        private static float Positive(float value, float fallback, float minimum)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);

        internal static WeaponDefinition CreateFallback()
        {
            var definition = CreateInstance<WeaponDefinition>();
            definition.name = "Baseline Hitscan (Runtime)";
            definition.hideFlags = HideFlags.HideAndDontSave;
            return definition;
        }
    }
}
