namespace ExtEvents.Editor
{
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;

    internal class FoldoutList
    {
        private readonly ReorderableList _list;
        private readonly SerializedProperty _elements;
        private readonly string _title;
        private readonly SerializedProperty _expanded;

        public Action<Rect, int> DrawElementCallback;
        public Func<int, float> ElementHeightCallback;
        public Action OnAddDropdownCallback;
        public Action<Rect, FoldoutList> DrawFooterCallback;

        private static readonly GUIStyle FooterBg = "RL Footer";
        private static readonly GUIStyle FooterButton = "RL FooterButton";

        private static Action<ReorderableList> _clearCache;
        private static Action<ReorderableList> _cacheIfNeeded;
        private static Action<ReorderableList> _clearCacheRecursive;
        private static FieldInfo _scheduleRemoveField;
        private static Func<ReorderableList, bool> _isOverMax;

        private static Action<ReorderableList> ClearCache => 
            _clearCache ??= CreateDelegate<Action<ReorderableList>>("ClearCache", "InvalidateCache");
        
        private static Action<ReorderableList> CacheIfNeeded => 
            _cacheIfNeeded ??= CreateDelegate<Action<ReorderableList>>("CacheIfNeeded");
        
        private static Func<ReorderableList, bool> IsOverMax => 
            _isOverMax ??= CreateDelegate<Func<ReorderableList, bool>>("get_isOverMaxMultiEditLimit");

        private static Action<ReorderableList> ClearCacheRecursive
        {
            get
            {
                if (_clearCacheRecursive != null) 
                    return _clearCacheRecursive;
                    
                var method = typeof(ReorderableList).GetMethod("ClearCacheRecursive", BindingFlags.Instance | BindingFlags.NonPublic) ??
                            typeof(ReorderableList).GetMethod("InvalidateCacheRecursive", BindingFlags.Instance | BindingFlags.NonPublic);
                _clearCacheRecursive = (Action<ReorderableList>)Delegate.CreateDelegate(typeof(Action<ReorderableList>), method);
                return _clearCacheRecursive;
            }
        }

        private bool ScheduleRemove
        {
            get
            {
                if (_scheduleRemoveField == null)
                    _scheduleRemoveField = typeof(ReorderableList).GetField("scheduleRemove", BindingFlags.NonPublic | BindingFlags.Instance);
                return _scheduleRemoveField != null && (bool)_scheduleRemoveField.GetValue(_list);
            }
            set => _scheduleRemoveField?.SetValue(_list, value);
        }

        private static T CreateDelegate<T>(string primary, string alt = null) where T : Delegate
        {
            var method = typeof(ReorderableList).GetMethod(primary, BindingFlags.Instance | BindingFlags.NonPublic) ??
                        (alt != null ? typeof(ReorderableList).GetMethod(alt, BindingFlags.Instance | BindingFlags.NonPublic) : null);
            return (T)Delegate.CreateDelegate(typeof(T), method);
        }

        public FoldoutList(SerializedProperty elements, string title, SerializedProperty expanded)
        {
            _elements = elements;
            _title = title;
            _expanded = expanded;
            _list = CreateList();
        }

        private ReorderableList CreateList() => new(_elements.serializedObject, _elements)
        {
            drawHeaderCallback = rect =>
            {
                rect = new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height);
                bool was = _expanded.boolValue;
                bool now = EditorGUI.Foldout(rect, was, _title, true);
                if (was != now)
                {
                    _expanded.boolValue = now;
                    _list.draggable = now;
                    ClearCache(_list);
                }
            },
            drawElementCallback = (rect, index, _, __) => { if (_expanded.boolValue) DrawElementCallback(rect, index); },
            elementHeightCallback = index => _expanded.boolValue ? ElementHeightCallback(index) : 0f,
            onAddDropdownCallback = (_, __) => OnAddDropdownCallback?.Invoke(),
            drawFooterCallback = rect =>
            {
                if (!_expanded.boolValue) return;
                if (DrawFooterCallback != null) DrawFooterCallback(rect, this);
                else ReorderableList.defaultBehaviours.DrawFooter(rect, _list);
            },
            draggable = _expanded.boolValue
        };

        public void DoList(Rect rect) => _list.DoList(rect);
        public float GetHeight() => _list.GetHeight();
        public void ResetCache()
        {
            ClearCache(_list);
            CacheIfNeeded(_list);
        }
        private void InvalidateCache() => ClearCacheRecursive(_list);

        public static void DrawFooter(Rect rect, FoldoutList list, params ButtonData[] buttons)
        {
            float right = rect.xMax - 10f;
            float left = right - 8f - buttons.Sum(b => b.Size.x);
            rect = new Rect(left, rect.y, right - left, rect.height);

            if (Event.current.type == EventType.Repaint)
                FooterBg.Draw(rect, false, false, false, false);

            float x = left + 4f;
            foreach (var btn in buttons)
            {
                bool disabled = btn.IsAddButton
                    ? (list._list.onCanAddCallback != null && !list._list.onCanAddCallback(list._list)) || IsOverMax(list._list)
                    : list._list.index < 0 || list._list.index >= list._list.count ||
                      (list._list.onCanRemoveCallback != null && !list._list.onCanRemoveCallback(list._list)) || IsOverMax(list._list);

                using (new EditorGUI.DisabledScope(disabled))
                {
                    var btnRect = new Rect(new Vector2(x, rect.y), btn.Size);
                    if (GUI.Button(btnRect, btn.Content, FooterButton) || (!btn.IsAddButton && GUI.enabled && list.ScheduleRemove))
                        btn.Action?.Invoke(btnRect, list);
                }
                x += btn.Size.x;
            }
            list.ScheduleRemove = false;
        }

        public static readonly ButtonData DefaultAddButton = new(
            new Vector2(25f, 16f),
            EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to the list"),
            true,
            (rect, list) =>
            {
                if (list._list.onAddDropdownCallback != null)
                    list._list.onAddDropdownCallback(rect, list._list);
                else if (list._list.onAddCallback != null)
                    list._list.onAddCallback(list._list);
                else
                    ReorderableList.defaultBehaviours.DoAddButton(list._list);

                list._list.onChangedCallback?.Invoke(list._list);
                list.InvalidateCache();
            });

        public static readonly ButtonData DefaultRemoveButton = new(
            new Vector2(25f, 16f),
            EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from the list"),
            false,
            (rect, list) =>
            {
                if (list._list.onRemoveCallback == null && list._list.index >= 0 && list._list.index < list._list.count)
                    ReorderableList.defaultBehaviours.DoRemoveButton(list._list);
                else
                    list._list.onRemoveCallback?.Invoke(list._list);

                list._list.onChangedCallback?.Invoke(list._list);
                list.InvalidateCache();
                GUI.changed = true;
            });

        public class ButtonData
        {
            public readonly Vector2 Size;
            public readonly GUIContent Content;
            public readonly Action<Rect, FoldoutList> Action;
            public readonly bool IsAddButton;

            public ButtonData(Vector2 size, GUIContent content, bool isAdd, Action<Rect, FoldoutList> action)
                => (Size, Content, IsAddButton, Action) = (size, content, isAdd, action);
        }
    }
}