using UnityEditor;
using UnityEngine;

/// <summary>Keep the small owned effects ready in memory, without compressed first-shot decoding.</summary>
public sealed class GameplayAudioImportSettings : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Resources/Audio/") || !assetPath.EndsWith(".wav")) return;
        var audio = (AudioImporter)assetImporter;
        var settings = audio.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = true;
        audio.defaultSampleSettings = settings;
        audio.forceToMono = false; // Source WAV files are already mono; retain their authored headroom.
        audio.loadInBackground = false;
    }

    [MenuItem("Tools/Shift Four/Reimport Owned Gameplay Audio")]
    public static void ReimportOwnedAudio()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
    }
}
