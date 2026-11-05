using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CiBuild
{
    public static void BuildiOS()
    {
        BuildPlayer(BuildTarget.iOS, Path.Combine("Build", "iOS"), CollectEnabledScenes());
    }

    public static void BuildAndroid()
    {
        var buildNumber = System.Environment.GetEnvironmentVariable("ANDROID_BUILD_NUMBER");
        if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out var versionCode))
        {
            PlayerSettings.Android.bundleVersionCode = versionCode;
        }

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        BuildPlayer(
            BuildTarget.Android,
            Path.Combine("Build", "Android", "DescentTutor.aab"),
            CollectEnabledScenes());
    }

    static string[] CollectEnabledScenes()
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

        return sceneList;
    }

    static void BuildPlayer(BuildTarget target, string outputRelativePath, string[] scenes)
    {
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), outputRelativePath);
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        Debug.Log($"[{target}] Building with scenes: {string.Join(", ", scenes)}");

        var report = BuildPipeline.BuildPlayer(scenes, outputPath, target, BuildOptions.None);

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"{target} build failed: {report.summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"{target} build succeeded: {outputPath}");
        EditorApplication.Exit(0);
    }
}
