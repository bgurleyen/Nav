using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyboardButton : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClickPress);
    }

    void OnClickPress()
    {
        var _text = GetComponentInChildren<TMP_Text>().text;

        if (int.TryParse(_text, out var _nr))
        {
            FMC_Screens.Instance.CurrentScreen.OnNumberPressed(_nr);
        }
        else if (_text.Length == 1)
        {
            if (_text == Keys.Decimal)
            {
                FMC_Screens.Instance.CurrentScreen.OnDecimalPressed();
            }
            else if (_text == Keys.Slash)
            {
                FMC_Screens.Instance.CurrentScreen.OnSlashPressed();
            }
            else // it's a letter
            {
                FMC_Screens.Instance.CurrentScreen.OnCharacterInput(_text[0]);
            }
        }
        else
        {
            switch (_text)
            {
                case Keys.Sign:
                    FMC_Screens.Instance.CurrentScreen.OnSignPressed();
                    break;
                case Keys.Del:
                    FMC_Screens.Instance.CurrentScreen.OnDeletePress();
                    break;
                case Keys.Clr:
                    FMC_Screens.Instance.CurrentScreen.OnClearPress();
                    break;
                case Keys.Exec:
                    FMC_Screens.Instance.CurrentScreen.OnExecPress();
                    break;
            }
        }
    }
}

public static class Keys
{
    public const string Exec = "EXEC";
    
    public const string Sign = "+/-";
    public const string Decimal = ".";
    public const string Slash = "/";
    
    public const string Del = "DEL";
    public const string Clr = "CLR";
    public const string Sp = "SP";
    

}
