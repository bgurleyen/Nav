using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WindTableScriptableObject))]
public class WindTableCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("WindInfoItems"));
        
        serializedObject.ApplyModifiedProperties();
    }
}


[CustomPropertyDrawer(typeof(WindInfo))]
public class WindInfoDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EditorTools.DrawFields(property, position, 
            new[] {"Altitude", "Degrees", "Knots"});

        EditorGUI.EndProperty();
    }
}

