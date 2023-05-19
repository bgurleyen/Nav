using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VirtualPointsScriptableObject))]
public class VirtualPointsCustomEditor : Editor
{
 

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("VirtualPointsItems"));
        
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

