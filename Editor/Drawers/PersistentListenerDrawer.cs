namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using SolidUtilities;
    using SolidUtilities.Editor;
    using TypeReferences;
    using UnityDropdown.Editor;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Events;
    using Object = UnityEngine.Object;

#if GENERIC_UNITY_OBJECTS
    using GenericUnityObjects.Editor;
#endif

    [CustomPropertyDrawer(typeof(PersistentListener))]
    public class PersistentListenerDrawer : PropertyDrawer
    {
        private static readonly Dictionary<(SerializedObject serializedObject, string propertyPath), PersistentListenerInfo> _previousListenerValues = new();
        private Rect _methodRect;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            const int constantLinesCount = 2;
            return (EditorGUIUtility.singleLineHeight + EditorPackageSettings.LinePadding) * constantLinesCount + GetSerializedArgsHeight(property);
        }

        private static float GetSerializedArgsHeight(SerializedProperty property)
        {
            if (!MethodInfoDrawer.HasMethod(property))
                return 0f;

            var argsArray = property.FindPropertyRelative(nameof(PersistentListener._persistentArguments));
            float totalHeight = 0f;

            for (int i = 0; i < argsArray.arraySize; i++)
            {
                totalHeight += EditorGUI.GetPropertyHeight(argsArray.GetArrayElementAtIndex(i)) + EditorPackageSettings.LinePadding;
            }

            return totalHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var currentRect = new Rect(position) { height = EditorGUIUtility.singleLineHeight };
            currentRect.y += EditorPackageSettings.LinePadding;
            
            _methodRect = new Rect(currentRect) { 
                y = currentRect.y + EditorGUIUtility.singleLineHeight + EditorPackageSettings.LinePadding 
            };

            DrawCallStateAndTarget(property, currentRect);
            DrawMethodAndArguments(property);
        }

        private void DrawCallStateAndTarget(SerializedProperty property, Rect currentRect)
        {
            var callStateProp = property.FindPropertyRelative(nameof(PersistentListener.CallState));
            (var callStateRect, var targetRect) = currentRect.CutVertically(GetCallStateWidth((UnityEventCallState)callStateProp.enumValueIndex));
            callStateRect.width -= 10f;

            DrawCallState(callStateRect, callStateProp);
            DrawTargetField(property, targetRect);
        }

        private void DrawMethodAndArguments(SerializedProperty property)
        {
            MethodInfoDrawer.Draw(_methodRect, property, out var paramNames);

            bool argumentsChanged = DrawArguments(property, paramNames);
            bool methodChanged = CheckMethodChanges(property);

            if (argumentsChanged || methodChanged)
            {
                Reinitialize(property);
            }
        }

        private static void DrawCallState(Rect rect, SerializedProperty callStateProp)
        {
            if (!EditorGUI.DropdownButton(rect, GetCallStateContent((UnityEventCallState)callStateProp.enumValueIndex), FocusType.Passive, EditorStyles.miniPullDown))
                return;

            var menu = new GenericMenu();
            foreach (UnityEventCallState state in Enum.GetValues(typeof(UnityEventCallState)))
            {
                menu.AddItem(
                    new GUIContent(GetCallStateFullName(state)),
                    callStateProp.enumValueIndex == (int)state,
                    () => SetCallState(callStateProp, state)
                );
            }
            menu.ShowAsContext();
        }

        private static void SetCallState(SerializedProperty callStateProp, UnityEventCallState state)
        {
            callStateProp.enumValueIndex = (int)state;
            callStateProp.serializedObject.ApplyModifiedProperties();
        }

        private void DrawTargetField(SerializedProperty property, Rect rect)
        {
            bool isStatic = property.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue;
            var targetProp = property.FindPropertyRelative(nameof(PersistentListener._target));

            if (isStatic)
            {
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(nameof(PersistentListener._staticType)), GUIContent.none);
                return;
            }

            var newTarget = DrawObjectField(rect, targetProp.objectReferenceValue);
            
            if (targetProp.objectReferenceValue == newTarget)
                return;

            HandleNewTarget(property, targetProp, newTarget);
        }

        private static Object DrawObjectField(Rect rect, Object currentValue)
        {
#if GENERIC_UNITY_OBJECTS
            return GenericObjectDrawer.ObjectField(rect, GUIContent.none, currentValue, typeof(Object), true);
#else
            return EditorGUI.ObjectField(rect, GUIContent.none, currentValue, typeof(Object), true);
#endif
        }

        private void HandleNewTarget(SerializedProperty property, SerializedProperty targetProp, Object newTarget)
        {
            if (newTarget is GameObject gameObject)
            {
                ShowComponentDropdown(property, targetProp, gameObject);
            }
            else if (newTarget is Component || newTarget is ScriptableObject || newTarget is null)
            {
                SetTargetAndUpdate(property, targetProp, newTarget);
            }
            else
            {
                Debug.LogWarning($"Cannot assign an object of type {newTarget.GetType()} to the target field. Only GameObjects, Components, and ScriptableObjects can be assigned.");
            }
        }

        private void SetTargetAndUpdate(SerializedProperty property, SerializedProperty targetProp, Object newTarget)
        {
            targetProp.objectReferenceValue = newTarget;
            ExtEventDrawer.ResetListCache(property.GetParent().GetParent());
            MethodInfoDrawer.ShowMethodDropdown(_methodRect, property);
        }

        private bool DrawArguments(SerializedProperty listenerProperty, List<string> paramNames)
        {
            var argumentsArray = listenerProperty.FindPropertyRelative(nameof(PersistentListener._persistentArguments));

            if (paramNames == null || paramNames.Count < argumentsArray.arraySize)
                return false;

            EditorGUI.BeginChangeCheck();

            var rect = _methodRect;
            rect.y += EditorGUIUtility.singleLineHeight + EditorPackageSettings.LinePadding;

            for (int i = 0; i < argumentsArray.arraySize; i++)
            {
                var argumentProp = argumentsArray.GetArrayElementAtIndex(i);
                var propertyHeight = EditorGUI.GetPropertyHeight(argumentProp);
                
                rect.height = propertyHeight;
                rect.y += EditorPackageSettings.LinePadding;

                string label = EditorPackageSettings.NicifyArgumentNames ? 
                    ObjectNames.NicifyVariableName(paramNames[i]) : paramNames[i];
                
                EditorGUI.PropertyField(rect, argumentProp, GUIContentHelper.Temp(label));
                rect.y += propertyHeight;
            }

            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                listenerProperty.serializedObject.ApplyModifiedProperties();
                listenerProperty.serializedObject.Update();
            }

            return changed;
        }

        private bool CheckMethodChanges(SerializedProperty listenerProperty)
        {
            var key = (listenerProperty.serializedObject, listenerProperty.propertyPath);
            var currentType = listenerProperty.FindPropertyRelative($"{nameof(PersistentListener._staticType)}.{nameof(TypeReference._typeNameAndAssembly)}").stringValue;
            var currentTarget = listenerProperty.FindPropertyRelative(nameof(PersistentListener._target)).objectReferenceValue;
            var currentMethodName = listenerProperty.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue;

            if (!_previousListenerValues.TryGetValue(key, out var listenerInfo))
            {
                _previousListenerValues.Add(key, new PersistentListenerInfo(currentType, currentTarget, currentMethodName));
                return false;
            }

            bool infoChanged = false;

            if (currentType != listenerInfo.TypeName)
            {
                infoChanged = true;
                listenerInfo.TypeName = currentType;
                MethodInfoDrawer.ShowMethodDropdown(_methodRect, listenerProperty);
            }

            if (currentTarget != listenerInfo.Target)
            {
                infoChanged = true;
                listenerInfo.Target = currentTarget;
            }

            if (currentMethodName != listenerInfo.MethodName)
            {
                infoChanged = true;
                listenerInfo.MethodName = currentMethodName;
            }

            return infoChanged;
        }

        private void ShowComponentDropdown(SerializedProperty listenerProperty, SerializedProperty targetProperty, GameObject gameObject)
        {
            var components = gameObject
                .GetComponents<Component>()
                .Where(component => component != null && !component.hideFlags.ContainsFlag(HideFlags.HideInInspector))
                .Prepend<Object>(gameObject);

            var dropdownItems = components.Select(component => 
                new DropdownItem<Object>(component, GetComponentName(component), GetComponentIcon(component))
            ).ToList();

            var tree = new DropdownMenu<Object>(dropdownItems, component => 
            {
                targetProperty.objectReferenceValue = component;
                targetProperty.serializedObject.ApplyModifiedProperties();
                ExtEventDrawer.ResetListCache(listenerProperty.GetParent().GetParent());
                MethodInfoDrawer.ShowMethodDropdown(_methodRect, listenerProperty);
            });

            tree.ShowAsContext();
        }

        private static string GetComponentName(Object component)
        {
            var componentType = component.GetType();
            var componentMenu = componentType.GetCustomAttribute<AddComponentMenu>();

            if (componentMenu != null && !string.IsNullOrEmpty(componentMenu.componentMenu))
            {
                return componentMenu.componentMenu.GetSubstringAfterLast('/');
            }

            return ObjectNames.NicifyVariableName(componentType.Name);
        }

        private static Texture GetComponentIcon(Object component)
        {
            return EditorGUIUtility.ObjectContent(component, component.GetType()).image;
        }

        public static void Reinitialize(SerializedProperty listenerProperty)
        {
            listenerProperty.serializedObject.ApplyModifiedProperties();
            var listener = PropertyObjectCache.GetObject<PersistentListener>(listenerProperty);
            listener._initializationComplete = false;
        }

        private static float GetCallStateWidth(UnityEventCallState callState) => callState switch
        {
            UnityEventCallState.EditorAndRuntime => 58f,
            UnityEventCallState.RuntimeOnly => 48f,
            UnityEventCallState.Off => 58f,
            _ => throw new NotImplementedException()
        };

        private static GUIContent GetCallStateContent(UnityEventCallState callState) => callState switch
        {
            UnityEventCallState.EditorAndRuntime => GUIContentHelper.Temp("E|R", IconCache.EditorRuntime),
            UnityEventCallState.RuntimeOnly => GUIContentHelper.Temp("R", IconCache.Runtime),
            UnityEventCallState.Off => GUIContentHelper.Temp("Off", IconCache.Off),
            _ => throw new NotImplementedException()
        };

        private static string GetCallStateFullName(UnityEventCallState callState) => callState switch
        {
            UnityEventCallState.EditorAndRuntime => "Editor and Runtime",
            UnityEventCallState.RuntimeOnly => "Runtime Only",
            UnityEventCallState.Off => "Off",
            _ => throw new NotImplementedException()
        };

        private class PersistentListenerInfo
        {
            public string TypeName;
            public Object Target;
            public string MethodName;

            public PersistentListenerInfo(string typeName, Object target, string methodName)
            {
                TypeName = typeName;
                Target = target;
                MethodName = methodName;
            }
        }

        private static class IconCache
        {
            private static Texture _offIcon;
            private static Texture _editorRuntimeIcon;
            private static Texture _runtimeIcon;

            public static Texture Off => _offIcon ??= EditorGUIUtility.IconContent("sv_icon_dot6_sml").image;
            public static Texture EditorRuntime => _editorRuntimeIcon ??= EditorGUIUtility.IconContent("sv_icon_dot4_sml").image;
            public static Texture Runtime => _runtimeIcon ??= EditorGUIUtility.IconContent("sv_icon_dot3_sml").image;
        }
    }
}
