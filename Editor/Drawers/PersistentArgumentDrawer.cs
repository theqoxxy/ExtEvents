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
        private static readonly Dictionary<(SerializedObject, string), SerializedProperty> _valuePropertyCache = new();

        private SerializedProperty _valueProperty;
        private SerializedProperty _isSerialized;
        private SerializedProperty _index;
        private bool _canBeDynamic;
        private GUIStyle _choiceButtonStyle;

        private GUIStyle ChoiceButtonStyle => _choiceButtonStyle ??= new GUIStyle(GUI.skin.button) 
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };

        private bool HasFoldout => _isSerialized.boolValue && _valueProperty?.propertyType == SerializedPropertyType.Generic;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init(property);
            if (!_isSerialized.boolValue || !property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            if (_valueProperty?.isArray == true && _valueProperty.propertyType == SerializedPropertyType.Generic)
            {
                if (!_valueProperty.isExpanded)
                    return EditorGUIUtility.singleLineHeight;

                float height = EditorGUIUtility.singleLineHeight * 2;
                for (int i = 0; i < _valueProperty.arraySize; i++)
                    height += EditorGUI.GetPropertyHeight(_valueProperty.GetArrayElementAtIndex(i), true) + EditorPackageSettings.LinePadding;
                return height;
            }

            float baseHeight = EditorGUI.GetPropertyHeight(_valueProperty, GUIContent.none);
            return _valueProperty.HasCustomPropertyDrawer() ? baseHeight + EditorGUIUtility.singleLineHeight : baseHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Init(property);
            _canBeDynamic = property.FindPropertyRelative(nameof(PersistentArgument._canBeDynamic)).boolValue;

            const float indentWidth = 15f;
            const float valueLeftIndent = 2f;
            const float choiceButtonWidth = 19f;

            position.height = EditorGUIUtility.singleLineHeight;
            position.xMin += EditorGUI.indentLevel * indentWidth;

            var labelAndButtonRect = new Rect(position) { width = EditorGUIUtility.labelWidth };
            var valueRect = new Rect(position) { x = position.x + EditorGUIUtility.labelWidth, width = position.width - EditorGUIUtility.labelWidth };

            var buttonRect = new Rect(labelAndButtonRect) 
                { x = labelAndButtonRect.x + labelAndButtonRect.width - choiceButtonWidth, width = choiceButtonWidth };
            var labelRect = new Rect(labelAndButtonRect) { width = labelAndButtonRect.width - (_canBeDynamic ? choiceButtonWidth : 0) };

            valueRect.xMin += valueLeftIndent;

            if (HasFoldout)
                property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, label, true);
            else
                EditorGUI.HandlePrefixLabel(labelRect, labelRect, label);

            if (_canBeDynamic && GUI.Button(buttonRect, _isSerialized.boolValue ? "s" : "d", ChoiceButtonStyle))
                _isSerialized.boolValue = !_isSerialized.boolValue;

            using (new EditorGUI.IndentLevelScope(0))
            {
                if (_isSerialized.boolValue)
                    DrawSerializedValue(property, valueRect);
                else
                    DrawDynamicValue(valueRect);
            }
        }

        private void Init(SerializedProperty property)
        {
            _isSerialized = property.FindPropertyRelative(nameof(PersistentArgument._isSerialized));
            _index = property.FindPropertyRelative(nameof(PersistentArgument._index));
            _valueProperty = _isSerialized.boolValue ? GetValueProperty(property) : null;
        }

        private static SerializedProperty GetValueProperty(SerializedProperty argumentProperty)
        {
            var key = (argumentProperty.serializedObject, argumentProperty.propertyPath);
            var type = PersistentArgumentHelper.GetTypeFromProperty(argumentProperty, nameof(PersistentArgument._targetType));
            Assert.IsNotNull(type);

            if (_valuePropertyCache.TryGetValue(key, out var cached) && cached.GetObjectType() == type)
                return cached;

            _valuePropertyCache.Remove(key);
            
            var holderType = ScriptableObjectCache.GetClass(type);
            var holder = ScriptableObject.CreateInstance(holderType);
            var serializedHolder = new SerializedObject(holder);
            
            holderType.GetField(nameof(DeserializedValueHolder<int>.Value))
                .SetValue(holder, argumentProperty.GetObject<PersistentArgument>().SerializedValue);
            
            var valueProperty = serializedHolder.FindProperty(nameof(DeserializedValueHolder<int>.Value));
            _valuePropertyCache.Add(key, valueProperty);
            return valueProperty;
        }

        private static void SaveValueProperty(SerializedProperty argumentProperty, SerializedProperty valueProperty)
        {
            valueProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            LogHelper.RemoveLogEntriesByMode(LogModes.NoScriptAssetWarning);
            
            argumentProperty.GetObject<PersistentArgument>().SerializedValue = valueProperty.GetObject();
            EditorUtility.SetDirty(argumentProperty.serializedObject.targetObject);
        }

        private void DrawSerializedValue(SerializedProperty property, Rect valueRect)
        {
            if (_valueProperty == null) return;

            _valueProperty.serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            if (_valueProperty.propertyType == SerializedPropertyType.Generic)
            {
                _valueProperty.isExpanded = property.isExpanded;
                if (!property.isExpanded) return;

                var shiftedRect = new Rect(valueRect)
                {
                    y = valueRect.y + EditorGUIUtility.singleLineHeight,
                    x = valueRect.x + (EditorGUI.indentLevel + 1) * 15f,
                    width = valueRect.width - (EditorGUI.indentLevel + 1) * 15f
                };

                if (_valueProperty.isArray && _valueProperty.propertyType == SerializedPropertyType.Generic)
                {
                    var sizeRect = new Rect(shiftedRect) { height = EditorGUIUtility.singleLineHeight };
                    EditorGUI.PropertyField(sizeRect, _valueProperty.FindPropertyRelative("Array.size"));

                    if (_valueProperty.isExpanded)
                    {
                        var elementRect = new Rect(shiftedRect)
                        {
                            y = shiftedRect.y + EditorGUIUtility.singleLineHeight + EditorPackageSettings.LinePadding,
                            height = EditorGUIUtility.singleLineHeight
                        };

                        for (int i = 0; i < _valueProperty.arraySize; i++)
                        {
                            var element = _valueProperty.GetArrayElementAtIndex(i);
                            elementRect.height = EditorGUI.GetPropertyHeight(element, true);
                            EditorGUI.PropertyField(elementRect, element, true);
                            elementRect.y += elementRect.height + EditorPackageSettings.LinePadding;
                        }
                    }
                }
                else if (_valueProperty.HasCustomPropertyDrawer())
                {
                    shiftedRect.height = EditorGUI.GetPropertyHeight(_valueProperty);
                    EditorGUI.PropertyField(shiftedRect, _valueProperty, GUIContent.none);
                }
                else
                {
                    var iterator = _valueProperty.Copy();
                    var end = _valueProperty.Copy();
                    end.NextVisible(false);
                    
                    bool enterChildren = true;
                    while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        enterChildren = false;
                        shiftedRect.height = EditorGUI.GetPropertyHeight(iterator, true);
                        EditorGUI.PropertyField(shiftedRect, iterator, true);
                        shiftedRect.y += shiftedRect.height + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
            }
            else
            {
                EditorGUI.PropertyField(valueRect, _valueProperty, GUIContent.none);
            }

            if (EditorGUI.EndChangeCheck())
                SaveValueProperty(property, _valueProperty);
        }

        private void DrawDynamicValue(Rect valueRect)
        {
            var argNames = ExtEventDrawer.CurrentEventInfo?.ArgNames ?? Array.Empty<string>();
            var argTypes = ExtEventDrawer.CurrentEventInfo?.ParamTypes ?? Type.EmptyTypes;
            var argType = PersistentArgumentHelper.GetTypeFromProperty(_index.serializedObject.FindProperty("_fromType"), 
                nameof(PersistentArgument._fromType), nameof(PersistentArgument._targetType));
            Assert.IsNotNull(argType);

            var matching = new List<(string name, int index)>();
            for (int i = 0; i < argTypes.Length; i++)
                if (argTypes[i].IsAssignableFrom(argType))
                    matching.Add((argNames[i], i));

            var currentName = _index.intValue < argNames.Length ? argNames[_index.intValue] : 
                (matching.Count > 0 ? matching[0].name : "Invalid Index");

            using (new EditorGUI.DisabledGroupScope(matching.Count <= 1))
            {
                if (EditorGUI.DropdownButton(valueRect, new GUIContent(currentName), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var (name, index) in matching)
                        menu.AddItem(new GUIContent(name), _index.intValue == index, i => 
                        { 
                            _index.intValue = (int)i; 
                            _index.serializedObject.ApplyModifiedProperties(); 
                        }, index);
                    menu.ShowAsContext();
                }
            }
        }
    }
}