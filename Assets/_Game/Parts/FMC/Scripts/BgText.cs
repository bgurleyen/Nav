using System;
using System.Collections;
using System.Collections.Generic;
using Navigation;
using TMPro;
using UnityEngine;

public class BgText : MonoBehaviour
{
    [SerializeField] private Color magentaColor;
    [SerializeField] private Color modifiedBackground;
    [SerializeField] private Color defaultColor;
    
    [SerializeField] internal TMP_Text label;

    private string m_text;

    public void Clear()
    {
        label.text = "";
    }

    public void SetAsTall(string text)
    {
        SetText(false, null, new TextBuilder{ State = TextState.TallText, Text = text});
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
            var part = parts[i];
            switch (part.State)
            {
                case TextState.TallText:
                    label.text += part.Text;
                    break;
                case TextState.ModSelection:
                    label.text += $"<mark=#{ColorUtility.ToHtmlStringRGBA(modifiedBackground)}>{part.Text}</mark>";
                    break;
                case TextState.Magenta:
                    if (Session.IsMod)
                    {
                        // don't show anything magenta in mod
                        label.text += part.Text;
                    }
                    else
                    {
                        label.text += $"<color=#{ColorUtility.ToHtmlStringRGB(magentaColor)}>{part.Text}</color>";
                    }

                    break;
                case TextState.Default:
                case TextState.SmallText:
                    label.text += $"<size=35>{part.Text}</size>";
                    break;
            }
            m_text = part.Text;
        }
        //<size=46><font="HelveticaNeue-Bold SDF"><line-height=49> </line-height></font></size>
    }

    public string GetText() => m_text;

    // Currently displayed text with rich-text tags stripped (empty when cleared).
    public string GetCurrentText()
    {
        if (label == null || string.IsNullOrEmpty(label.text))
            return string.Empty;

        return label.GetParsedText();
    }

    public struct TextBuilder
    {
        public TextState State;
        public string Text;
    }
    
    public enum TextState
    {
        Default, 
        ModSelection, 
        Magenta,
        TallText,
        SmallText
    }
}
