namespace ExtEvents.Editor
{
    using System;
    using SolidUtilities;
    using SolidUtilities.Editor;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

#if GENERIC_UNITY_OBJECTS
    using GenericUnityObjects.Editor;
#endif

    public static class DynamicListenersDrawer
    {
        private const float LineHeight = 18f;
        private const float Padding = 2f;

        public static float GetHeight(SerializedProperty prop)
        {
            if (!prop.FindPropertyRelative(nameof(BaseExtEvent.Expanded)).boolValue) 
                return 0f;

            var obj = PropertyObjectCache.GetObject<BaseExtEvent>(prop);
            if (obj?._dynamicListeners == null) 
                return 0f;

            return (LineHeight + Padding) * (prop.isExpanded ? obj._dynamicListeners.GetInvocationList().Length : 0);
        }

        public static void DrawListeners(SerializedProperty prop, Rect total, float listHeight)
        {
            using var indent = new EditorGUI.IndentLevelScope(EditorGUI.indentLevel + 2);

            if (!prop.FindPropertyRelative(nameof(BaseExtEvent.Expanded)).boolValue) 
                return;

            var obj = PropertyObjectCache.GetObject<BaseExtEvent>(prop);
            if (obj._dynamicListeners == null) 
                return;

            var rect = new Rect(total)
            {
                height = LineHeight,
                y = total.y + listHeight - LineHeight - Padding
            };

            prop.isExpanded = EditorGUI.Foldout(rect, prop.isExpanded, "Dynamic Listeners", true);
            if (!prop.isExpanded) 
                return;

            foreach (var d in obj._dynamicListeners.GetInvocationList())
            {
                rect.y += LineHeight + Padding;
                var (typeRect, methodRect) = Split(rect);
                
                DrawTarget(typeRect, d);
                EditorGUI.LabelField(methodRect, GetMethodName(d));
            }
        }

        private static (Rect, Rect) Split(Rect rect)
        {
            float half = rect.width / 2f;
            return (new Rect(rect) { width = half }, new Rect(rect) { x = rect.x + half, width = half });
        }

        private static void DrawTarget(Rect rect, Delegate d)
        {
            if (d.Target is Object target)
            {
                using (new EditorGUI.DisabledScope(true))
                {
#if GENERIC_UNITY_OBJECTS
                    GenericObjectDrawer.ObjectField(rect, GUIContent.none, target, target.GetType(), true);
#else
                    EditorGUI.ObjectField(rect, GUIContent.none, target, target.GetType(), true);
#endif
                }
            }
            else
            {
                var name = d.Method.DeclaringType?.FullName ?? "";
                if (name.EndsWith("<>c")) name = name[..^4];
                EditorGUI.LabelField(rect, name.GetSubstringAfterLast('.'));
            }
        }

        private static string GetMethodName(Delegate d)
        {
            var name = d.Method.Name;
            return name.StartsWith('<') ? "Lambda Expression" : $"{name}()";
        }
    }
}