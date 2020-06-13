using UnityEngine;

public class ScreenBase : MonoBehaviour
{
    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        gameObject.SetActive(false);
    }

    public virtual void DisplayNextPage() { }

    public virtual void DisplayPrevPage() { }

    public virtual void OnLineSelectRight(int index) { }

    public virtual void OnLineSelectLeft(int index) { }

    public virtual void OnExecPress() { }

    public virtual void OnRightCornerPress() { }

    public virtual void OnLeftCornerPress() { }

    public virtual void OnDecimalPressed() { }

    public virtual void OnSlashPressed() { }

    public virtual void OnSignPressed() { }

    public virtual void OnDeletePress() { }
    
    public virtual void OnClearPress() { }

    public virtual void OnNumberPressed(int number) { }

    public virtual void OnCharacterInput(char character) { }
}
