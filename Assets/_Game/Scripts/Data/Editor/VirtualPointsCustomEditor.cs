using Helpers.ReorderableList;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VirtualPointsScriptableObject))]
public class VirtualPointsCustomEditor : Editor
{
    ReorderableList virtualPointItems;

    void OnEnable()
    {
        virtualPointItems = new ReorderableList(serializedObject.FindProperty("VirtualPointsItems"))
        {
            draggable = false
        };
    }

    public override void OnInspectorGUI()
    {
        virtualPointItems?.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }
}


[CustomPropertyDrawer(typeof(VirtualPoints))]
public class VirtualPointsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EditorTools.DrawFields(property, position, 
            new[] {"Number", "x", "y"});

        EditorGUI.EndProperty();
    }
}

