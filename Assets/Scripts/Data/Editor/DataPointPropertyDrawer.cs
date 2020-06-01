using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DataPoint))]
public class DataPointPropertyDrawer : PropertyDrawer
{
    // Draw the property inside the given rect
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), GUIContent.none);

        // Don't make child fields be indented
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        var fieldWidth = position.width / 7;
        float widthCursor = 0;

        // Calculate rects
        
        var NameRect = new Rect(position.x + widthCursor, position.y, fieldWidth, position.height);
        widthCursor += fieldWidth;
        
        var DegreesLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;
        
        var DegreesRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        widthCursor += fieldWidth/2;

        var DistanceLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;

        var DistanceRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        widthCursor += fieldWidth/2;

        var SpeedLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;

        var SpeedRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        widthCursor += fieldWidth/2;

        var AltitudeLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;

        var AltitudeRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        widthCursor += fieldWidth/2;

        var DetailsLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;

        var DetailsRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        widthCursor += fieldWidth/2;
        
        var IDLabelRect = new Rect(position.x + widthCursor+fieldWidth/4, position.y, fieldWidth/4, position.height);
        widthCursor += fieldWidth/2;
        
        var IDRect = new Rect(position.x + widthCursor, position.y, fieldWidth/2, position.height);
        
        
        // Draw fields - passs GUIContent.none to each so they are drawn without labels
        EditorGUI.PropertyField(NameRect, property.FindPropertyRelative("Name"), GUIContent.none);
        EditorGUI.LabelField(DegreesLabelRect,"Deg.");
        EditorGUI.PropertyField(DegreesRect, property.FindPropertyRelative("RawDegrees"), GUIContent.none);
        EditorGUI.LabelField(DistanceLabelRect,"Dist.");
        EditorGUI.PropertyField(DistanceRect, property.FindPropertyRelative("Distance"), GUIContent.none);
        EditorGUI.LabelField(SpeedLabelRect,"Speed.");
        EditorGUI.PropertyField(SpeedRect, property.FindPropertyRelative("RawSpeed"), GUIContent.none);
        EditorGUI.LabelField(AltitudeLabelRect,"Alt.");
        EditorGUI.PropertyField(AltitudeRect, property.FindPropertyRelative("RawAltitude"), GUIContent.none);


        EditorGUI.LabelField(DetailsLabelRect,"Details.");
        EditorGUI.PropertyField(DetailsRect, property.FindPropertyRelative("Details"), GUIContent.none);
        EditorGUI.LabelField(IDLabelRect,"ID");
        EditorGUI.PropertyField(IDRect, property.FindPropertyRelative("ID"), GUIContent.none);

        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }
}
