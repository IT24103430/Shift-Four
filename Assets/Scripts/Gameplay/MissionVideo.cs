using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace ShiftFour
{
    /// <summary>Full-screen, unscaled video playback shared by the intro and ending.</summary>
    public sealed class MissionVideo : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        private VideoPlayer video;
        private Camera gameplayCamera;
        private Camera backdrop;
        private bool cameraWasEnabled;
        private Action onFinished;
        private float prepareStarted;
        private GUIStyle skipStyle;

        public void Play(string fileName, Camera camera, Action finished)
        {
            if (IsPlaying) return;
            gameplayCamera = camera;
            cameraWasEnabled = camera != null && camera.enabled;
            onFinished = finished;
            IsPlaying = true;
            prepareStarted = Time.realtimeSinceStartup;
            if (camera != null)
            {
                var background = new GameObject("Video backdrop camera");
                background.transform.SetParent(transform, false);
                backdrop = background.AddComponent<Camera>();
                backdrop.CopyFrom(camera);
                backdrop.cullingMask = 0;
                backdrop.clearFlags = CameraClearFlags.SolidColor;
                backdrop.backgroundColor = Color.black;
                camera.enabled = false;
            }
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.source = VideoSource.Url;
            video.url = Path.Combine(Application.streamingAssetsPath, "Videos", fileName);
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.Direct;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            video.isLooping = false;
            video.waitForFirstFrame = true;
            video.prepareCompleted += OnPrepared;
            video.loopPointReached += OnEnded;
            video.errorReceived += OnError;
            video.Prepare();
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!IsPlaying) return;
            for (ushort track = 0; track < source.audioTrackCount; track++)
                source.SetDirectAudioVolume(track, 1f);
            source.Play();
        }
        private void OnEnded(VideoPlayer source) => Finish();
        private void OnError(VideoPlayer source, string error)
        {
            Debug.LogWarning("Mission video unavailable: " + error);
            Finish();
        }
        private void Update()
        {
            if (!IsPlaying) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)) Finish();
            else if (video != null && !video.isPrepared && Time.realtimeSinceStartup - prepareStarted > 15f)
            {
                Debug.LogWarning("Mission video preparation timed out.");
                Finish();
            }
        }
        public void Finish()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            ReleasePlayback();
            var callback = onFinished;
            onFinished = null;
            callback?.Invoke();
        }
        private void ReleasePlayback()
        {
            if (video != null)
            {
                video.prepareCompleted -= OnPrepared;
                video.loopPointReached -= OnEnded;
                video.errorReceived -= OnError;
                video.Stop();
                Destroy(video);
                video = null;
            }
            if (gameplayCamera != null) gameplayCamera.enabled = cameraWasEnabled;
            if (backdrop != null) Destroy(backdrop.gameObject);
        }
        private void OnDestroy()
        {
            onFinished = null;
            ReleasePlayback();
        }
        private void OnGUI()
        {
            if (!IsPlaying) return;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;
            int oldDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -100;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (video != null && video.texture != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), video.texture, ScaleMode.ScaleToFit);
            skipStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16 };
            GUI.Label(new Rect(Screen.width - 270, Screen.height - 42, 246, 28), "SPACE / ESC  Skip", skipStyle);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }
    }
}
