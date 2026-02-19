namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;

    internal static class ScriptableObjectCache
    {
        private const string AssemblyName = "ExtEvents.Editor.DynamicAssembly";
        private static readonly AssemblyBuilder _assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(AssemblyName), AssemblyBuilderAccess.Run);
        private static readonly ModuleBuilder _module = _assembly.DefineDynamicModule(AssemblyName, true);
        private static readonly Dictionary<Type, Type> _cache = new();

        public static Type GetClass(Type valueType)
        {
            if (_cache.TryGetValue(valueType, out var type))
                return type;

            var name = $"{AssemblyName}.{valueType.FullName.Replace('.', '_').Replace('`', '_')}";
            var baseType = typeof(DeserializedValueHolder<>).MakeGenericType(valueType);
            return _cache[valueType] = _module.DefineType(name, TypeAttributes.NotPublic, baseType).CreateType();
        }
    }
}