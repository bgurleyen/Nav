using System.IO;
using UnityEditor;
using UnityEngine;

public static class FirebaseEditorMenu
{
    const string AndroidPackage = "com.DefaultCompany.Navigation";

    [MenuItem("Game/Firestore/Apply google-services.json...")]
    static void ApplyGoogleServicesJson()
    {
        string path = EditorUtility.OpenFilePanel(
            "Select google-services.json",
            "",
            "json");

        if (string.IsNullOrEmpty(path))
            return;

        ApplyConfigFiles(path, null);
    }

    [MenuItem("Game/Firestore/Apply Android + iOS Firebase Config...")]
    static void ApplyAndroidAndIosConfig()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select google-services.json (Android)",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        string plistPath = EditorUtility.OpenFilePanel(
            "Select GoogleService-Info.plist (iOS)",
            "",
            "plist");

        ApplyConfigFiles(jsonPath, string.IsNullOrEmpty(plistPath) ? null : plistPath);
    }

    [MenuItem("Game/Firestore/Show Current Firebase Project")]
    static void ShowCurrentProject()
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "google-services.json");
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog(
                "Firebase Config",
                "google-services.json not found in StreamingAssets.",
                "OK");
            return;
        }

        string projectId = ExtractProjectId(File.ReadAllText(jsonPath));
        EditorUtility.DisplayDialog(
            "Firebase Config",
            $"Project id: {projectId}\nAndroid package: {AndroidPackage}\n\nConsole:\nhttps://console.firebase.google.com/project/{projectId}",
            "OK");
    }

    [MenuItem("Game/Firestore/List All Records (Play Mode)")]
    static void ListAllRecords()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Firestore Dump",
                "Enter Play mode first, wait for \"Firestore Init\" in Console, then run this menu again.",
                "OK");
            return;
        }

        FirestoreController controller = Object.FindObjectOfType<FirestoreController>();
        if (controller == null)
        {
            EditorUtility.DisplayDialog(
                "Firestore Dump",
                "FirestoreController not found in the active scene.",
                "OK");
            return;
        }

        controller.DebugListAllRecords();
        Debug.Log("[FirestoreDump] Request sent. Watch Console for collection output.");
    }

    static void ApplyConfigFiles(string jsonSourcePath, string plistSourcePath)
    {
        string destJson = Path.Combine(Application.streamingAssetsPath, "google-services.json");
        string destDesktop = Path.Combine(Application.streamingAssetsPath, "google-services-desktop.json");
        string destPlist = Path.Combine(Application.dataPath, "GoogleService-Info.plist");
        string androidXmlDir = Path.Combine(
            Application.dataPath,
            "Plugins/Android/FirebaseApp.androidlib/res/values");
        string androidXml = Path.Combine(androidXmlDir, "google-services.xml");

        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.Copy(jsonSourcePath, destJson, true);

        string generatePy = Path.Combine(
            Application.dataPath,
            "Firebase/Editor/generate_xml_from_google_services_json.py");

        if (!string.IsNullOrEmpty(plistSourcePath) && File.Exists(plistSourcePath))
        {
            File.Copy(plistSourcePath, destPlist, true);
            RunPython(generatePy, $"-i \"{destPlist}\" -o \"{destDesktop}\"");
        }
        else if (File.Exists(generatePy))
        {
            Directory.CreateDirectory(androidXmlDir);
            RunPython(generatePy, $"-i \"{destJson}\" -o \"{destDesktop}\" -a \"{androidXml}\"");
        }

        AssetDatabase.Refresh();

        string projectId = ExtractProjectId(File.ReadAllText(destJson));
        EditorUtility.DisplayDialog(
            "Firebase Config Applied",
            $"Project: {projectId}\n\nUpdated:\n- StreamingAssets/google-services.json\n- StreamingAssets/google-services-desktop.json\n- Plugins/Android/.../google-services.xml"
            + (File.Exists(destPlist) ? "\n- GoogleService-Info.plist" : ""),
            "OK");
    }

    static void RunPython(string scriptPath, string args)
    {
        if (!File.Exists(scriptPath))
            return;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\" {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        process?.WaitForExit(15000);
    }

    static string ExtractProjectId(string json)
    {
        const string marker = "\"project_id\"";
        int index = json.IndexOf(marker, System.StringComparison.Ordinal);
        if (index < 0)
            return "(unknown)";

        int colon = json.IndexOf(':', index);
        int q1 = json.IndexOf('"', colon + 1);
        int q2 = json.IndexOf('"', q1 + 1);
        if (q1 < 0 || q2 < 0)
            return "(unknown)";

        return json.Substring(q1 + 1, q2 - q1 - 1);
    }
}
