namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using SolidUtilities.Editor;
    using SolidUtilities.UnityEditorInternals;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Events;

    [CustomPropertyDrawer(typeof(BaseExtEvent), true)]
    public class ExtEventDrawer : PropertyDrawer
    {
        private static readonly Dictionary<(SerializedObject, string), ExtEventInfo> _eventCache = new();
        private static readonly Dictionary<(SerializedObject, string), FoldoutList> _listCache = new();
        private static string[] _overrideNames;

        public static ExtEventInfo CurrentEventInfo { get; private set; }
        public static void SetOverrideArgNames(string[] names) => _overrideNames = names;
        public static void ResetOverrideArgNames() => _overrideNames = null;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var list = GetOrCreateList(property, label.text);
            return list.GetHeight() + DynamicListenersDrawer.GetHeight(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CurrentEventInfo = GetOrCreateEventInfo(property);
            var list = GetOrCreateList(property, label.text);
            var rect = new Rect(position) { height = list.GetHeight() };
            list.DoList(rect);
            DynamicListenersDrawer.DrawListeners(property, position, rect.height);
        }

        public static void ResetListCache(SerializedProperty prop) => 
            GetOrCreateList(prop, null).ResetCache();

        public static ExtEventInfo GetOrCreateEventInfo(SerializedProperty prop)
        {
            var key = (prop.serializedObject, prop.propertyPath);
            if (!_eventCache.TryGetValue(key, out var info))
            {
                var (field, type) = prop.GetFieldInfoAndType();
                var args = type.GenericTypeArguments;
                var attr = field.GetCustomAttribute<EventArgumentsAttribute>()?.ArgumentNames ?? Array.Empty<string>();
                
                var names = new string[args.Length];
                for (int i = 0; i < names.Length; i++)
                    names[i] = i < attr.Length ? attr[i] : $"Arg{i + 1}";

                info = new ExtEventInfo(names, type.IsGenericType ? args : Type.EmptyTypes);
                _eventCache[key] = info;
            }

            if (_overrideNames != null)
                info.ArgNames = _overrideNames;
            
            return info;
        }

        private static FoldoutList GetOrCreateList(SerializedProperty prop, string label)
        {
            var key = (prop.serializedObject, prop.propertyPath);
            if (_listCache.TryGetValue(key, out var list))
                return list;

            var listeners = prop.FindPropertyRelative(nameof(ExtEvent._persistentListeners));
            var expanded = prop.FindPropertyRelative(nameof(BaseExtEvent.Expanded));

            list = new FoldoutList(listeners, label, expanded)
            {
                DrawElementCallback = (r, i) => EditorGUI.PropertyField(r, listeners.GetArrayElementAtIndex(i)),
                ElementHeightCallback = i => listeners.arraySize == 0 ? 21f : EditorGUI.GetPropertyHeight(listeners.GetArrayElementAtIndex(i)),
                DrawFooterCallback = (r, _) => FoldoutList.DrawFooter(r, list,
                    CreateAddButton(listeners, true, new Vector2(29f, 16f), EditorIcons.AddButtonS.Default, "Add static listener"),
                    CreateAddButton(listeners, false, new Vector2(25f, 16f), EditorIcons.AddButtonI.Default, "Add instance listener"),
                    FoldoutList.DefaultRemoveButton)
            };

            _listCache[key] = list;
            return list;
        }

        private static FoldoutList.ButtonData CreateAddButton(SerializedProperty listeners, bool isStatic, Vector2 size, Texture2D icon, string tip)
        {
            return new FoldoutList.ButtonData(size, new GUIContent(icon, tip), true, (_, __) => AddListener(listeners, isStatic));
        }

        private static void AddListener(SerializedProperty listeners, bool isStatic)
        {
            listeners.arraySize++;
            int index = listeners.arraySize - 1;
            var last = listeners.GetArrayElementAtIndex(index);
            var prev = index > 0 ? listeners.GetArrayElementAtIndex(index - 1) : null;

            last.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue = isStatic;

            if (prev?.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue != isStatic)
                last.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue = string.Empty;

            if (listeners.arraySize == 1)
                last.FindPropertyRelative(nameof(PersistentListener.CallState)).enumValueIndex = (int)UnityEventCallState.RuntimeOnly;

            listeners.serializedObject.ApplyModifiedProperties();
        }
    }

    public class ExtEventInfo
    {
        public string[] ArgNames;
        public readonly Type[] ParamTypes;
        public ExtEventInfo(string[] names, Type[] types) => (ArgNames, ParamTypes) = (names, types);
    }
}