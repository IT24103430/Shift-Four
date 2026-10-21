using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit, repeatable scale/clearance migration. Does not rebuild or bake the level.</summary>
public static class DronePresentationInstaller
{
    [MenuItem("Tools/Shift Four/Apply Compact Drone Presentation")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before applying drone presentation.");
        string[] paths = { "Assets/Prefabs/Agents/Scout.prefab", "Assets/Prefabs/Agents/Guard.prefab",
            "Assets/Prefabs/Agents/Interceptor.prefab", "Assets/Prefabs/Agents/Flanker.prefab", "Assets/Scenes/MainLab.unity" };
        string backup = "Library/DronePresentationBackup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        foreach (string path in paths)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup + "/" + path));
            File.Copy(path, backup + "/" + path);
        }
        foreach (string path in paths.Take(4))
        {
            string before = File.ReadAllText(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new IOException(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ShiftFourVisualUpgrade.PreserveGameplayYaml(before, path);
        }
        string sceneBefore = File.ReadAllText(paths[4]);
        var scene = EditorSceneManager.OpenScene(paths[4]);
        foreach (var brain in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DroneBrain>(true)))
            Configure(brain.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("MainLab save failed");
        ShiftFourVisualUpgrade.PreserveGameplayYaml(sceneBefore, paths[4]);
        AssetDatabase.SaveAssets();
        // Save/import can normalize unknown teammate fields. Restore protected blocks only
        // after the final save, using the pre-migration bytes for every authored file.
        foreach (string path in paths)
        {
            ShiftFourVisualUpgrade.PreserveGameplayYaml(File.ReadAllText(backup + "/" + path), path, reimport: false);
            PreserveAnimationYaml(File.ReadAllText(backup + "/" + path), path);
        }
        Debug.Log("COMPACT_DRONES_APPLIED: scale=0.50; clearance remeasured; backup=" + backup);
    }

    private static void PreserveAnimationYaml(string before, string path)
    {
        // This migration never edits animation tuning. Avoid serializing newly introduced
        // defaults into teammate-authored components when Unity saves their containers.
        string after = File.ReadAllText(path);
        foreach (string block in Regex.Split(before, @"(?=^--- !u!)", RegexOptions.Multiline))
        {
            var script = Regex.Match(block, @"m_Script:.*guid: (\w+)");
            if (!script.Success || !AssetDatabase.GUIDToAssetPath(script.Groups[1].Value).EndsWith("DroneRotorVisuals.cs")) continue;
            string pattern = Regex.Escape(block.Split('\n')[0]) + @"\n.*?(?=^--- !u!|\z)";
            after = Regex.Replace(after, pattern, _ => block, RegexOptions.Multiline | RegexOptions.Singleline);
        }
        File.WriteAllText(path, after);
    }

    private static void Configure(GameObject root)
    {
        var visual = root.GetComponentsInChildren<DroneRotorVisuals>(true).Single().transform;
        visual.localScale = Vector3.one * .5f;
        EditorUtility.SetDirty(visual);
        if (PrefabUtility.IsPartOfPrefabInstance(visual)) PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        // Reuses the measured mesh hitboxes, full rotor sweep and native avoidance fit.
        ShiftFourProximityFix.ConfigureDrone(root);
    }
}
