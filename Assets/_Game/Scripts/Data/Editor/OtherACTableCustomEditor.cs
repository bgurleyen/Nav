using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OtherACScriptableObject))]
public class OtherACTableCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ACItems"));
        
        serializedObject.ApplyModifiedProperties();
    }
}


[CustomPropertyDrawer(typeof(ACInfo))]
public class ACInfoDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EditorTools.DrawFields(property, position, 
            new[] {"Point", "Altitude", "Speed"});

        EditorGUI.EndProperty();
    }
}

