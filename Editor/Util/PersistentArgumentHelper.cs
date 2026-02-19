namespace ExtEvents.Editor
{
    using System;
    using TypeReferences;
    using UnityEditor;

    public static class PersistentArgumentHelper
    {
        public static Type GetTypeFromProperty(SerializedProperty property, string primary, string fallback = null)
        {
            return GetType(property, primary) ?? (fallback != null ? GetType(property, fallback) : null);
        }

        private static Type GetType(SerializedProperty property, string field) => 
            Type.GetType(property.FindPropertyRelative($"{field}.{nameof(TypeReference._typeNameAndAssembly)}").stringValue);
    }
}