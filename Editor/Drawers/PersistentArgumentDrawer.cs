namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using SolidUtilities;
    using SolidUtilities.Editor;
    using SolidUtilities.UnityEditorInternals;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Assertions;

    [CustomPropertyDrawer(typeof(PersistentArgument))]
    public class PersistentArgumentDrawer : PropertyDrawer
    {
        private static readonly Dictionary<(SerializedObject serializedObject, string propertyPath), SerializedProperty> _valuePropertyCache = new();

        private SerializedProperty _valueProperty;
        private SerializedProperty _isSerialized;
        private bool _showChoiceButton;
        private GUIStyle _buttonStyle;

        private GUIStyle ButtonStyle => _buttonStyle ??= new GUIStyle(GUI.skin.button) 
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };

        private bool ShouldDrawFoldout => _isSerialized.boolValue && 
            _valueProperty?.propertyType == SerializedPropertyType.Generic;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            FindProperties(property);

            if (!_isSerialized.boolValue || !property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            if (_valueProperty?.isArray == true && _valueProperty.propertyType == SerializedPropertyType.Generic)
            {
                return CalculateArrayHeight(_valueProperty);
            }

            return CalculateGenericHeight();
        }

        public override void OnGUI(Rect fieldRect, SerializedProperty property, GUIContent label)
        {
            FindProperties(property);
            _showChoiceButton = property.FindPropertyRelative(nameof(PersistentArgument._canBeDynamic)).boolValue;

            var (labelRect, buttonRect, valueRect) = GetLabelButtonValueRects(fieldRect);
            DrawLabel(property, fieldRect, labelRect, label);

            using (new EditorIndentLevelScope(0))
            {
                if (_showChoiceButton)
                    DrawChoiceButton(buttonRect, _isSerialized);

                DrawValue(property, valueRect, fieldRect);
            }
        }

        private float CalculateArrayHeight(SerializedProperty arrayProperty)
        {
            if (!arrayProperty.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight * 2; // Foldout + size field
            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                var element = arrayProperty.GetArrayElementAtIndex(i);
                height += EditorGUI.GetPropertyHeight(element, true) + EditorPackageSettings.LinePadding;
            }
            return height;
        }

        private float CalculateGenericHeight()
        {
            float baseHeight = EditorGUI.GetPropertyHeight(_valueProperty, GUIContent.none);
            return _valueProperty.HasCustomPropertyDrawer() ? baseHeight + EditorGUIUtility.singleLineHeight : baseHeight;
        }

        private static SerializedProperty GetValueProperty(SerializedProperty argumentProperty)
        {
            var key = (argumentProperty.serializedObject, argumentProperty.propertyPath);
            var type = PersistentArgumentHelper.GetTypeFromProperty(argumentProperty, nameof(PersistentArgument._targetType));
            Assert.IsNotNull(type);

            if (_valuePropertyCache.TryGetValue(key, out var valueProperty) && valueProperty.GetObjectType() == type)
                return valueProperty;

            _valuePropertyCache.Remove(key);
            return CreateValueProperty(argumentProperty, type, key);
        }

        private static SerializedProperty CreateValueProperty(SerializedProperty argumentProperty, Type type, (SerializedObject, string) key)
        {
            Type soType = ScriptableObjectCache.GetClass(type);
            var so = ScriptableObject.CreateInstance(soType);
            var serializedObject = new SerializedObject(so);
            
            var soValueField = soType.GetField(nameof(DeserializedValueHolder<int>.Value));
            var value = argumentProperty.GetObject<PersistentArgument>().SerializedValue;
            soValueField.SetValue(so, value);
            
            var valueProperty = serializedObject.FindProperty(nameof(DeserializedValueHolder<int>.Value));
            _valuePropertyCache.Add(key, valueProperty);
            return valueProperty;
        }

        private static void SaveValueProperty(SerializedProperty argumentProperty, SerializedProperty valueProperty)
        {
            valueProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            LogHelper.RemoveLogEntriesByMode(LogModes.NoScriptAssetWarning);
            
            var value = valueProperty.GetObject();
            var argument = argumentProperty.GetObject<PersistentArgument>();
            argument.SerializedValue = value;
        }

        private void DrawValue(SerializedProperty property, Rect valueRect, Rect totalRect)
        {
            if (_isSerialized.boolValue)
            {
                DrawSerializedValue(property, valueRect, totalRect);
            }
            else
            {
                DrawDynamicValue(property, valueRect);
            }
        }

        private void DrawDynamicValue(SerializedProperty property, Rect valueRect)
        {
            var indexProp = property.FindPropertyRelative(nameof(PersistentArgument._index));
            var argNames = ExtEventDrawer.CurrentEventInfo?.ArgNames ?? Array.Empty<string>();
            var currentArgName = indexProp.intValue < argNames.Length ? argNames[indexProp.intValue] : "Invalid Index";

            var matchingArgNames = GetMatchingArgNames(
                argNames, 
                ExtEventDrawer.CurrentEventInfo?.ParamTypes ?? Type.EmptyTypes,
                PersistentArgumentHelper.GetTypeFromProperty(property, nameof(PersistentArgument._fromType), nameof(PersistentArgument._targetType))
            );

            using (new EditorGUI.DisabledGroupScope(matchingArgNames.Count == 1))
            {
                if (EditorGUI.DropdownButton(valueRect, GUIContentHelper.Temp(currentArgName), FocusType.Keyboard))
                {
                    ShowArgNameDropdown(matchingArgNames, indexProp);
                }
            }
        }

        private static List<(string name, int index)> GetMatchingArgNames(string[] allArgNames, Type[] argTypes, Type argType)
        {
            Assert.IsNotNull(argType);
            var matchingNames = new List<(string name, int index)>();

            for (int i = 0; i < argTypes.Length; i++)
            {
                if (argTypes[i].IsAssignableFrom(argType))
                {
                    matchingNames.Add((allArgNames[i], i));
                }
            }

            return matchingNames;
        }

        private void ShowArgNameDropdown(List<(string name, int index)> argNames, SerializedProperty indexProp)
        {
            var menu = new GenericMenu();
            foreach (var (name, index) in argNames)
            {
                menu.AddItem(new GUIContent(name), indexProp.intValue == index, 
                    i => SetArgumentIndex(indexProp, (int)i), index);
            }
            menu.ShowAsContext();
        }

        private static void SetArgumentIndex(SerializedProperty indexProp, int index)
        {
            indexProp.intValue = index;
            indexProp.serializedObject.ApplyModifiedProperties();
        }

        private void DrawSerializedValue(SerializedProperty property, Rect valueRect, Rect totalRect)
        {
            if (_valueProperty == null) return;

            _valueProperty.serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            
            DrawValueProperty(property, valueRect, totalRect);
            
            if (EditorGUI.EndChangeCheck())
            {
                SaveValueProperty(property, _valueProperty);
            }
        }

        private void FindProperties(SerializedProperty property)
        {
            _isSerialized = property.FindPropertyRelative(nameof(PersistentArgument._isSerialized));
            _valueProperty = _isSerialized.boolValue ? GetValueProperty(property) : null;
        }

        private void DrawValueProperty(SerializedProperty mainProperty, Rect valueRect, Rect totalRect)
        {
            if (_valueProperty.propertyType == SerializedPropertyType.Generic)
            {
                DrawValueInFoldout(mainProperty, _valueProperty, totalRect);
            }
            else
            {
                EditorGUI.PropertyField(valueRect, _valueProperty, GUIContent.none);
            }
        }

        private void DrawLabel(SerializedProperty property, Rect totalRect, Rect labelRect, GUIContent label)
        {
            if (ShouldDrawFoldout)
            {
                property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
            }
            else
            {
                EditorGUI.HandlePrefixLabel(totalRect, labelRect, label);
            }
        }

        private (Rect label, Rect button, Rect value) GetLabelButtonValueRects(Rect totalRect)
        {
            const float indentWidth = 15f;
            const float valueLeftIndent = 2f;
            const float choiceButtonWidth = 19f;

            totalRect.height = EditorGUIUtility.singleLineHeight;
            totalRect.xMin += EditorGUI.indentLevel * indentWidth;

            (Rect labelAndButtonRect, Rect valueRect) = totalRect.CutVertically(EditorGUIUtility.labelWidth);
            (Rect labelRect, Rect buttonRect) = labelAndButtonRect.CutVertically(_showChoiceButton ? choiceButtonWidth : 0f, fromRightSide: true);

            valueRect.xMin += valueLeftIndent;
            return (labelRect, buttonRect, valueRect);
        }

        private static void DrawValueInFoldout(SerializedProperty mainProperty, SerializedProperty valueProperty, Rect totalRect)
        {
            valueProperty.isExpanded = mainProperty.isExpanded;
            if (!mainProperty.isExpanded) return;

            var shiftedRect = totalRect.ShiftOneLineDown(EditorGUI.indentLevel + 1);

            if (valueProperty.isArray && valueProperty.propertyType == SerializedPropertyType.Generic)
            {
                DrawArrayProperty(shiftedRect, valueProperty);
            }
            else if (valueProperty.HasCustomPropertyDrawer())
            {
                shiftedRect.height = EditorGUI.GetPropertyHeight(valueProperty);
                EditorGUI.PropertyField(shiftedRect, valueProperty, GUIContent.none);
            }
            else
            {
                DrawGenericPropertyFields(valueProperty, ref shiftedRect);
            }
        }

        private static void DrawGenericPropertyFields(SerializedProperty property, ref Rect position)
        {
            SerializedProperty iterator = property.Copy();
            var nextProp = property.Copy();
            nextProp.NextVisible(false);
            
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, nextProp))
            {
                enterChildren = false;
                position.height = EditorGUI.GetPropertyHeight(iterator, true);
                EditorGUI.PropertyField(position, iterator, true);
                position = position.ShiftOneLineDown(lineHeight: position.height);
            }
        }

        private static void DrawArrayProperty(Rect position, SerializedProperty arrayProperty)
        {
            var sizeRect = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
            EditorGUI.PropertyField(sizeRect, arrayProperty.FindPropertyRelative("Array.size"));

            if (!arrayProperty.isExpanded) return;

            var elementRect = new Rect(position)
            {
                y = position.y + EditorGUIUtility.singleLineHeight + EditorPackageSettings.LinePadding,
                height = EditorGUIUtility.singleLineHeight
            };

            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                var element = arrayProperty.GetArrayElementAtIndex(i);
                elementRect.height = EditorGUI.GetPropertyHeight(element, true);
                EditorGUI.PropertyField(elementRect, element, true);
                elementRect.y += elementRect.height + EditorPackageSettings.LinePadding;
            }
        }

        private void DrawChoiceButton(Rect buttonRect, SerializedProperty isSerializedProperty)
        {
            if (GUI.Button(buttonRect, isSerializedProperty.boolValue ? "s" : "d", ButtonStyle))
            {
                isSerializedProperty.boolValue = !isSerializedProperty.boolValue;
            }
        }

        private sealed class EditorIndentLevelScope : IDisposable
        {
            private readonly int _previousIndent;
            
            public EditorIndentLevelScope(int indentLevel)
            {
                _previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = indentLevel;
            }
            
            public void Dispose() => EditorGUI.indentLevel = _previousIndent;
        }
    }
}
