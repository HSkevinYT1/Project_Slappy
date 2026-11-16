using UnityEditor;
using UnityEngine;

/// <summary>Muestra SingleLayer como un desplegable de capas en el Inspector.</summary>
[CustomPropertyDrawer(typeof(SingleLayer))]
public class SingleLayerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var index = property.FindPropertyRelative("index");
        EditorGUI.BeginProperty(position, label, property);
        index.intValue = EditorGUI.LayerField(position, label, index.intValue);
        EditorGUI.EndProperty();
    }
}
