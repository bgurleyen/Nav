using Helpers.ReorderableList;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WindTableScriptableObject))]
public class WindTableCustomEditor : Editor
{
    ReorderableList windInfoItems;

    void OnEnable()
    {
        windInfoItems = new ReorderableList(serializedObject.FindProperty("WindInfoItems"))
        {
            draggable = false
        };
    }

    public override void OnInspectorGUI()
    {
        windInfoItems?.DoLayoutList();
        
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

