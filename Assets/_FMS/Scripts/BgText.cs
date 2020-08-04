using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BgText : MonoBehaviour
{
    [SerializeField] Color magentaColor;
    [SerializeField] Color modifiedBackground;
    [SerializeField] Color defaultColor;
    
    [SerializeField] TMP_Text label;

    public void Clear()
    {
        label.text = "";
    }
    
    
    public void SetAsModified(string text)
    {
        SetText(false, null, new TextBuilder{ State = TextState.ModSelection, Text = text});
    }

    public void SetAsDefault(string text)
    {
        
        SetText(false, null, new TextBuilder{ State = TextState.Default, Text = text});
    }

    public void SetAsMagenta(string text)
    {
        SetText(false, null, new TextBuilder{ State = TextState.Magenta, Text = text});
    }

    public void SetText(bool monospace, string separator, params TextBuilder[] parts)
    {
        label.text = monospace ? "<mspace=0.52em>" : "";
        for (var i = 0; i < parts.Length; i++)
        {
            if (i != 0 && !string.IsNullOrEmpty(separator))
            {
                label.text += separator;
            }
            var _part = parts[i];
            switch (_part.State)
            {
                case TextState.SmallText:
                    label.text += $"<size=35>{_part.Text}</size>";
                    break;
                case TextState.ModSelection:
                    label.text += $"<mark=#{ColorUtility.ToHtmlStringRGBA(modifiedBackground)}>{_part.Text}</mark>";
                    break;
                case TextState.Magenta:
                    label.text += $"<color=#{ColorUtility.ToHtmlStringRGB(magentaColor)}>{_part.Text}</color>";
                    break;
                case TextState.Default:
                case TextState.TallText:
                    label.text += _part.Text;
                    break;
            }
        }
        //<size=46><font="HelveticaNeue-Bold SDF"><line-height=49> </line-height></font></size>
    }
    
    
    public struct TextBuilder
    {
        public TextState State;
        public string Text;
    }
    
    public enum TextState
    {
        Default, ModSelection, Magenta,
        TallText,
        SmallText
    }
}
