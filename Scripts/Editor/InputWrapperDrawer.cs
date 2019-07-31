using UnityEditor;
using UnityEngine;

namespace HatFeather.Shared
{
    [CustomPropertyDrawer(typeof(InputWrapper))]
    internal class InputWrapperDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var prop = property.FindPropertyRelative("inputName");
            EditorGUI.PropertyField(position, prop, label);

            EditorGUI.EndProperty();
        }
    }
}
