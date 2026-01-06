namespace ExtEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine.Scripting;

    public partial class Converter
    {
        public static readonly Dictionary<(Type from, Type to), Type> BuiltInConverters = new();
        public static readonly Dictionary<(Type from, Type to), Type> ConverterTypes = new();

        static Converter()
        {
            RegisterBuiltInConverters();
#if UNITY_EDITOR
            FindCustomConverters();
#endif
        }

        private static void RegisterBuiltInConverters()
        {
            RegisterConverter<sbyte, short>();
            RegisterConverter<sbyte, int>();
            RegisterConverter<sbyte, long>();
            RegisterConverter<sbyte, float>();
            RegisterConverter<sbyte, double>();
            RegisterConverter<sbyte, decimal>();
            
            RegisterConverter<byte, short>();
            RegisterConverter<byte, ushort>();
            RegisterConverter<byte, int>();
            RegisterConverter<byte, uint>();
            RegisterConverter<byte, long>();
            RegisterConverter<byte, ulong>();
            RegisterConverter<byte, float>();
            RegisterConverter<byte, double>();
            RegisterConverter<byte, decimal>();
            
            RegisterConverter<short, int>();
            RegisterConverter<short, long>();
            RegisterConverter<short, float>();
            RegisterConverter<short, double>();
            RegisterConverter<short, decimal>();
            
            RegisterConverter<ushort, int>();
            RegisterConverter<ushort, uint>();
            RegisterConverter<ushort, long>();
            RegisterConverter<ushort, ulong>();
            RegisterConverter<ushort, float>();
            RegisterConverter<ushort, double>();
            RegisterConverter<ushort, decimal>();
            
            RegisterConverter<int, long>();
            RegisterConverter<int, float>();
            RegisterConverter<int, double>();
            RegisterConverter<int, decimal>();
            
            RegisterConverter<uint, long>();
            RegisterConverter<uint, ulong>();
            RegisterConverter<uint, float>();
            RegisterConverter<uint, double>();
            RegisterConverter<uint, decimal>();
            
            RegisterConverter<long, float>();
            RegisterConverter<long, double>();
            RegisterConverter<long, decimal>();
            
            RegisterConverter<ulong, float>();
            RegisterConverter<ulong, double>();
            RegisterConverter<ulong, decimal>();
            
            RegisterConverter<float, double>();
        }

        private static void RegisterConverter<TFrom, TTo>() 
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            var key = (typeof(TFrom), typeof(TTo));
            BuiltInConverters[key] = typeof(GenericConverter<TFrom, TTo>);
            ConverterTypes[key] = typeof(GenericConverter<TFrom, TTo>);
        }

#if UNITY_EDITOR
        private static void FindCustomConverters()
        {
            foreach ((var fromToTypes, Type customConverterType) in GetCustomConverters())
            {
                if (ConverterTypes.TryGetValue(fromToTypes, out var converterType))
                {
                    UnityEngine.Debug.LogWarning($"Two custom converters for the same pair of types: {converterType} and {customConverterType}");
                    continue;
                }

                ConverterTypes.Add(fromToTypes, customConverterType);
            }
        }

        internal static IEnumerable<((Type from, Type to) fromToTypes, Type customConverter)> GetCustomConverters()
        {
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom<Converter>();

            foreach (Type type in types)
            {
                if (type.IsGenericType || type.IsAbstract)
                    continue;

                var baseType = type.BaseType;

                // ReSharper disable once PossibleNullReferenceException
                if (!baseType.IsGenericType)
                    continue;

                var genericArgs = baseType.GetGenericArguments();

                if (genericArgs.Length != 2)
                    continue;

                var fromToTypes = (genericArgs[0], genericArgs[1]);

                yield return (fromToTypes, type);
            }
        }
#endif
    }

    [Preserve]
    internal class GenericConverter<TFrom, TTo> : Converter
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        private TTo _arg;

        public override unsafe void* Convert(void* sourceTypePointer)
        {
            dynamic fromValue = Unsafe.Read<TFrom>(sourceTypePointer);
            _arg = (TTo)fromValue;
            return Unsafe.AsPointer(ref _arg);
        }
    }
}
