using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CiBuild
{
    public static void BuildiOS()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabledScenes = new string[scenes.Length];
        var count = 0;
        foreach (var scene in scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            enabledScenes[count++] = scene.path;
        }

        var sceneList = new string[count];
        for (var i = 0; i < count; i++)
        {
            sceneList[i] = enabledScenes[i];
        }

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "Build", "iOS");
        Directory.CreateDirectory(outputPath);

        var report = BuildPipeline.BuildPlayer(
            sceneList,
            outputPath,
            BuildTarget.iOS,
            BuildOptions.None);

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"iOS build failed: {report.summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"iOS build succeeded: {outputPath}");
        EditorApplication.Exit(0);
    }
}
