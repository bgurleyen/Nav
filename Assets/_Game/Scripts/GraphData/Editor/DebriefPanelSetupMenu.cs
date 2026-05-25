#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DebriefPanelSetupMenu
{
    const string PanelName = "Debrief Panel";

    [MenuItem("Game/Debrief/Setup Panel In Active Scene")]
    static void SetupPanelInScene()
    {
        CanvasGroup graphGroup = FindGraphGroup();
        if (graphGroup == null)
        {
            EditorUtility.DisplayDialog(
                "Debrief Setup",
                "Canvas Graph (CanvasGroup) not found in the active scene.",
                "OK");
            return;
        }

        Transform graphUi = graphGroup.transform.Find("Graph UI");
        if (graphUi == null)
            graphUi = graphGroup.transform;

        Transform panelTransform = graphUi.Find(PanelName);
        GameObject panelGo;
        if (panelTransform == null)
        {
            panelGo = new GameObject(PanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(DebriefLayoutScaler));
            panelGo.transform.SetParent(graphUi, false);
            Stretch(panelGo.GetComponent<RectTransform>());

            Image image = panelGo.GetComponent<Image>();
            image.color = new Color32(12, 12, 36, 255);
            image.raycastTarget = false;
        }
        else
        {
            panelGo = panelTransform.gameObject;
        }

        DebriefSummaryPanel summary = panelGo.GetComponent<DebriefSummaryPanel>();
        if (summary == null)
            summary = panelGo.AddComponent<DebriefSummaryPanel>();

        summary.ClearGeneratedLayout();
        summary.EditorBuildLayout();

        DebriefLayoutScaler scaler = panelGo.GetComponent<DebriefLayoutScaler>();
        if (scaler == null)
            scaler = panelGo.AddComponent<DebriefLayoutScaler>();
        Transform layoutRoot = panelGo.transform.Find("Layout Root");
        if (layoutRoot != null)
            scaler.SetLayoutRoot(layoutRoot as RectTransform);

        GraphManage graphManage = Object.FindObjectOfType<GraphManage>();
        if (graphManage != null)
        {
            SerializedObject so = new SerializedObject(graphManage);
            so.FindProperty("debriefPanel").objectReferenceValue = summary;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(graphManage);
        }

        EditorSceneManager.MarkSceneDirty(panelGo.scene);
        Selection.activeGameObject = panelGo;

        EditorUtility.DisplayDialog(
            "Debrief Setup",
            "Debrief Panel created/updated under Canvas Graph.\nGraphManage.debriefPanel linked automatically.",
            "OK");
    }

    static CanvasGroup FindGraphGroup()
    {
        CanvasGroup[] groups = Object.FindObjectsOfType<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].gameObject.name == "Canvas Graph")
                return groups[i];
        }

        return null;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
    }
}
#endif
