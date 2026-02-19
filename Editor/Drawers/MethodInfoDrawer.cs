namespace ExtEvents.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using SolidUtilities;
    using SolidUtilities.Editor;
    using TypeReferences;
    using TypeReferences.Editor.Util;
    using UnityDropdown.Editor;
    using UnityEditor;
    using UnityEngine;

    public static class MethodInfoDrawer
    {
        private static readonly Dictionary<string, string> TypeAliases = new()
        {
            ["Boolean"] = "bool", ["Byte"] = "byte", ["SByte"] = "sbyte", ["Char"] = "char",
            ["Decimal"] = "decimal", ["Double"] = "double", ["Single"] = "float", ["Int32"] = "int",
            ["UInt32"] = "uint", ["Int64"] = "long", ["UInt64"] = "ulong", ["Int16"] = "short",
            ["UInt16"] = "ushort", ["Object"] = "object", ["String"] = "string"
        };

        private static readonly Color ErrorColor = new(1f, 0f, 0f, .5f);

        public static bool HasMethod(SerializedProperty prop)
        {
            var name = prop.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue;
            if (string.IsNullOrEmpty(name)) return false;
            
            var isStatic = prop.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue;
            var type = GetTargetType(prop, isStatic);
            return type != null && GetMethod(type, prop, isStatic, name) != null;
        }

        public static void Draw(Rect rect, SerializedProperty prop, out List<string> argNames)
        {
            argNames = null;
            var isStatic = prop.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue;
            var type = GetTargetType(prop, isStatic);
            var name = prop.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue;
            var method = GetMethod(type, prop, isStatic, name);
            
            argNames = method?.GetParameters().Select(p => p.Name).ToList();

            using (new EditorGUI.DisabledGroupScope(type == null))
            {
                var color = GUI.backgroundColor;
                if (method == null && !string.IsNullOrEmpty(name))
                    GUI.backgroundColor = ErrorColor;

                var display = GetDisplayText(name, method);
                if (EditorGUI.DropdownButton(rect, new GUIContent(display), FocusType.Passive))
                    ShowMenu(type, prop, !isStatic, method);

                GUI.backgroundColor = color;
            }
        }

        public static void ShowMethodDropdown(Rect rect, SerializedProperty prop)
        {
            if (!string.IsNullOrEmpty(prop.FindPropertyRelative(nameof(PersistentListener._methodName)).stringValue))
                return;

            var isStatic = prop.FindPropertyRelative(nameof(PersistentListener._isStatic)).boolValue;
            var type = GetTargetType(prop, isStatic);
            if (type == null) return;

            ShowMenu(type, prop, !isStatic, null, GUIUtility.GUIToScreenPoint(rect.position));
        }

        private static Type GetTargetType(SerializedProperty prop, bool isStatic)
        {
            if (!isStatic)
                return prop.FindPropertyRelative(nameof(PersistentListener._target)).objectReferenceValue?.GetType();

            var typeName = prop.FindPropertyRelative($"{nameof(PersistentListener._staticType)}.{nameof(TypeReference._typeNameAndAssembly)}").stringValue;
            return Type.GetType(typeName);
        }

        private static MethodInfo GetMethod(Type type, SerializedProperty prop, bool isStatic, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            var args = prop.FindPropertyRelative(nameof(PersistentListener._persistentArguments));
            var types = GetArgTypes(args);
            return types != null ? MethodInfoCache.GetItem(type, name, isStatic, types) : null;
        }

        private static Type[] GetArgTypes(SerializedProperty args)
        {
            var types = new Type[args.arraySize];
            for (int i = 0; i < args.arraySize; i++)
            {
                var name = args.GetArrayElementAtIndex(i)
                    .FindPropertyRelative($"{nameof(PersistentArgument._targetType)}.{nameof(TypeReference._typeNameAndAssembly)}").stringValue;
                
                types[i] = Type.GetType(name);
                if (types[i] == null) return null;
            }
            return types;
        }

        private static void ShowMenu(Type type, SerializedProperty prop, bool instance, MethodInfo current, Vector2? pos = null)
        {
            var paramTypes = ExtEventDrawer.CurrentEventInfo?.ParamTypes ?? Type.EmptyTypes;
            var items = new List<DropdownItem<MethodInfo>>();

            if (instance)
                items.AddRange(GetMethods(type, paramTypes, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, "Instance"));
            
            items.AddRange(GetMethods(type, paramTypes, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, "Static"));

            SortItems(items);

            var selected = items.Find(i => i.Value == current);
            if (selected != null) selected.IsSelected = true;

            var menu = new DropdownMenu<MethodInfo>(items, m => OnMethodSelected(current, m, prop));
            menu.ExpandAllFolders();

            if (pos.HasValue) menu.ShowDropdown(pos.Value);
            else menu.ShowAsContext();
        }

        private static IEnumerable<DropdownItem<MethodInfo>> GetMethods(Type type, Type[] eventParams, BindingFlags flags, string category)
        {
            return type.GetMethods(flags)
                .Where(m => BaseExtEvent.MethodIsEligible(m, eventParams, 
                    EditorPackageSettings.IncludeInternalMethods, 
                    EditorPackageSettings.IncludePrivateMethods))
                .SelectMany(m => CreateItems(m, category));
        }

        private static IEnumerable<DropdownItem<MethodInfo>> CreateItems(MethodInfo method, string category)
        {
            var isProp = method.Name.IsPropertySetter();
            var sub = isProp ? "Properties" : "Methods";
            var name = FormatMethodName(method, isProp);
            yield return new DropdownItem<MethodInfo>(method, $"{category} {sub}/{name}", searchName: name);
        }

        private static string FormatMethodName(MethodInfo method, bool isProp)
        {
            if (isProp)
            {
                var param = method.GetParameters()[0].ParameterType;
                return $"{method.Name.Substring(4)} ({Alias(param.Name)})";
            }

            var pars = method.GetParameters();
            var list = string.Join(", ", pars.Select(p => $"{Alias(p.ParameterType.Name)} {p.Name}"));
            return $"{method.Name}({list})";
        }

        private static string Alias(string name) => 
            TypeAliases.TryGetValue(name, out var alias) ? alias : name;

        private static void SortItems(List<DropdownItem<MethodInfo>> items)
        {
            items.Sort((a, b) =>
            {
                var aFolder = a.Path.GetSubstringBefore('/');
                var bFolder = b.Path.GetSubstringBefore('/');

                if (aFolder != bFolder)
                {
                    var aFirst = aFolder.GetSubstringBefore(' ');
                    var bFirst = bFolder.GetSubstringBefore(' ');
                    
                    if (aFirst != bFirst)
                        return aFirst == "Static" ? 1 : -1;

                    var aLast = aFolder.GetSubstringAfterLast(' ');
                    var bLast = bFolder.GetSubstringAfterLast(' ');
                    
                    if (aLast != bLast)
                        return aLast == "Methods" ? 1 : -1;
                }

                return string.Compare(a.Path.GetSubstringAfterLast('/'), b.Path.GetSubstringAfterLast('/'), StringComparison.Ordinal);
            });
        }

        private static string GetDisplayText(string name, MethodInfo method)
        {
            if (string.IsNullOrEmpty(name)) return "No Function";
            if (name.IsPropertySetter()) name = name.Substring(4);
            return method != null ? name : $"{name} {{Missing}}";
        }

        private static void OnMethodSelected(MethodInfo prev, MethodInfo next, SerializedProperty prop)
        {
            if (prev == next) return;

            var name = prop.FindPropertyRelative(nameof(PersistentListener._methodName));
            var args = prop.FindPropertyRelative(nameof(PersistentListener._persistentArguments));

            name.stringValue = next.Name;
            
            var pars = next.GetParameters();
            args.arraySize = pars.Length;

            for (int i = 0; i < pars.Length; i++)
                InitArg(args.GetArrayElementAtIndex(i), pars[i].ParameterType, prop);

            PersistentListenerDrawer.ResetListener(prop);
            ExtEventDrawer.ResetListCache(prop.GetParent().GetParent());
        }

        private static void InitArg(SerializedProperty arg, Type type, SerializedProperty listener)
        {
            var target = new SerializedTypeReference(arg.FindPropertyRelative(nameof(PersistentArgument._targetType)));
            target.SetType(type);
            target.SetSuppressLogs(true, false);

            var parent = arg.GetParent()?.GetParent()?.GetParent()?.GetParent();
            var info = ExtEventDrawer.GetOrCreateEventInfo(parent);

            int match = -1;
            bool exact = false;

            for (int i = 0; i < info.ParamTypes.Length; i++)
            {
                var p = info.ParamTypes[i];
                if (p.IsAssignableFrom(type))
                {
                    exact = true;
                    match = i;
                    break;
                }
                if (Converter.ExistsForTypes(p, type))
                {
                    match = i;
                    break;
                }
            }

            bool hasMatch = match != -1;
            
            arg.FindPropertyRelative(nameof(PersistentArgument._isSerialized)).boolValue = !hasMatch;
            arg.FindPropertyRelative(nameof(PersistentArgument._canBeDynamic)).boolValue = hasMatch;

            if (hasMatch)
            {
                arg.FindPropertyRelative(nameof(PersistentArgument._index)).intValue = match;
                
                var from = new SerializedTypeReference(arg.FindPropertyRelative(nameof(PersistentArgument._fromType)));
                from.SetType(exact ? type : info.ParamTypes[match]);
            }
        }
    }
}