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
        private static readonly Dictionary<(SerializedObject, string), ListenerState> _stateCache = new();
        private Rect _methodRect;

        private const float LinePadding = 2f;
        private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!MethodInfoDrawer.HasMethod(property))
                return (LineHeight + LinePadding) * 2;

            var args = property.FindPropertyRelative(nameof(PersistentListener._persistentArguments));
            float total = (LineHeight + LinePadding) * 2;
            for (int i = 0; i < args.arraySize; i++)
                total += EditorGUI.GetPropertyHeight(args.GetArrayElementAtIndex(i)) + LinePadding;
            return total;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var rect = new Rect(position) { height = LineHeight };
            rect.y += LinePadding;
            _methodRect = new Rect(rect) { y = rect.y + LineHeight + LinePadding };

            var callState = property.FindPropertyRelative(nameof(PersistentListener.CallState));
            var state = (UnityEventCallState)callState.enumValueIndex;
            
            float stateWidth = GetStateWidth(state);
            var stateRect = new Rect(rect) { width = stateWidth - 10f };
            var targetRect = new Rect(rect) { x = rect.x + stateWidth, width = rect.width - stateWidth };

            if (EditorGUI.DropdownButton(stateRect, GetStateContent(state), FocusType.Passive, EditorStyles.miniPullDown))
                ShowStateMenu(callState);

            DrawTarget(property, targetRect);
            DrawMethodAndArgs(property);
        }

        private static void ShowStateMenu(SerializedProperty callState)
        {
            var menu = new GenericMenu();
            foreach (UnityEventCallState value in Enum.GetValues(typeof(UnityEventCallState)))
            {
                var localValue = value;
                menu.AddItem(new GUIContent(GetStateName(value)), callState.enumValueIndex == (int)value, 
                    () => SetState(callState, localValue));
            }
            menu.ShowAsContext();
        }

        private static void SetState(SerializedProperty callState, UnityEventCallState value)
        {
            callState.enumValueIndex = (int)value;
            callState.serializedObject.ApplyModifiedProperties();
        }

        private void DrawTarget(SerializedProperty property, Rect rect)
        {
            var isStatic = property.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue;
            
            if (isStatic)
            {
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(nameof(PersistentListener._staticType)), GUIContent.none);
                return;
            }

            var target = property.FindPropertyRelative(nameof(PersistentListener._target));
            var newTarget = DrawObjectField(rect, target.objectReferenceValue);
            
            if (target.objectReferenceValue == newTarget) 
                return;

            if (newTarget is GameObject go)
            {
                ShowComponentPicker(property, target, go);
            }
            else if (newTarget == null || newTarget is Component || newTarget is ScriptableObject)
            {
                target.objectReferenceValue = newTarget;
                ExtEventDrawer.ResetListCache(property.GetParent().GetParent());
                MethodInfoDrawer.ShowMethodDropdown(_methodRect, property);
            }
            else
            {
                Debug.LogWarning("Target must be GameObject, Component, ScriptableObject, or null");
            }
        }

        private static Object DrawObjectField(Rect rect, Object value)
        {
#if GENERIC_UNITY_OBJECTS
            return GenericObjectDrawer.ObjectField(rect, GUIContent.none, value, typeof(Object), true);
#else
            return EditorGUI.ObjectField(rect, GUIContent.none, value, typeof(Object), true);
#endif
        }

        private void ShowComponentPicker(SerializedProperty property, SerializedProperty target, GameObject go)
        {
            var items = go.GetComponents<Component>()
                .Where(c => c != null && !c.hideFlags.HasFlag(HideFlags.HideInInspector))
                .Prepend<Object>(go)
                .Select(c => new DropdownItem<Object>(c, GetDisplayName(c), GetIcon(c)))
                .ToList();

            new DropdownMenu<Object>(items, selected =>
            {
                target.objectReferenceValue = selected;
                target.serializedObject.ApplyModifiedProperties();
                ExtEventDrawer.ResetListCache(property.GetParent().GetParent());
                MethodInfoDrawer.ShowMethodDropdown(_methodRect, property);
            }).ShowAsContext();
        }

        private static string GetDisplayName(Object obj)
        {
            var type = obj.GetType();
            var menu = type.GetCustomAttribute<AddComponentMenu>();
            return menu != null && !string.IsNullOrEmpty(menu.componentMenu) 
                ? menu.componentMenu.Split('/').Last() 
                : ObjectNames.NicifyVariableName(type.Name);
        }

        private static Texture GetIcon(Object obj) => 
            EditorGUIUtility.ObjectContent(obj, obj.GetType()).image;

        private void DrawMethodAndArgs(SerializedProperty property)
        {
            MethodInfoDrawer.Draw(_methodRect, property, out var paramNames);

            if (paramNames == null) 
                return;

            var args = property.FindPropertyRelative(nameof(PersistentListener._persistentArguments));
            if (args.arraySize != paramNames.Count) 
                return;

            bool changed = false;
            var rect = _methodRect;
            rect.y += LineHeight + LinePadding;

            for (int i = 0; i < args.arraySize; i++)
            {
                var arg = args.GetArrayElementAtIndex(i);
                float height = EditorGUI.GetPropertyHeight(arg);
                
                rect.height = height;
                rect.y += LinePadding;

                EditorGUI.BeginChangeCheck();
                string label = EditorPackageSettings.NicifyArgumentNames 
                    ? ObjectNames.NicifyVariableName(paramNames[i]) 
                    : paramNames[i];
                EditorGUI.PropertyField(rect, arg, new GUIContent(label));
                
                if (EditorGUI.EndChangeCheck())
                    changed = true;

                rect.y += height;
            }

            if (changed || DetectStateChange(property))
                ResetListener(property);
        }

        private bool DetectStateChange(SerializedProperty property)
        {
            var key = (property.serializedObject, property.propertyPath);
            
            string type = property.FindPropertyRelative($"{nameof(PersistentListener._staticType)}.{nameof(TypeReference._typeNameAndAssembly)}").stringValue;
            var target = property.FindPropertyRelative(nameof(PersistentListener._target)).objectReferenceValue;
            string method = property.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue;

            if (!_stateCache.TryGetValue(key, out var state))
            {
                _stateCache[key] = new ListenerState(type, target, method);
                return false;
            }

            bool changed = false;

            if (type != state.TypeName)
            {
                changed = true;
                state.TypeName = type;
                MethodInfoDrawer.ShowMethodDropdown(_methodRect, property);
            }

            if (target != state.Target)
            {
                changed = true;
                state.Target = target;
            }

            if (method != state.MethodName)
            {
                changed = true;
                state.MethodName = method;
            }

            return changed;
        }

        public static void ResetListener(SerializedProperty property)
        {
            property.serializedObject.ApplyModifiedProperties();
            PropertyObjectCache.GetObject<PersistentListener>(property)._initializationComplete = false;
        }

        private static float GetStateWidth(UnityEventCallState state) => state switch
        {
            UnityEventCallState.EditorAndRuntime => 58f,
            UnityEventCallState.RuntimeOnly => 48f,
            UnityEventCallState.Off => 58f,
            _ => 58f
        };

        private static GUIContent GetStateContent(UnityEventCallState state) => state switch
        {
            UnityEventCallState.EditorAndRuntime => new GUIContent("E|R", Icons.EditorRuntime),
            UnityEventCallState.RuntimeOnly => new GUIContent("R", Icons.Runtime),
            UnityEventCallState.Off => new GUIContent("Off", Icons.Off),
            _ => new GUIContent("Off")
        };

        private static string GetStateName(UnityEventCallState state) => state switch
        {
            UnityEventCallState.EditorAndRuntime => "Editor and Runtime",
            UnityEventCallState.RuntimeOnly => "Runtime Only",
            UnityEventCallState.Off => "Off",
            _ => "Off"
        };

        private class ListenerState
        {
            public string TypeName;
            public Object Target;
            public string MethodName;
            public ListenerState(string type, Object target, string method) 
                => (TypeName, Target, MethodName) = (type, target, method);
        }

        private static class Icons
        {
            public static readonly Texture Off = EditorGUIUtility.IconContent("sv_icon_dot6_sml").image;
            public static readonly Texture EditorRuntime = EditorGUIUtility.IconContent("sv_icon_dot4_sml").image;
            public static readonly Texture Runtime = EditorGUIUtility.IconContent("sv_icon_dot3_sml").image;
        }
    }
}