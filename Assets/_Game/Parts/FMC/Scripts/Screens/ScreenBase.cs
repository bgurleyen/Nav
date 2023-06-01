using System;
using UnityEngine;

public class ScreenBase : MonoBehaviour
{
    protected static MainScreen Main => MainScreen.Instance;
    protected FMC_Screens FMSScreens { get; private set; }

    protected virtual void Awake()
    {
        FMSScreens = GetComponentInParent<FMC_Screens>();
    }

    public virtual void Show()
    {
       transform.localScale= Vector3.one;
    }

    public virtual void Hide()
    {
       transform.localScale= Vector3.zero;
    }

    public virtual void DisplayNextPage() { }

    public virtual void DisplayPrevPage() { }

    public virtual void OnLineSelectRight(int index) { }

    public virtual void OnLineSelectLeft(int index) { }

    public virtual void OnExecPress() { }

    public virtual void OnRightCornerPress() { }

    public virtual void OnLeftCornerPress() { }

    public virtual void OnDecimalPressed() { OnCharacterInput('.');}

    public virtual void OnSlashPressed() { OnCharacterInput('/');}

    public virtual void OnSignPressed() { OnCharacterInput('-');}

    public virtual void OnNumberPressed(int number) { OnCharacterInput((char) (number + 48));}
    
    public virtual void OnDeletePress() { }
    
    public virtual void OnClearPress() { }

    public virtual void OnCharacterInput(char character) { }
}
