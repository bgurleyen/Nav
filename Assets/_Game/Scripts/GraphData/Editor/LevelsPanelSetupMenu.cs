#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class LevelsPanelSetupMenu
{
    const string PanelName = "LevelsPanel";

    [MenuItem("Game/Levels/Setup Panel In Active Scene")]
    static void SetupPanelInScene()
    {
        GameObject panelGo = FindLevelsPanelObject();
        if (panelGo == null)
        {
            EditorUtility.DisplayDialog(
                "Levels Setup",
                "LevelsPanel not found in the active scene.\nCreate an empty UI object named \"LevelsPanel\" under Graph UI, then run this again.",
                "OK");
            return;
        }

        Stretch(panelGo.GetComponent<RectTransform>());

        Image image = panelGo.GetComponent<Image>();
        if (image == null)
            image = panelGo.AddComponent<Image>();
        image.color = new Color32(12, 12, 36, 255);
        image.raycastTarget = false;

        LevelsRanksPanel ranksPanel = panelGo.GetComponent<LevelsRanksPanel>();
        if (ranksPanel == null)
            ranksPanel = panelGo.AddComponent<LevelsRanksPanel>();

        ranksPanel.ClearGeneratedLayout();
        ranksPanel.EditorBuildLayout();

        DebriefLayoutScaler scaler = panelGo.GetComponent<DebriefLayoutScaler>();
        if (scaler == null)
            scaler = panelGo.AddComponent<DebriefLayoutScaler>();

        Transform layoutRoot = panelGo.transform.Find("Layout Root");
        if (layoutRoot != null)
            scaler.SetLayoutRoot(layoutRoot as RectTransform);

        EditorSceneManager.MarkSceneDirty(panelGo.scene);
        Selection.activeGameObject = panelGo;

        EditorUtility.DisplayDialog(
            "Levels Setup",
            "Levels rank matrix built inside LevelsPanel.",
            "OK");
    }

    static GameObject FindLevelsPanelObject()
    {
        GameObject[] roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == PanelName)
                    return transforms[j].gameObject;
            }
        }

        return null;
    }

    static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
    }
}
#endif
