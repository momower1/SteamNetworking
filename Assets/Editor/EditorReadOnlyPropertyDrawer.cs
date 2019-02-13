using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SteamNetworking
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class EditorReadOnlyPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            string valueString;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    valueString = property.intValue.ToString();
                    break;
                case SerializedPropertyType.Boolean:
                    valueString = property.boolValue.ToString();
                    break;
                case SerializedPropertyType.Float:
                    valueString = property.floatValue.ToString("0.00000");
                    break;
                case SerializedPropertyType.String:
                    valueString = property.stringValue;
                    break;
                default:
                    valueString = "(not supported)";
                    break;
            }

            EditorGUI.LabelField(position, label.text, valueString);
        }
    }
}
