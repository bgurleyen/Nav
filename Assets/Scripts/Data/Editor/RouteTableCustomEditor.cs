using Helpers.ReorderableList;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RouteScriptableObject))]
public class RouteTableCustomEditor : Editor
{
    ReorderableList routePoints;

    void OnEnable()
    {
        routePoints = new ReorderableList(serializedObject.FindProperty("Points"))
        {
            draggable = false
        };
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_gameConfig"));
        EditorGUILayout.Space();
        routePoints?.DoLayoutList();
        
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomPropertyDrawer(typeof(RoutePoint))]
public class RoutePointPropertyDrawer : PropertyDrawer
{
    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        EditorTools.DrawFields(property, position,
            new[] {"Name", "RawDegrees", "Distance", "RawSpeed", "RawAltitude", "Details"},//, "ID"},
            new[] {"Name", "Degrees", "Distance", "Speed", "Altitude", "Details"});//, "ID"});
        EditorGUI.EndProperty();
    }
}


