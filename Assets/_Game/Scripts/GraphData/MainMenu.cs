using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName = "Main";
    [SerializeField] private GameObject agreePanel;
    [SerializeField] private GameObject namePanel;
    [SerializeField] private GameObject mainPanel;

    [Header("MainPanel")]
    [SerializeField] private TextMeshProUGUI welcomText;

    [Header("NamePanel")]
    [SerializeField] private TMP_InputField userNameField;
    //private AsyncOperation asyncLoad;
    private int minLength = 3;
    private int maxLength = 20;

    private void Awake()
    {
        EnsureExtraMenuButtons();
        SetupAgreementPrivacyLink();

        if (PlayerPrefsHolder.Agree == 0)
        {
            agreePanel.SetActive(true);
        }
        else
        {
            if (string.IsNullOrEmpty(PlayerPrefsHolder.UserName))
            {
                namePanel.SetActive(true);
            }
            else
            {
                UserNameShowInText();
            }
        }
    }

    void Start()
    {
        //asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        //asyncLoad.allowSceneActivation = false;
    }

    void Update()
    {

    }

    public void OnPlayButtonClick(bool isNewGame)
    {
        if (isNewGame) PlayerPrefsHolder.Level = 0;

        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void OnStartButtonClick()
    {
        PlayerPrefsHolder.ShowLevelSelectOnLoad = true;
        SceneManager.LoadScene(sceneName);
    }

    public void OnContactButtonClick()
    {
        Application.OpenURL("mailto:info@zflite.com");
    }

    public void OnPrivacyButtonClick()
    {
        Application.OpenURL(ZfliteUrls.Privacy);
    }

    void EnsureExtraMenuButtons()
    {
        Button source = null;
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].gameObject.name == "How to play")
            {
                source = buttons[i];
                break;
            }
        }

        if (source == null)
            return;

        EnsureMenuButton(source, "Privacy", ">  Privacy", OnPrivacyButtonClick);
        EnsureMenuButton(source, "Contact us", ">  Contact us", OnContactButtonClick);
    }

    static void EnsureMenuButton(Button template, string objectName, string labelText, UnityAction onClick)
    {
        if (template.transform.parent.Find(objectName) != null)
            return;

        var copy = Object.Instantiate(template.gameObject, template.transform.parent);
        copy.name = objectName;
        copy.transform.SetAsLastSibling();

        var label = copy.GetComponentInChildren<Text>(true);
        if (label != null)
            label.text = labelText;

        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
    }

    void SetupAgreementPrivacyLink()
    {
        if (agreePanel == null)
            return;

        var tmp = agreePanel.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null)
            return;

        tmp.text =
            "Continuing means you understand this. Privacy: " +
            $"<link=\"{ZfliteUrls.Privacy}\"><u>zflite.com/privacy</u></link>";

        if (tmp.GetComponent<TmpLinkOpener>() == null)
            tmp.gameObject.AddComponent<TmpLinkOpener>();
    }

    public void OnAgreeButtonClick()
    {
        PlayerPrefsHolder.Agree = 1;
        agreePanel.SetActive(false);

        if (!string.IsNullOrEmpty(PlayerPrefsHolder.UserName))
        {
            mainPanel.SetActive(true);
        }
        else
        {
            namePanel.SetActive(true);
        }
    }

    public void OnCreateButtonClick()
    {
        if (IsUsernameValid(userNameField.text, out string error))
        {
            Debug.Log("Username is valid: " + userNameField.text);
            PlayerPrefsHolder.UserName = userNameField.text;
            UserNameShowInText();

            namePanel.SetActive(false);
            mainPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Invalid username: " + error);
        }
    }

    public bool IsUsernameValid(string username, out string errorMessage)
    {
        if (username.Length < minLength)
        {
            errorMessage = $"Username must be at least {minLength} characters long.";
            return false;
        }

        if (username.Length > maxLength)
        {
            errorMessage = $"Username must be no more than {maxLength} characters long.";
            return false;
        }

        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            errorMessage = "Username can only contain letters, numbers, and underscores.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private void UserNameShowInText()
    {
        welcomText.text = $"Welcome, {PlayerPrefsHolder.UserName}";
    }
}
