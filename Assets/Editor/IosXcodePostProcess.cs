#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Prevents ITMS-90206: UnityFramework.framework/Frameworks (nested frameworks).
/// Unity-iPhone: embed Swift if needed; UnityFramework: do not embed Swift std libs.
/// </summary>
public static class IosXcodePostProcess
{
    const string AlwaysEmbedSwift = "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES";

    [PostProcessBuild(int.MaxValue)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var pbxProject = new PBXProject();
        pbxProject.ReadFromFile(projectPath);

        var mainTarget = pbxProject.GetUnityMainTargetGuid();
        var frameworkTarget = pbxProject.GetUnityFrameworkTargetGuid();

        pbxProject.SetBuildProperty(mainTarget, AlwaysEmbedSwift, "YES");
        pbxProject.SetBuildProperty(frameworkTarget, AlwaysEmbedSwift, "NO");

        pbxProject.WriteToFile(projectPath);
    }
}
#endif
