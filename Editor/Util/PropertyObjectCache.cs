namespace ExtEvents.Editor
{
    using System.Collections.Generic;
    using SolidUtilities.Editor;
    using UnityEditor;

    internal static class PropertyObjectCache
    {
        private static readonly Dictionary<(SerializedObject, string), object> _cache = new();

        public static T GetObject<T>(SerializedProperty property)
        {
            var key = (property.serializedObject, property.propertyPath);
            if (!_cache.TryGetValue(key, out var obj))
                _cache[key] = obj = property.GetObject();
            return (T)obj;
        }
    }
}