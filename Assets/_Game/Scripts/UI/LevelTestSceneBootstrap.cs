using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry for the dedicated "Level Test" scene. Loads Main under <see cref="LevelTestMode"/>
/// without affecting normal play (Main / EMPTY / UI - Test).
/// </summary>
public class LevelTestSceneBootstrap : MonoBehaviour
{
    public const string SceneName = LevelTestMode.SceneName;

    void Awake()
    {
        LevelTestMode.BeginSession();
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
        SceneManager.LoadScene(LevelTestMode.MainSceneName);
    }

    void OnDestroy()
    {
        if (LevelTestMode.IsActive)
            LevelTestMode.EndSession();
    }
}
