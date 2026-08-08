using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;
using PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.UI
{
    /// <summary>알림 프리팹이 선택된 알림 정보를 받을 때 구현합니다.</summary>
    public interface INotificationDotView
    {
        void Show(string key, int count);
        void Hide();
    }

    /// <summary>알림 키와 표시할 프리팹을 연결합니다.</summary>
    internal static class NotificationDotViews
    {
        private sealed class Registration : IDisposable
        {
            private string key;
            private readonly GameObject prefab;

            public Registration(string key, GameObject prefab)
            {
                this.key = key;
                this.prefab = prefab;
            }

            public void Dispose()
            {
                if (key == null)
                    return;

                if (Prefabs.TryGetValue(key, out GameObject current) && current == prefab)
                {
                    Prefabs.Remove(key);
                    Changed?.Invoke();
                }

                key = null;
            }
        }

        private static readonly Dictionary<string, GameObject> Prefabs =
            new(StringComparer.Ordinal);

        internal static event Action Changed;

        internal static IDisposable Register(string key, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Notification key cannot be empty.", nameof(key));
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            key = NotificationDots.NormalizeKey(key);
            if (!Prefabs.TryAdd(key, prefab))
                throw new InvalidOperationException($"Notification view already registered: {key}");

            Changed?.Invoke();
            return new Registration(key, prefab);
        }

        internal static IDisposable Register<TEnum>(TEnum key, GameObject prefab)
            where TEnum : struct, Enum =>
            Register(NotificationDotEnum.GetKey(key), prefab);

        internal static bool TryGetPrefab(string key, out GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                prefab = null;
                return false;
            }

            return Prefabs.TryGetValue(NotificationDots.NormalizeKey(key), out prefab);
        }

        internal static void Clear()
        {
            if (Prefabs.Count == 0)
                return;

            Prefabs.Clear();
            Changed?.Invoke();
        }
    }

    /// <summary>Presenter가 감시할 enum 알림과 표시 우선순위입니다.</summary>
    [Serializable]
    public sealed class NotificationDotTarget
    {
        [SerializeField] private string enumTypeName;
        [SerializeField] private long enumValue;
        [SerializeField] private int priority;

        [NonSerialized] private string cachedTypeName;
        [NonSerialized] private long cachedEnumValue;
        [NonSerialized] private Type cachedType;
        [NonSerialized] private object cachedValue;
        [NonSerialized] private string cachedKey;

        private NotificationDotTarget(Type enumType, object value, int priority)
        {
            SetEnum(enumType, value);
            this.priority = priority;
        }

        internal Type EnumType
        {
            get
            {
                Resolve();
                return cachedType;
            }
        }

        internal object EnumValue
        {
            get
            {
                Resolve();
                return cachedValue;
            }
        }

        internal string Key
        {
            get
            {
                Resolve();
                return cachedKey ?? string.Empty;
            }
        }

        internal string DisplayName
        {
            get
            {
                Resolve();
                return cachedType != null && cachedValue != null
                    ? $"{cachedType.Name}.{cachedValue}"
                    : "Missing Notification";
            }
        }

        internal int Priority => priority;
        internal bool IsValid => !string.IsNullOrWhiteSpace(Key);

        public static NotificationDotTarget Create<TEnum>(TEnum value, int priority = 0)
            where TEnum : struct, Enum =>
            new(typeof(TEnum), value, priority);

        internal static NotificationDotTarget Create(Type enumType, object value, int priority = 0)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));
            if (!enumType.IsEnum)
                throw new ArgumentException("Type must be an enum.", nameof(enumType));
            if (value == null || value.GetType() != enumType)
                throw new ArgumentException("Value must belong to the enum type.", nameof(value));

            return new NotificationDotTarget(enumType, value, priority);
        }

        private void SetEnum(Type enumType, object value)
        {
            enumTypeName = enumType.AssemblyQualifiedName;
            enumValue = ToRawValue(enumType, value);
            InvalidateCache();
        }

        private void Resolve()
        {
            if (cachedTypeName == enumTypeName && cachedEnumValue == enumValue)
                return;

            cachedTypeName = enumTypeName;
            cachedEnumValue = enumValue;
            cachedType = Type.GetType(enumTypeName, throwOnError: false);
            cachedValue = null;
            cachedKey = null;

            if (cachedType == null || !cachedType.IsEnum)
                return;

            cachedValue = Enum.ToObject(cachedType, ToUnderlyingValue(cachedType, enumValue));
            if (!Enum.IsDefined(cachedType, cachedValue))
            {
                cachedValue = null;
                return;
            }

            cachedKey = NotificationDotEnum.GetKey(cachedType, cachedValue);
        }

        private void InvalidateCache()
        {
            cachedTypeName = null;
            cachedType = null;
            cachedValue = null;
            cachedKey = null;
        }

        private static long ToRawValue(Type enumType, object value)
        {
            Type underlying = Enum.GetUnderlyingType(enumType);
            return IsUnsigned(underlying)
                ? unchecked((long)Convert.ToUInt64(value))
                : Convert.ToInt64(value);
        }

        private static object ToUnderlyingValue(Type enumType, long value)
        {
            Type underlying = Enum.GetUnderlyingType(enumType);
            return IsUnsigned(underlying) ? unchecked((ulong)value) : value;
        }

        private static bool IsUnsigned(Type type) =>
            type == typeof(byte)
            || type == typeof(ushort)
            || type == typeof(uint)
            || type == typeof(ulong);
    }
    /// <summary>여러 알림을 감시하고 우선순위가 가장 높은 하나만 표시합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class NotificationDotPresenter : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private NotificationDotTarget[] targets = Array.Empty<NotificationDotTarget>();

        private readonly List<IDisposable> subscriptions = new();
        private readonly HashSet<string> subscribedKeys = new(StringComparer.Ordinal);
        private GameObject viewInstance;
        private GameObject viewPrefab;
        private readonly List<MonoBehaviour> viewBehaviours = new();
        private readonly List<INotificationDotView> viewCallbacks = new();
        private string currentKey;
        private string currentDisplayName;
        private int currentCount;

#if UNITY_EDITOR
        private bool editorRefreshQueued;
#endif

        internal Transform SpawnPoint => spawnPoint;
        internal IReadOnlyList<NotificationDotTarget> Targets => targets;
        internal string CurrentKey => currentKey;
        internal string CurrentDisplayName => currentDisplayName;
        internal int CurrentCount => currentCount;
        internal bool HasVisibleDot =>
            isActiveAndEnabled && gameObject.activeInHierarchy &&
            viewInstance != null && viewInstance.activeSelf;

        private void OnEnable()
        {
            NotificationDotViews.Changed += Refresh;
            Rebind();
        }

        private void OnDisable()
        {
            NotificationDotViews.Changed -= Refresh;
            Unsubscribe();
            HideWhileDisabled();
        }

        private void OnDestroy()
        {
            if (viewInstance != null && gameObject.activeInHierarchy)
                ReleaseView();
            else
                ForgetView();
        }

        public void SetTargets(IEnumerable<NotificationDotTarget> values)
        {
            if (values == null)
            {
                targets = Array.Empty<NotificationDotTarget>();
            }
            else if (values is NotificationDotTarget[] array)
            {
                targets = (NotificationDotTarget[])array.Clone();
            }
            else
            {
                targets = new List<NotificationDotTarget>(values).ToArray();
            }

            if (isActiveAndEnabled)
                Rebind();
        }

        private void Refresh()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            targets ??= Array.Empty<NotificationDotTarget>();
            NotificationDotTarget selected = null;
            int selectedCount = 0;

            for (int i = 0; i < targets.Length; i++)
            {
                NotificationDotTarget target = targets[i];
                if (target == null || string.IsNullOrWhiteSpace(target.Key))
                    continue;

                int count = NotificationDots.GetCount(target.Key);
                if (count <= 0)
                    continue;
                if (selected != null && target.Priority <= selected.Priority)
                    continue;

                selected = target;
                selectedCount = count;
            }

            if (selected == null)
            {
                HideCurrent();
                return;
            }

            Show(selected.Key, selected.DisplayName, selectedCount);
        }

        public bool VisitCurrent() =>
            !string.IsNullOrWhiteSpace(currentKey) && NotificationDots.Visit(currentKey);

        private void Rebind()
        {
            targets ??= Array.Empty<NotificationDotTarget>();
            Unsubscribe();
            subscribedKeys.Clear();

            for (int i = 0; i < targets.Length; i++)
            {
                string key = targets[i]?.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                key = NotificationDots.NormalizeKey(key);
                if (!subscribedKeys.Add(key))
                    continue;

                subscriptions.Add(NotificationDots.Subscribe(key, OnChanged, notifyImmediately: false));
            }

            Refresh();
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < subscriptions.Count; i++)
                subscriptions[i]?.Dispose();

            subscriptions.Clear();
            subscribedKeys.Clear();
        }

        private void OnChanged(NotificationDotChange change)
        {
            Refresh();
        }

        private void Show(string key, string displayName, int count)
        {
            if (!TryResolvePrefab(key, out GameObject prefab))
            {
                HideCurrent();
                return;
            }

            if (viewInstance != null && viewPrefab == prefab &&
                currentKey == key && currentCount == count && viewInstance.activeSelf)
                return;

            if (viewInstance == null || viewPrefab != prefab)
            {
                ReleaseView();
                Transform parent = spawnPoint != null ? spawnPoint : transform;
                viewInstance = PrefabPool.Spawn(prefab, parent);
                viewPrefab = prefab;
                CacheViewCallbacks();
            }

            currentKey = key;
            currentDisplayName = displayName;
            currentCount = count;
            viewInstance.SetActive(true);

            for (int i = 0; i < viewCallbacks.Count; i++)
                viewCallbacks[i].Show(key, count);
        }

        private static bool TryResolvePrefab(string key, out GameObject prefab)
        {
            if (NotificationDotViews.TryGetPrefab(key, out prefab))
                return true;

            string viewKey = NotificationDots.GetViewKey(key);
            return NotificationDotViews.TryGetPrefab(viewKey, out prefab);
        }

        private void HideCurrent()
        {
            ReleaseView();
            ClearCurrent();
        }

        private void HideWhileDisabled()
        {
            NotifyViewHidden();
            if (viewInstance != null && gameObject.activeInHierarchy)
                viewInstance.SetActive(false);

            ClearCurrent();
        }

        private void ClearCurrent()
        {
            currentKey = null;
            currentDisplayName = null;
            currentCount = 0;
        }

        private void CacheViewCallbacks()
        {
            viewBehaviours.Clear();
            viewCallbacks.Clear();
            viewInstance.GetComponentsInChildren(true, viewBehaviours);
            for (int i = 0; i < viewBehaviours.Count; i++)
            {
                if (viewBehaviours[i] is INotificationDotView callback)
                    viewCallbacks.Add(callback);
            }
        }

        private void ReleaseView()
        {
            if (viewInstance != null)
            {
                NotifyViewHidden();

                if (!PrefabPool.Release(viewInstance))
                {
                    if (Application.isPlaying)
                        Destroy(viewInstance);
                    else
                        DestroyImmediate(viewInstance);
                }
            }

            ForgetView();
        }

        private void NotifyViewHidden()
        {
            if (string.IsNullOrEmpty(currentKey))
                return;

            for (int i = 0; i < viewCallbacks.Count; i++)
                viewCallbacks[i].Hide();
        }

        private void ForgetView()
        {
            viewInstance = null;
            viewPrefab = null;
            viewBehaviours.Clear();
            viewCallbacks.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || editorRefreshQueued)
                return;

            editorRefreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshAfterValidate;
        }

        private void RefreshAfterValidate()
        {
            editorRefreshQueued = false;
            if (this != null && Application.isPlaying && isActiveAndEnabled)
                Rebind();
        }
#endif
    }

}