using Helpers.ReorderableList;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OtherACScriptableObject))]
public class OtherACTableCustomEditor : Editor
{
    ReorderableList ACInfoItems;

    void OnEnable()
    {
        ACInfoItems = new ReorderableList(serializedObject.FindProperty("ACItems"))
        {
            draggable = false
        };
    }

    public override void OnInspectorGUI()
    {
        ACInfoItems?.DoLayoutList();
        
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

