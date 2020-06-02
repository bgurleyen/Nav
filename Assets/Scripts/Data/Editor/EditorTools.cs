using UnityEditor;
using UnityEngine;

public static class EditorTools
{
    public static void DrawFieldColumn(SerializedProperty property, string propertyName, string alias, int totalProps, int index, Rect propertyRect, float spacing = 5)
    {
        var _width = propertyRect.width / totalProps - spacing;

        var _labelRect = new Rect(
            propertyRect.x + index * ( _width + spacing),
            propertyRect.y,
            _width / 2, propertyRect.height);
        var _valueRect = new Rect(
            propertyRect.x + index * (_width +spacing) + _width / 2,
            propertyRect.y,
            _width / 2, propertyRect.height);
        EditorGUI.LabelField(_labelRect, alias);
        EditorGUI.PropertyField(_valueRect, property.FindPropertyRelative(propertyName), GUIContent.none);
    }

    public static void DrawFields(SerializedProperty property, Rect position, string[] fields, string[] aliases = null)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            DrawFieldColumn(property, fields[i], GetAlias(fields[i], aliases, i), fields.Length, i, position);
        }
    }


    public static string GetAlias(string fullName, string[] aliases, int index)
    {
        return aliases == null || aliases.Length <= index
            ? fullName
            : aliases[index];
    }
}
