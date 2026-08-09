using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry for the dedicated "Score Test" scene. Loads Main, then opens the random-score dialog.
/// Normal play (Main / EMPTY) never touches this — open the Score Test scene only when testing.
/// </summary>
public class ScoreTestSceneBootstrap : MonoBehaviour
{
    public const string SceneName = "Score Test";
    const string MainSceneName = "Main";

    static bool _pending;

    /// <summary>
    /// Calculator calls this so normal level-start UI is skipped while a score test is driving Main.
    /// </summary>
    public static bool ConsumePending()
    {
        if (!_pending)
            return false;

        _pending = false;
        return true;
    }

    void Awake()
    {
        _pending = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
        SceneManager.LoadScene(MainSceneName);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainSceneName)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(ShowDialogNextFrame());
    }

    IEnumerator ShowDialogNextFrame()
    {
        yield return null;
        ScoreTestDialog.ShowForTestScene();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
