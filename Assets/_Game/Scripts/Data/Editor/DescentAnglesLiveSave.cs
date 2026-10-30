using System;
using Navigation;
using UnityEditor;
using UnityEngine;

// Inspector edits stay in memory until the asset is saved. The angle tool
// reads the asset files, so write dirty level data as soon as the edit ends.
[InitializeOnLoad]
static class DescentAnglesLiveSave
{
    static double _next;

    static DescentAnglesLiveSave()
    {
        EditorApplication.update += Tick;
        EditorApplication.focusChanged += focused =>
        {
            if (!focused)
                SaveAll();
        };
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorGUIUtility.editingTextField)
            return;
        if (EditorApplication.timeSinceStartup < _next)
            return;
        _next = EditorApplication.timeSinceStartup + 0.5;
        SaveAll();
    }

    static void SaveAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        SaveDirty<ATCInstructionsScriptableObject>();
        SaveDirty<RouteScriptableObject>();
        SaveDirty<VirtualPointsScriptableObject>();
        SaveDirty<LevelInfoScriptableObject>();
        SaveDirty<LevelDataScriptableObject>();
    }

    static void SaveDirty<T>() where T : UnityEngine.Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("Data Files", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            T obj = AssetDatabase.LoadAssetAtPath<T>(path);
            if (obj != null && EditorUtility.IsDirty(obj))
                AssetDatabase.SaveAssetIfDirty(obj);
        }
    }
}
