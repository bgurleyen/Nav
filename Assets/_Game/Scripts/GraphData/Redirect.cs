using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Redirect : MonoBehaviour
{
    private void Awake()
    {
        SceneManager.LoadScene("Main");
    }
}
