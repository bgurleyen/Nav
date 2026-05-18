using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName = "Main";
    [SerializeField] private GameObject agreePanel;
    [SerializeField] private GameObject namePanel;
    [SerializeField] private GameObject howToPlayPanel;
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


        SceneManager.LoadScene(sceneName);
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
