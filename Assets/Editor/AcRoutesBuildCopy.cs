using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class AcRoutesBuildCopy : IPreprocessBuildWithReport
{
    const string SourceRelative = "Assets/_Game/Data Files/General/ac_routes.txt";
    const string DestRelative = "Assets/Resources/ac_routes.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        CopyIntoResources();
    }

    public static void CopyIntoResources()
    {
        string source = Path.Combine(Directory.GetCurrentDirectory(), SourceRelative);
        string dest = Path.Combine(Directory.GetCurrentDirectory(), DestRelative);
        if (!File.Exists(source))
        {
            UnityEngine.Debug.LogError("[AcRoutesBuildCopy] Missing " + SourceRelative);
            return;
        }

        string destDir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(source, dest, true);
        AssetDatabase.ImportAsset(DestRelative, ImportAssetOptions.ForceUpdate);
    }
}
