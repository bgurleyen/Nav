using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CiBuild
{
    public static void BuildiOS()
    {
        var buildNumber = System.Environment.GetEnvironmentVariable("IOS_BUILD_NUMBER");
        if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out var version))
        {
            PlayerSettings.iOS.buildNumber = buildNumber;
        }

        BuildPlayer(BuildTarget.iOS, Path.Combine("Build", "iOS"), CollectEnabledScenes());
    }

    public static void BuildAndroid()
    {
        var buildNumber = System.Environment.GetEnvironmentVariable("ANDROID_BUILD_NUMBER");
        if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out var versionCode))
        {
            PlayerSettings.Android.bundleVersionCode = versionCode;
        }

        if (!ApplyAndroidSigning())
        {
            EditorApplication.Exit(1);
            return;
        }

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        BuildPlayer(
            BuildTarget.Android,
            Path.Combine("Build", "Android", "DescentTutor.aab"),
            CollectEnabledScenes());
    }

    static bool ApplyAndroidSigning()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        var keystorePath = Path.Combine(projectRoot, "AndroidSigning", "descent-tutor.keystore");
        if (!File.Exists(keystorePath))
        {
            Debug.LogError("Android keystore missing: " + keystorePath);
            return false;
        }

        var password = System.Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
        if (string.IsNullOrEmpty(password))
        {
            var passwordFile = Path.Combine(projectRoot, "AndroidSigning", "KEYSTORE_PASSWORD.txt");
            if (File.Exists(passwordFile))
            {
                password = File.ReadAllText(passwordFile).Trim();
            }
        }

        if (string.IsNullOrEmpty(password))
        {
            Debug.LogError("Set ANDROID_KEYSTORE_PASS or create AndroidSigning/KEYSTORE_PASSWORD.txt");
            return false;
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keyaliasName = "upload";
        PlayerSettings.Android.keystorePass = password;
        PlayerSettings.Android.keyaliasPass = password;
        return true;
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
