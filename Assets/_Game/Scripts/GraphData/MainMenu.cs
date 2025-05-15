using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string sceneName = "Main";
    [SerializeField] private GameObject agreePanel;
    //private AsyncOperation asyncLoad;

    private void Awake()
    {
        if(PlayerPrefsHolder.Agree == 0)
        {
            agreePanel.SetActive(true);    
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
        if(isNewGame) PlayerPrefsHolder.Level = 0;

        //if (asyncLoad != null)
        //{
        //    asyncLoad.allowSceneActivation = true;
        //}
            SceneManager.LoadScene(sceneName);
    }

    public void OnAgreeButtonClick()
    {
        PlayerPrefsHolder.Agree = 1;
    }
}
