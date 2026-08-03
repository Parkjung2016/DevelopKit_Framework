using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>프로젝트 전역 알림닷 시스템에 접근하는 간단한 진입점입니다.</summary>
    public static class NotificationDots
    {
        private static NotificationDotSystem current = new();

        public static NotificationDotSystem Current => current;

        public static string NormalizeKey(string key) => NotificationDotSystem.NormalizeKey(key);

        public static event Action<NotificationDotChange> Changed
        {
            add => current.Changed += value;
            remove => current.Changed -= value;
        }

        public static int GetCount(string key) => current.GetCount(key);
        public static int GetCount<TEnum>(TEnum key) where TEnum : struct, Enum => current.GetCount(key);
        public static int GetCount<TEnum>() where TEnum : struct, Enum => current.GetCount<TEnum>();

        public static int GetDirectCount(string key) => current.GetDirectCount(key);
        public static int GetDirectCount<TEnum>(TEnum key) where TEnum : struct, Enum =>
            current.GetDirectCount(key);

        public static bool HasCountOverride(string key) => current.HasCountOverride(key);
        public static void SetCountOverride(string key, int count) => current.SetCountOverride(key, count);
        public static void ClearCountOverride(string key) => current.ClearCountOverride(key);

        public static bool IsActive(string key) => current.IsActive(key);
        public static bool IsActive<TEnum>(TEnum key) where TEnum : struct, Enum => current.IsActive(key);
        public static bool IsActive<TEnum>() where TEnum : struct, Enum => current.IsActive<TEnum>();

        public static void SetCount(string key, int count) => current.SetCount(key, count);
        public static void SetCount<TEnum>(TEnum key, int count) where TEnum : struct, Enum =>
            current.SetCount(key, count);

        public static void SetActive(string key, bool active) => current.SetActive(key, active);
        public static void SetActive<TEnum>(TEnum key, bool active) where TEnum : struct, Enum =>
            current.SetActive(key, active);

        public static void Add(string key, int amount = 1) => current.Add(key, amount);
        public static void Add<TEnum>(TEnum key, int amount = 1) where TEnum : struct, Enum =>
            current.Add(key, amount);

        public static void Clear(string key) => current.Clear(key);
        public static void Clear<TEnum>(TEnum key) where TEnum : struct, Enum => current.Clear(key);

        public static bool Visit(string key) => current.Visit(key);
        public static bool Visit<TEnum>(TEnum key) where TEnum : struct, Enum => current.Visit(key);


        public static string GetViewKey(string key) => current.GetViewKey(key);
        public static string GetViewKey<TEnum>(TEnum key) where TEnum : struct, Enum =>
            current.GetViewKey(key);

        public static bool TryGetDefinition(
            string key,
            out NotificationDotDefinition definition) =>
            current.TryGetDefinition(key, out definition);

        public static bool TryGetDefinition<TEnum>(
            TEnum key,
            out NotificationDotDefinition definition)
            where TEnum : struct, Enum =>
            current.TryGetDefinition(key, out definition);

        public static void RegisterEnum<TEnum>() where TEnum : struct, Enum =>
            current.EnsureEnum<TEnum>();

        public static NotificationDotRegistration Register(NotificationDotDefinition definition) =>
            current.Register(definition);

        public static NotificationDotHandle CreateHandle(string key, int initialCount = 0) =>
            current.CreateHandle(key, initialCount);

        public static NotificationDotHandle CreateHandle<TEnum>(TEnum key, int initialCount = 0)
            where TEnum : struct, Enum =>
            current.CreateHandle(key, initialCount);

        public static IDisposable Subscribe(
            string key,
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true) =>
            current.Subscribe(key, callback, notifyImmediately);

        public static IDisposable Subscribe<TEnum>(
            TEnum key,
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true)
            where TEnum : struct, Enum =>
            current.Subscribe(key, callback, notifyImmediately);

        public static IDisposable Subscribe<TEnum>(
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true)
            where TEnum : struct, Enum =>
            current.Subscribe<TEnum>(callback, notifyImmediately);

        public static IDisposable BeginBatch() => current.BeginBatch();
        public static void Reset() => current.Reset();

        public static void GetSnapshot(List<NotificationDotSnapshot> results, bool includeInactive = false) =>
            current.GetSnapshot(results, includeInactive);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            current = new NotificationDotSystem();
        }
    }

    public enum NotificationDotRelation
    {
        Dependency,
        Parent
    }
    /// <summary>Enum 알림의 경로, 동작, UI 표현과 종속성을 설정합니다.</summary>
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public sealed class NotificationDotAttribute : Attribute
    {
        public NotificationDotAttribute()
        {
        }

        public NotificationDotAttribute(
            object dependsOn,
            NotificationDotDependencyMode dependencyMode = NotificationDotDependencyMode.Active)
        {
            if (dependsOn == null)
                throw new ArgumentNullException(nameof(dependsOn));

            Type type = dependsOn.GetType();
            if (!type.IsEnum)
                throw new ArgumentException("Dependency must be an enum value.", nameof(dependsOn));

            DependencyEnumType = type;
            DependencyName = dependsOn.ToString();
            DependencyMode = dependencyMode;
        }

        public NotificationDotAttribute(
            Type dependencyEnumType,
            string dependencyName,
            NotificationDotDependencyMode dependencyMode = NotificationDotDependencyMode.Active)
        {
            if (dependencyEnumType == null)
                throw new ArgumentNullException(nameof(dependencyEnumType));
            if (!dependencyEnumType.IsEnum)
                throw new ArgumentException("Dependency type must be an enum.", nameof(dependencyEnumType));
            if (string.IsNullOrWhiteSpace(dependencyName))
                throw new ArgumentException("Dependency name cannot be empty.", nameof(dependencyName));

            DependencyEnumType = dependencyEnumType;
            DependencyName = dependencyName;
            DependencyMode = dependencyMode;
        }

        public NotificationDotAttribute(
            string dependencyKey,
            NotificationDotDependencyMode dependencyMode = NotificationDotDependencyMode.Active)
        {
            DependencyKey = NotificationDotSystem.NormalizeKey(dependencyKey);
            DependencyMode = dependencyMode;
        }


        /// <summary>현재 Enum 값을 포함할 부모 값의 이름입니다.</summary>
        public string Parent { get; set; }

        /// <summary>부모가 다른 Enum에 있을 때 타입을 명시합니다.</summary>
        public Type ParentType { get; set; }


        /// <summary>방문하거나 상호작용하면 현재 발생분을 숨깁니다.</summary>
        public bool ClearOnVisit { get; set; }

        /// <summary>UI 프리팹이나 Addressable을 찾을 표현 키입니다.</summary>
        public string ViewKey { get; set; }

        /// <summary>생성자로 전달한 enum 값의 관계를 지정합니다.</summary>
        public NotificationDotRelation Relation { get; set; }

        public NotificationDotDependencyMode DependencyMode { get; }

        internal Type DependencyEnumType { get; }
        internal string DependencyName { get; }
        internal string DependencyKey { get; }
        internal bool HasDependency => Relation == NotificationDotRelation.Dependency
            && (!string.IsNullOrWhiteSpace(DependencyKey) || DependencyEnumType != null);
        internal bool HasTypedParent => Relation == NotificationDotRelation.Parent
            && DependencyEnumType != null;
    }
    /// <summary>Enum 타입과 값을 알림 정의로 변환하고 타입별로 캐시합니다.</summary>
    public static class NotificationDotEnum
    {
        private sealed class TypeMap
        {
            public TypeMap(string typeKey, Dictionary<object, string> keys)
            {
                TypeKey = typeKey;
                Keys = keys;
            }

            public string TypeKey { get; }
            public Dictionary<object, string> Keys { get; }
            public Dictionary<object, NotificationDotDefinition> Definitions { get; set; }
            public IReadOnlyList<NotificationDotDefinition> DefinitionList { get; set; }
            public IReadOnlyList<string> RootKeys { get; set; }
        }

        private static readonly object CacheLock = new();
        private static readonly Dictionary<Type, TypeMap> TypeMaps = new();
        private static readonly HashSet<Type> BuildingTypes = new();

        public static string GetTypeKey<TEnum>() where TEnum : struct, Enum =>
            NotificationDotEnumCache<TEnum>.TypeKey;

        public static string GetKey<TEnum>(TEnum value) where TEnum : struct, Enum =>
            NotificationDotEnumCache<TEnum>.GetKey(value);

        public static NotificationDotDefinition GetDefinition<TEnum>(TEnum value)
            where TEnum : struct, Enum =>
            NotificationDotEnumCache<TEnum>.GetDefinition(value);

        public static IReadOnlyList<NotificationDotDefinition> GetDefinitions<TEnum>()
            where TEnum : struct, Enum =>
            NotificationDotEnumCache<TEnum>.Definitions;

        public static string GetTypeKey(Type enumType) => GetTypeMap(enumType).TypeKey;

        internal static IReadOnlyList<string> GetRootKeys<TEnum>() where TEnum : struct, Enum =>
            GetTypeMap(typeof(TEnum)).RootKeys;

        public static string GetKey(Type enumType, object value)
        {
            ValidateValue(enumType, value);
            TypeMap map = GetTypeMap(enumType);
            if (map.Keys.TryGetValue(value, out string key))
                return key;

            throw UndefinedValue(value);
        }

        public static NotificationDotDefinition GetDefinition(Type enumType, object value)
        {
            ValidateValue(enumType, value);
            TypeMap map = GetTypeMap(enumType);
            if (map.Definitions.TryGetValue(value, out NotificationDotDefinition definition))
                return definition;

            throw UndefinedValue(value);
        }

        internal static string BuildTypeKey(Type enumType)
        {
            ValidateEnumType(enumType);
            string typeName = enumType.FullName ?? enumType.Name;
            return NotificationDotSystem.NormalizeKey(typeName.Replace('+', '.'));
        }

        internal static TypeMapData GetCachedData(Type enumType)
        {
            TypeMap map = GetTypeMap(enumType);
            return new TypeMapData(map.TypeKey, map.Keys, map.Definitions, map.DefinitionList);
        }

        private static TypeMap GetTypeMap(Type enumType)
        {
            ValidateEnumType(enumType);

            lock (CacheLock)
            {
                if (TypeMaps.TryGetValue(enumType, out TypeMap cached))
                    return cached;
                if (!BuildingTypes.Add(enumType))
                {
                    throw new InvalidOperationException(
                        string.Concat("Notification parent cycle detected across enum types: ", enumType.FullName, "."));
                }

                try
                {
                    string typeKey = BuildTypeKey(enumType);
                    Dictionary<object, string> keys = BuildKeys(enumType, typeKey);
                    var map = new TypeMap(typeKey, keys)
                    {
                        RootKeys = BuildRootKeys(keys)
                    };
                    TypeMaps.Add(enumType, map);

                    try
                    {
                        BuildDefinitions(enumType, map);
                        return map;
                    }
                    catch
                    {
                        TypeMaps.Remove(enumType);
                        throw;
                    }
                }
                finally
                {
                    BuildingTypes.Remove(enumType);
                }
            }
        }
        private static Dictionary<object, string> BuildKeys(Type enumType, string typeKey)
        {
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            var fieldsByName = new Dictionary<string, FieldInfo>(fields.Length, StringComparer.Ordinal);
            var pathsByName = new Dictionary<string, string>(fields.Length, StringComparer.Ordinal);
            var resolving = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < fields.Length; i++)
                fieldsByName[fields[i].Name] = fields[i];

            string ResolvePath(string name)
            {
                if (pathsByName.TryGetValue(name, out string cached))
                    return cached;
                if (!fieldsByName.TryGetValue(name, out FieldInfo field))
                {
                    throw new InvalidOperationException(
                        string.Concat("Notification value '", name, "' does not exist in ", enumType.FullName, "."));
                }
                if (!resolving.Add(name))
                {
                    throw new InvalidOperationException(
                        string.Concat("Notification parent cycle detected in ", enumType.FullName, "."));
                }

                NotificationDotAttribute parent = field.GetCustomAttributes<NotificationDotAttribute>()
                    .FirstOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.Parent)
                        || attribute.HasTypedParent);
                string path;
                if (parent == null)
                {
                    path = string.Concat(typeKey, "/", name);
                }
                else
                {
                    string parentName = GetParentName(parent);
                    Type parentType = ResolveParentType(enumType, fieldsByName, parent, parentName);
                    string parentKey;
                    if (parentType == enumType)
                    {
                        parentKey = ResolvePath(parentName);
                    }
                    else
                    {
                        object parentValue = Enum.Parse(parentType, parentName, ignoreCase: false);
                        parentKey = GetKey(parentType, parentValue);
                    }

                    path = string.Concat(parentKey, "/", name);
                }

                resolving.Remove(name);
                pathsByName.Add(name, path);
                return path;
            }

            var result = new Dictionary<object, string>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                result[field.GetValue(null)] = ResolvePath(field.Name);
            }

            return result;
        }

        private static string GetParentName(NotificationDotAttribute attribute) =>
            attribute.HasTypedParent ? attribute.DependencyName : attribute.Parent;

        private static Type ResolveParentType(
            Type childEnumType,
            IReadOnlyDictionary<string, FieldInfo> childFields,
            NotificationDotAttribute parent,
            string parentName)
        {
            if (parent.HasTypedParent)
            {
                ValidateParentValue(parent.DependencyEnumType, parentName);
                return parent.DependencyEnumType;
            }

            if (parent.ParentType != null)
            {
                ValidateEnumType(parent.ParentType);
                ValidateParentValue(parent.ParentType, parentName);
                return parent.ParentType;
            }

            if (childFields.ContainsKey(parentName))
                return childEnumType;

            Type match = null;
            bool includeTestAssemblies = IsTestAssembly(childEnumType.Assembly);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!includeTestAssemblies && IsTestAssembly(assembly))
                    continue;
                Type[] types = GetLoadableTypes(assembly);
                for (int i = 0; i < types.Length; i++)
                {
                    Type candidate = types[i];
                    if (candidate == null || !candidate.IsEnum || candidate == childEnumType)
                        continue;
                    if (!candidate.IsDefined(typeof(NotificationDotAttribute), inherit: false))
                        continue;
                    if (candidate.GetField(parentName, BindingFlags.Public | BindingFlags.Static) == null)
                        continue;
                    if (match != null && match != candidate)
                    {
                        throw new InvalidOperationException(
                            string.Concat(
                                "Notification parent '", parentName,
                                "' exists in more than one enum. Set ParentType explicitly on ",
                                childEnumType.FullName, "."));
                    }

                    match = candidate;
                }
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Notification parent '", parentName,
                        "' was not found. Add [NotificationDot] to its enum or set ParentType explicitly."));
            }

            return match;
        }

        private static void ValidateParentValue(Type parentType, string parentName)
        {
            if (parentType.GetField(parentName, BindingFlags.Public | BindingFlags.Static) == null)
            {
                throw new InvalidOperationException(
                    string.Concat("Notification parent '", parentName, "' does not exist in ", parentType.FullName, "."));
            }
        }

        private static bool IsTestAssembly(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name.EndsWith(".Tests", StringComparison.Ordinal)
                || name.EndsWith("Tests", StringComparison.Ordinal);
        }
        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static IReadOnlyList<string> BuildRootKeys(Dictionary<object, string> keys)
        {
            var allKeys = new HashSet<string>(keys.Values, StringComparer.Ordinal);
            var roots = new List<string>(keys.Count);
            var added = new HashSet<string>(StringComparer.Ordinal);

            foreach (string key in keys.Values)
            {
                int separator = key.LastIndexOf('/');
                string parentKey = separator > 0 ? key.Substring(0, separator) : string.Empty;
                if (!allKeys.Contains(parentKey) && added.Add(key))
                    roots.Add(key);
            }

            return roots;
        }
        private static void BuildDefinitions(Type enumType, TypeMap map)
        {
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            bool defaultClearOnVisit =
                enumType.GetCustomAttributes<NotificationDotAttribute>()
                    .Any(attribute => attribute.ClearOnVisit);
            string defaultViewKey =
                enumType.GetCustomAttributes<NotificationDotAttribute>()
                    .FirstOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.ViewKey))?.ViewKey ?? string.Empty;
            var definitions = new Dictionary<object, NotificationDotDefinition>(fields.Length);
            var definitionList = new List<NotificationDotDefinition>(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(null);
                var definition = new NotificationDotDefinition(map.Keys[value]);

                if (defaultClearOnVisit
                    || field.GetCustomAttributes<NotificationDotAttribute>()
                        .Any(attribute => attribute.ClearOnVisit))
                {
                    definition.ClearOnVisit();
                }

                string viewKey = field.GetCustomAttributes<NotificationDotAttribute>()
                    .FirstOrDefault(attribute => !string.IsNullOrWhiteSpace(attribute.ViewKey))?.ViewKey;
                definition.UseView(viewKey ?? defaultViewKey);

                foreach (NotificationDotAttribute dependency in
                         field.GetCustomAttributes<NotificationDotAttribute>())
                {
                    if (!dependency.HasDependency)
                        continue;
                    string sourceKey = dependency.DependencyKey;
                    if (string.IsNullOrWhiteSpace(sourceKey))
                    {
                        object sourceValue = Enum.Parse(
                            dependency.DependencyEnumType,
                            dependency.DependencyName,
                            ignoreCase: false);
                        sourceKey = GetKey(dependency.DependencyEnumType, sourceValue);
                    }

                    definition.DependsOn(sourceKey, dependency.DependencyMode);
                }

                NotificationDotDefinition frozen = definition.FreezeCopy();
                definitions[value] = frozen;
                definitionList.Add(frozen);
            }

            map.Definitions = definitions;
            map.DefinitionList = definitionList;
        }

        private static void ValidateValue(Type enumType, object value)
        {
            ValidateEnumType(enumType);
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.GetType() != enumType)
            {
                throw new ArgumentException(
                    string.Concat("Value must be a ", enumType.FullName, " enum."),
                    nameof(value));
            }
        }

        private static void ValidateEnumType(Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));
            if (!enumType.IsEnum)
                throw new ArgumentException("Type must be an enum.", nameof(enumType));
        }

        private static ArgumentOutOfRangeException UndefinedValue(object value) =>
            new(
                nameof(value),
                value,
                "Defined enum values can be used as notification keys.");

        internal readonly struct TypeMapData
        {
            public TypeMapData(
                string typeKey,
                Dictionary<object, string> keys,
                Dictionary<object, NotificationDotDefinition> definitions,
                IReadOnlyList<NotificationDotDefinition> definitionList)
            {
                TypeKey = typeKey;
                Keys = keys;
                Definitions = definitions;
                DefinitionList = definitionList;
            }

            public string TypeKey { get; }
            public Dictionary<object, string> Keys { get; }
            public Dictionary<object, NotificationDotDefinition> Definitions { get; }
            public IReadOnlyList<NotificationDotDefinition> DefinitionList { get; }
        }
    }

    internal static class NotificationDotEnumCache<TEnum> where TEnum : struct, Enum
    {
        private static readonly NotificationDotEnum.TypeMapData Data =
            NotificationDotEnum.GetCachedData(typeof(TEnum));
        private static readonly Dictionary<TEnum, string> Keys = BuildKeys();
        private static readonly Dictionary<TEnum, NotificationDotDefinition> DefinitionMap =
            BuildDefinitions();

        public static string TypeKey => Data.TypeKey;
        public static IReadOnlyList<NotificationDotDefinition> Definitions => Data.DefinitionList;

        public static string GetKey(TEnum value)
        {
            if (Keys.TryGetValue(value, out string key))
                return key;

            throw new ArgumentOutOfRangeException(
                nameof(value), value, "Defined enum values can be used as notification keys.");
        }

        public static NotificationDotDefinition GetDefinition(TEnum value)
        {
            if (DefinitionMap.TryGetValue(value, out NotificationDotDefinition definition))
                return definition;

            throw new ArgumentOutOfRangeException(
                nameof(value), value, "Defined enum values can be used as notification keys.");
        }

        private static Dictionary<TEnum, string> BuildKeys()
        {
            var result = new Dictionary<TEnum, string>(Data.Keys.Count);
            foreach (KeyValuePair<object, string> pair in Data.Keys)
                result[(TEnum)pair.Key] = pair.Value;
            return result;
        }

        private static Dictionary<TEnum, NotificationDotDefinition> BuildDefinitions()
        {
            var result = new Dictionary<TEnum, NotificationDotDefinition>(Data.Definitions.Count);
            foreach (KeyValuePair<object, NotificationDotDefinition> pair in Data.Definitions)
                result[(TEnum)pair.Key] = pair.Value;
            return result;
        }
    }
}
