using UnityEngine;

namespace ShiftFour
{
    /// <summary>Plays the mission ending inside MainLab using the live scene and camera.</summary>
    [DisallowMultipleComponent]
    public sealed class EndingCinematic : MonoBehaviour
    {
        public const float Duration = 40f;
        private const float FadeSeconds = 1.2f;
        private static readonly string[] Names = { "PAMUDI", "NIMSARA", "NIMTHARA", "ASMADALA" };
        private static readonly string[] StudentIds = { "IT24103460", "IT24103464", "IT24610327", "IT24103430" };
        private static readonly string[] Roles = { "WORLD BUILDER", "SYSTEMS ENGINEER", "CORE DEVELOPER", "AGENT CONTROLLER" };
        private static readonly string[] Details = { "CHECKPOINT  /  SCOUT AI", "STORAGE  /  FLANKER AI",
            "SERVER  /  GUARD AI", "CONTROL  /  INTERCEPTOR AI" };

        private GameSession session;
        private Camera playerCamera;
        private Camera cinematicCamera;
        private Light pulseLight;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float startedAt;
        private bool playing;

        public bool IsPlaying => playing;
        public float Elapsed => playing ? Mathf.Max(0f, Time.unscaledTime - startedAt) : Duration;

        public void Begin(GameSession owner, PlayerController player)
        {
            if (playing || owner == null || player == null || player.ViewCamera == null) return;
            session = owner;
            playerCamera = player.ViewCamera;
            startPosition = playerCamera.transform.position;
            startRotation = playerCamera.transform.rotation;
            var cameraObject = new GameObject("Ending cinematic camera");
            cameraObject.transform.SetParent(transform, false);
            cinematicCamera = cameraObject.AddComponent<Camera>();
            cinematicCamera.CopyFrom(playerCamera);
            cinematicCamera.transform.SetPositionAndRotation(startPosition, startRotation);
            // The gameplay camera stays attached to the player, but only the cinematic camera renders.
            playerCamera.enabled = false;

            var lightObject = new GameObject("Facility shutdown pulse");
            lightObject.transform.SetParent(transform, false);
            var exit = FindAnyObjectByType<ExitZone>();
            lightObject.transform.position = exit != null ? exit.transform.position + Vector3.up * 2f :
                startPosition + startRotation * Vector3.forward * 3f;
            pulseLight = lightObject.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.color = new Color(1f, .34f, .08f);
            pulseLight.range = 18f;
            pulseLight.intensity = 0f;

            startedAt = Time.unscaledTime;
            playing = true;
        }

        private void LateUpdate()
        {
            if (!playing) return;
            float t = Elapsed;
            if (t >= Duration) { Finish(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            { Finish(); return; }

            // A restrained camera drift keeps the actual cleared laboratory in the final shot.
            float drift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 11f));
            cinematicCamera.transform.position = startPosition + startRotation *
                new Vector3(.4f * drift, .15f * drift, -.65f * drift);
            cinematicCamera.transform.rotation = startRotation * Quaternion.Euler(-3f * drift, 5f * drift, 0f);
            float burst = Mathf.Max(0f, 1f - Mathf.Abs(t - 7f) / 1.5f);
            pulseLight.intensity = burst * 12f;
        }

        public void Finish()
        {
            if (!playing) return;
            playing = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (cinematicCamera != null) Destroy(cinematicCamera.gameObject);
            if (pulseLight != null) Destroy(pulseLight.gameObject);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (playing && playerCamera != null) playerCamera.enabled = true;
        }

        private void OnGUI()
        {
            if (!playing || session == null) return;
            float t = Elapsed;
            float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUI.depth = -20;
            try
            {
                float w = Screen.width / scale, h = Screen.height / scale;
                float fade = Mathf.Clamp01(t / FadeSeconds) * Mathf.Clamp01((Duration - t) / FadeSeconds);
                // The opening cut and shutdown pulse are layered over the real lab image.
                float cut = t < 1f ? 1f - t : 0f;
                float flash = Mathf.Max(0f, 1f - Mathf.Abs(t - 7f) / .55f);
                Fill(new Rect(0, 0, w, h), new Color(.01f, .018f, .03f, .50f * fade + cut));
                if (flash > 0f) Fill(new Rect(0, 0, w, h), new Color(1f, .38f, .10f, flash * .8f));

                if (t < 9f)
                {
                    Text(new Rect(0, h * .37f, w, 64), t < 3f ? "MISSION COMPLETE" :
                        t < 6f ? "ALL FOUR SECTORS CLEAR" : "FACILITY SECURED", 36, Color.white);
                    Text(new Rect(0, h * .48f, w, 32), "BOMB DEFUSED  /  EXTRACTION CONFIRMED", 17,
                        new Color(.36f, .91f, .94f));
                }
                else if (t < 36f)
                {
                    Text(new Rect(0, h * .18f, w, 32), "SHIFT-FOUR", 27, Color.white);
                    Text(new Rect(0, h * .26f, w, 26), "DEVELOPED BY GROUP 26", 16,
                        new Color(.36f, .91f, .94f));
                    int index = Mathf.Min(3, Mathf.FloorToInt((t - 9f) / 6.75f));
                    Text(new Rect(0, h * .42f, w, 55), Names[index], 37, Color.white);
                    Text(new Rect(0, h * .50f, w, 30), StudentIds[index], 18,
                        new Color(.36f, .91f, .94f));
                    Text(new Rect(0, h * .56f, w, 30), Roles[index], 16,
                        new Color(.36f, .91f, .94f));
                    Text(new Rect(0, h * .62f, w, 25), Details[index], 13, Color.white);
                }
                else
                {
                    Text(new Rect(0, h * .40f, w, 65), "THANK YOU FOR PLAYING", 35, Color.white);
                    Text(new Rect(0, h * .51f, w, 30), "SHIFT-FOUR  /  GROUP 26", 15,
                        new Color(.36f, .91f, .94f));
                }
                Text(new Rect(0, h - 51f, w, 25), "ESC OR SPACE  /  SKIP", 12,
                    new Color(.75f, .81f, .84f));
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; }
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static void Text(Rect rect, string value, int size, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = size,
                fontStyle = FontStyle.Bold, normal = { textColor = color } };
            GUI.Label(rect, value, style);
        }
    }
}
