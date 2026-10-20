using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CombatHudVerification
{
    public static void BuildDesktop()
    {
        try
        {
            Directory.CreateDirectory("Builds/CombatHud");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainLab.unity" },
                locationPathName = "Builds/CombatHud/ShiftFour.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory("Logs/CombatHud");
            File.WriteAllText("Logs/CombatHud/build-result.txt", report.summary.result + "\n" + report.summary.totalErrors +
                " errors\n" + report.summary.totalWarnings + " warnings\n" + report.summary.totalTime);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
