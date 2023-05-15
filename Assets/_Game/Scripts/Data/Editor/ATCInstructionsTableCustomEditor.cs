using Helpers.ReorderableList;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ATCInstructionsScriptableObject))]
public class ATCInstructionsTableCustomEditor : Editor
{
    ReorderableList atcItems;

    void OnEnable()
    {
        atcItems = new ReorderableList(serializedObject.FindProperty("ATCInstrucitonItems"))
        {
            draggable = false
        };
    }

    public override void OnInspectorGUI()
    {
        atcItems?.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }
}


[CustomPropertyDrawer(typeof(ATCInstructionInfo))]
public class ATCInstructionInfoDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EditorTools.DrawFields(property, position, 
            new[] {"point", "mode", "Altitude", "VS", "VS_nx", "Speed", "Speed_nx"});
        
        EditorGUI.EndProperty();
    }
}

