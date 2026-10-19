using UnityEngine;

namespace ShiftFour
{
    [CreateAssetMenu(fileName = "DroneDifficultyProfile", menuName = "Shift Four/Drone Difficulty Profile")]
    public sealed class DroneDifficultyProfile : ScriptableObject
    {
        [SerializeField] private DroneDifficultySettings settings = DroneDifficultySettings.Default;

        public DroneDifficultySettings Settings => settings.Validated();

        private void OnValidate() => settings = settings.Validated();

        public static DroneDifficultyProfile LoadDefault(DroneArchetype archetype)
        {
            switch (archetype)
            {
                case DroneArchetype.Scout:
                case DroneArchetype.Guard:
                case DroneArchetype.Interceptor:
                case DroneArchetype.Flanker:
                    return Resources.Load<DroneDifficultyProfile>("DroneDifficulty/" + archetype);
                default: return null;
            }
        }
    }
}
