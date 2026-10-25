using UnityEngine;

namespace ShiftFour
{
    /// <summary>Plays the supplied final movie after the player plants the bomb and extracts.</summary>
    [DisallowMultipleComponent]
    public sealed class EndingCinematic : MonoBehaviour
    {
        private MissionVideo movie;
        public bool IsPlaying => movie != null && movie.IsPlaying;

        public void Begin(GameSession owner, PlayerController player)
        {
            if (IsPlaying || owner == null || player == null) return;
            movie = gameObject.AddComponent<MissionVideo>();
            movie.Play("ShiftFour_Final_Ending.mp4", player.ViewCamera, null);
        }

        public void Finish()
        {
            if (movie != null) movie.Finish();
        }
    }
}
