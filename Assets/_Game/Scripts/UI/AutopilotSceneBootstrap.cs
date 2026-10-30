using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry for the dedicated "Autopilot" scene. Loads Main under <see cref="AutopilotMode"/>.
/// </summary>
public class AutopilotSceneBootstrap : MonoBehaviour
{
    public const string SceneName = AutopilotMode.SceneName;

    void Awake()
    {
        AutopilotMode.BeginSession();
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
        SceneManager.LoadScene(AutopilotMode.MainSceneName);
    }

    void OnDestroy()
    {
        if (AutopilotMode.IsActive)
            AutopilotMode.EndSession();
    }
}
