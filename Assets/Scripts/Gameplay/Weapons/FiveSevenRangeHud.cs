using UnityEngine;

namespace ShiftFour
{
    // Used only in the isolated range scene; MainLab's HUD stays untouched.
    public sealed class FiveSevenRangeHud : MonoBehaviour
    {
        [SerializeField] private Health target;
        private void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 500, 76), "FN Five-seveN / isolated weapon range\nWASD move  •  Mouse look  •  Hold click fire  •  Space jump\nEsc releases cursor; click to resume");
            if (target != null) GUI.Label(new Rect(24, 100, 300, 24), $"Target health: {target.Current:0} / {target.Maximum:0}");
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(Screen.width * .5f - 5, Screen.height * .5f, 11, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width * .5f, Screen.height * .5f - 5, 1, 11), Texture2D.whiteTexture);
        }
    }
}
