using UnityEditor;
using UnityEngine;

/// <summary>Keep effects ready in memory and stream the longer stereo music track.</summary>
public sealed class GameplayAudioImportSettings : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        bool music = assetPath == "Assets/Resources/Audio/BackgroundMusic.mp3";
        if (!assetPath.StartsWith("Assets/Resources/Audio/") || (!music && !assetPath.EndsWith(".wav"))) return;
        var audio = (AudioImporter)assetImporter;
        var settings = audio.defaultSampleSettings;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
        settings.quality = music ? .7f : 1f;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = !music;
        audio.defaultSampleSettings = settings;
        audio.forceToMono = false; // WAV effects are mono; music retains its stereo image.
        audio.loadInBackground = music;
    }

    [MenuItem("Tools/Shift Four/Reimport Owned Gameplay Audio")]
    public static void ReimportOwnedAudio()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
    }
}
