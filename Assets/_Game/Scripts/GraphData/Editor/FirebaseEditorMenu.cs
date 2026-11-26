using System;
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

    [MenuItem("Game/Firestore/Deploy firestore.rules (herkes + level)")]
    static void DeployFirestoreRules()
    {
        if (!File.Exists(RulesFilePath))
        {
            EditorUtility.DisplayDialog("Firestore Rules", "firebase/firestore.rules not found.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Deploy Firestore rules",
                "Proje: " + ResolveCurrentProjectId() + "\n\n" +
                "herkes için canlı Firestore kuralları güncellenmeli.\n\nDevam?",
                "Deploy",
                "Cancel"))
            return;

        if (!TryStartRulesDeploy(out string error))
        {
            EditorUtility.DisplayDialog(
                "Firestore Rules",
                "Otomatik deploy başarısız.\n\n" + error +
                "\n\nAlternatif:\n" +
                "1) Game → Firestore → Copy firestore.rules to clipboard\n" +
                "2) Game → Firestore → Open Rules in Firebase Console\n" +
                "3) Yapıştır → Publish",
                "OK");
        }
    }

    [MenuItem("Game/Firestore/Open Rules in Firebase Console")]
    static void OpenRulesInConsole()
    {
        Application.OpenURL(
            $"https://console.firebase.google.com/project/{ResolveCurrentProjectId()}/firestore/rules");
    }

    [MenuItem("Game/Firestore/Copy firestore.rules to clipboard")]
    static void CopyRulesToClipboard()
    {
        if (!File.Exists(RulesFilePath))
        {
            EditorUtility.DisplayDialog("Firestore Rules", "firebase/firestore.rules not found.", "OK");
            return;
        }

        EditorGUIUtility.systemCopyBuffer = File.ReadAllText(RulesFilePath);
        EditorUtility.DisplayDialog(
            "Firestore Rules",
            "firestore.rules panoya kopyalandı.\n\nConsole → Firestore → Rules → Publish.\nSonra Create ile tekrar dene.",
            "OK");
    }

    static string RulesFilePath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "firebase", "firestore.rules"));

    static string RulesWorkingDir =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "firebase"));

    static string ResolveCurrentProjectId()
    {
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "google-services.json");
        if (!File.Exists(jsonPath))
            return "navigationshare-alpha-618e7";

        return ExtractProjectId(File.ReadAllText(jsonPath));
    }

    static bool TryStartRulesDeploy(out string error)
    {
        error = null;
        string[] attempts =
        {
            "firebase deploy --only firestore:rules",
            "npx --yes firebase-tools deploy --only firestore:rules",
        };

        foreach (string args in attempts)
        {
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {args}",
                    WorkingDirectory = RulesWorkingDir,
                    UseShellExecute = true,
                };

                if (System.Diagnostics.Process.Start(start) != null)
                {
                    Debug.Log($"[Firestore] Rules deploy started: {args}");
                    return true;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
            }
        }

        if (string.IsNullOrEmpty(error))
            error = "firebase / npx bulunamadı. Console'dan Publish kullanın.";

        return false;
    }

    [MenuItem("Game/Firestore/Test herkes write (Play Mode)")]
    static void TestHerkesWrite()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("herkes test", "Önce Play mode'a gir.", "OK");
            return;
        }

        FirestoreController.RegisterEveryoneName("HerkesTest_" + DateTime.Now.ToString("HHmmss"));
        Debug.Log("[Firestore] Test herkes write — Console'da sonucu kontrol et.");
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

        FirestoreController controller = UnityEngine.Object.FindObjectOfType<FirestoreController>();
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
