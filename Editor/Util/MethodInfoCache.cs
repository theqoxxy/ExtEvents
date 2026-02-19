namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal static class MethodInfoCache
    {
        private static readonly Dictionary<(Type, string, Type[]), MethodInfo> _cache = new();

        public static MethodInfo GetItem(Type type, string name, bool isStatic, Type[] argTypes)
        {
            var key = (type, name, argTypes);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | 
                       (isStatic ? BindingFlags.Static : BindingFlags.Instance | BindingFlags.Static);

            var method = type.GetMethod(name, flags, null, CallingConventions.Any, argTypes, null) ?? 
                        FindMethod(type, name, flags, argTypes);

            _cache[key] = method;
            return method;
        }

        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type[] argTypes)
        {
            return type.GetMethods(flags)
                .FirstOrDefault(m => m.Name == name && ParametersMatch(m.GetParameters(), argTypes));
        }

        private static bool ParametersMatch(ParameterInfo[] parameters, Type[] argTypes)
        {
            if (parameters.Length != argTypes.Length)
                return false;

            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].ParameterType != argTypes[i])
                    return false;

            return true;
        }
    }
}