using System;
using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.Runtime
{
    /// <summary>
    /// 경로 형태의 알림 키를 계층으로 관리하고 자식 개수를 부모에 자동으로 합산합니다.
    /// 게임의 메인 스레드에서 사용하도록 설계되었습니다.
    /// </summary>
    internal sealed class NotificationDotSystem
    {
        private sealed class Node
        {
            public Node(string key, Node parent)
            {
                Key = key;
                Parent = parent;
            }

            public readonly string Key;
            public readonly Node Parent;
            public int ManualCount;

            public long DependencyCount;
            public int? CountOverride;
            public long TotalCount;
            public long AcknowledgedCount;
            public Action<NotificationDotChange> Changed;

            public long RawDirectCount => ManualCount + DependencyCount;
            public long UnreadDirectCount => Math.Max(0, RawDirectCount - AcknowledgedCount);
            public long DirectCount => CountOverride ?? UnreadDirectCount;
        }

        private sealed class DependencyLink
        {
            public Node Source;
            public Node Target;
            public NotificationDotDependencyMode Mode;
            public long Contribution;
        }

        private sealed class DefinitionRecord
        {
            public NotificationDotDefinition Definition;
            public bool Permanent;
            public long RegistrationId;
            public readonly List<DependencyLink> Links = new();
        }

        private sealed class Subscription : IDisposable
        {
            private Node node;
            private Action<NotificationDotChange> callback;

            public Subscription(Node node, Action<NotificationDotChange> callback)
            {
                this.node = node;
                this.callback = callback;
            }

            public void Dispose()
            {
                if (node == null)
                    return;

                node.Changed -= callback;
                node = null;
                callback = null;
            }
        }

        private sealed class CompositeSubscription : IDisposable
        {
            private List<IDisposable> subscriptions;

            public CompositeSubscription(List<IDisposable> subscriptions)
            {
                this.subscriptions = subscriptions;
            }

            public void Dispose()
            {
                if (subscriptions == null)
                    return;

                for (int i = subscriptions.Count - 1; i >= 0; i--)
                    subscriptions[i]?.Dispose();

                subscriptions = null;
            }
        }
        private sealed class BatchScope : IDisposable
        {
            private NotificationDotSystem owner;

            public BatchScope(NotificationDotSystem owner)
            {
                this.owner = owner;
                owner.batchDepth++;
            }

            public void Dispose()
            {
                if (owner == null)
                    return;

                NotificationDotSystem current = owner;
                owner = null;
                current.EndBatch();
            }
        }

        private readonly Dictionary<string, Node> nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DefinitionRecord> definitions = new(StringComparer.Ordinal);
        private readonly Dictionary<Node, List<DependencyLink>> linksBySource = new();
        private readonly HashSet<Type> registeredEnumTypes = new();
        private readonly Node root = new(string.Empty, null);
        private Dictionary<Node, int> batchPreviousCounts;
        private int batchDepth;

        private int definitionGeneration;
        private long nextRegistrationId;

        internal event Action<NotificationDotChange> Changed;

        internal int RegisteredKeyCount => nodes.Count;
        internal int RegisteredDefinitionCount => definitions.Count;

        internal int GetCount(string key)
        {
            key = NormalizeKey(key);
            return nodes.TryGetValue(key, out Node node) ? ToPublicCount(node.TotalCount) : 0;
        }

        internal int GetCount<TEnum>(TEnum key) where TEnum : struct, Enum =>
            GetCount(EnsureEnumKey(key));

        internal int GetCount<TEnum>() where TEnum : struct, Enum
        {
            EnsureEnum<TEnum>();
            IReadOnlyList<string> rootKeys = NotificationDotEnum.GetRootKeys<TEnum>();
            long total = 0;
            for (int i = 0; i < rootKeys.Count; i++)
            {
                total += GetCount(rootKeys[i]);
                if (total >= int.MaxValue)
                    return int.MaxValue;
            }

            return (int)total;
        }

        internal int GetDirectCount(string key)
        {
            key = NormalizeKey(key);
            return nodes.TryGetValue(key, out Node node) ? ToPublicCount(node.DirectCount) : 0;
        }

        internal int GetDirectCount<TEnum>(TEnum key) where TEnum : struct, Enum =>
            GetDirectCount(EnsureEnumKey(key));

        /// <summary>현재 키에 표시값 override가 적용되어 있는지 확인합니다.</summary>
        internal bool HasCountOverride(string key)
        {
            key = NormalizeKey(key);
            return nodes.TryGetValue(key, out Node node) && node.CountOverride.HasValue;
        }

        /// <summary>원래 값은 유지하면서 현재 표시값만 바꿉니다.</summary>
        internal void SetCountOverride(string key, int count)
        {
            key = NormalizeKey(key);
            Node node = GetOrCreateNode(key);
            count = Math.Max(0, count);

            long previousDirect = node.DirectCount;
            node.CountOverride = count;
            ApplyDelta(node, node.DirectCount - previousDirect);
        }

        /// <summary>표시값 override를 해제하고 원래 런타임 값으로 돌아갑니다.</summary>
        internal void ClearCountOverride(string key)
        {
            key = NormalizeKey(key);
            if (!nodes.TryGetValue(key, out Node node) || !node.CountOverride.HasValue)
                return;

            long previousDirect = node.DirectCount;
            node.CountOverride = null;
            ApplyDelta(node, node.DirectCount - previousDirect);
        }

        internal bool IsActive(string key) => GetCount(key) > 0;

        internal bool IsActive<TEnum>(TEnum key) where TEnum : struct, Enum => GetCount(key) > 0;

        internal bool IsActive<TEnum>() where TEnum : struct, Enum => GetCount<TEnum>() > 0;

        internal void SetCount(string key, int count)
        {
            key = NormalizeKey(key);
            SetManualCount(GetOrCreateNode(key), count);
        }

        internal void SetCount<TEnum>(TEnum key, int count) where TEnum : struct, Enum =>
            SetCount(EnsureEnumKey(key), count);

        internal void SetActive(string key, bool active) => SetCount(key, active ? 1 : 0);

        internal void SetActive<TEnum>(TEnum key, bool active) where TEnum : struct, Enum =>
            SetCount(key, active ? 1 : 0);

        internal void Add(string key, int amount = 1)
        {
            if (amount == 0)
                return;

            key = NormalizeKey(key);
            Node node = GetOrCreateNode(key);
            long next = (long)node.ManualCount + amount;
            int count = next <= 0 ? 0 : next >= int.MaxValue ? int.MaxValue : (int)next;
            SetManualCount(node, count);
        }

        internal void Add<TEnum>(TEnum key, int amount = 1) where TEnum : struct, Enum =>
            Add(EnsureEnumKey(key), amount);

        internal void Remove(string key, int amount = 1)
        {
            if (amount <= 0)
                return;

            Add(key, -amount);
        }

        internal void Remove<TEnum>(TEnum key, int amount = 1) where TEnum : struct, Enum =>
            Remove(EnsureEnumKey(key), amount);

        internal void Clear(string key) => SetCount(key, 0);

        internal void Clear<TEnum>(TEnum key) where TEnum : struct, Enum => SetCount(key, 0);

        /// <summary>현재 개수를 확인 처리하고 이후에 추가되는 값만 다시 표시합니다.</summary>
        internal bool Visit(string key)
        {
            key = NormalizeKey(key);
            if (!definitions.TryGetValue(key, out DefinitionRecord record)
                || !record.Definition.ClearsOnVisit
                || !nodes.TryGetValue(key, out Node node)
                || node.DirectCount <= 0)
            {
                return false;
            }

            long previousDirect = node.DirectCount;
            node.CountOverride = null;
            node.AcknowledgedCount = node.RawDirectCount;
            ApplyDelta(node, node.DirectCount - previousDirect);
            return true;
        }

        internal bool Visit<TEnum>(TEnum key) where TEnum : struct, Enum =>
            Visit(EnsureEnumKey(key));


        internal NotificationDotRegistration Register(NotificationDotDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            NotificationDotDefinition frozen = definition.FreezeCopy();
            if (definitions.ContainsKey(frozen.Key))
            {
                throw new InvalidOperationException(
                    string.Concat("Notification definition already registered: ", frozen.Key));
            }

            long id = ++nextRegistrationId;
            AddDefinition(frozen, permanent: false, id);
            return new NotificationDotRegistration(this, frozen.Key, id, definitionGeneration);
        }

        internal bool TryGetDefinition(string key, out NotificationDotDefinition definition)
        {
            key = NormalizeKey(key);
            if (definitions.TryGetValue(key, out DefinitionRecord record))
            {
                definition = record.Definition;
                return true;
            }

            definition = null;
            return false;
        }

        internal bool TryGetDefinition<TEnum>(TEnum key, out NotificationDotDefinition definition)
            where TEnum : struct, Enum =>
            TryGetDefinition(EnsureEnumKey(key), out definition);

        internal string GetViewKey(string key) =>
            TryGetDefinition(key, out NotificationDotDefinition definition)
                ? definition.ViewKey
                : string.Empty;

        internal string GetViewKey<TEnum>(TEnum key) where TEnum : struct, Enum =>
            GetViewKey(EnsureEnumKey(key));

        private void EnsureEnum<TEnum>() where TEnum : struct, Enum
        {
            Type enumType = typeof(TEnum);
            if (!registeredEnumTypes.Add(enumType))
                return;

            try
            {
                IReadOnlyList<NotificationDotDefinition> enumDefinitions =
                    NotificationDotEnum.GetDefinitions<TEnum>();
                for (int i = 0; i < enumDefinitions.Count; i++)
                    EnsureDefinition(enumDefinitions[i]);
            }
            catch
            {
                registeredEnumTypes.Remove(enumType);
                throw;
            }
        }

        internal IDisposable Subscribe(
            string key,
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            key = NormalizeKey(key);
            Node node = GetOrCreateNode(key);
            node.Changed += callback;

            if (notifyImmediately)
            {
                int count = ToPublicCount(node.TotalCount);
                callback(new NotificationDotChange(key, count, count));
            }

            return new Subscription(node, callback);
        }

        internal IDisposable Subscribe<TEnum>(
            TEnum key,
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true)
            where TEnum : struct, Enum =>
            Subscribe(EnsureEnumKey(key), callback, notifyImmediately);

        internal IDisposable Subscribe<TEnum>(
            Action<NotificationDotChange> callback,
            bool notifyImmediately = true)
            where TEnum : struct, Enum
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            EnsureEnum<TEnum>();
            IReadOnlyList<string> rootKeys = NotificationDotEnum.GetRootKeys<TEnum>();
            var subscriptions = new List<IDisposable>(rootKeys.Count);
            string typeKey = NotificationDotEnum.GetTypeKey<TEnum>();
            int lastCount = GetCount<TEnum>();

            void OnRootChanged(NotificationDotChange _)
            {
                int count = GetCount<TEnum>();
                if (count == lastCount)
                    return;

                int previous = lastCount;
                lastCount = count;
                callback(new NotificationDotChange(typeKey, previous, count));
            }

            for (int i = 0; i < rootKeys.Count; i++)
                subscriptions.Add(Subscribe(rootKeys[i], OnRootChanged, notifyImmediately: false));

            if (notifyImmediately)
                callback(new NotificationDotChange(typeKey, lastCount, lastCount));

            return new CompositeSubscription(subscriptions);
        }

        /// <summary>여러 변경을 묶어 최종 결과를 한 번만 알립니다.</summary>
        internal IDisposable BeginBatch() => new BatchScope(this);

        /// <summary>모든 알림 개수를 지웁니다.</summary>
        internal void Reset()
        {

            using (BeginBatch())
            {
                foreach (Node node in nodes.Values)
                {
                    long previousDirect = node.DirectCount;
                    node.ManualCount = 0;

                    node.CountOverride = null;
                    node.AcknowledgedCount = 0;
                    ApplyDelta(node, node.DirectCount - previousDirect);
                }
            }
        }

        internal void GetSnapshot(List<NotificationDotSnapshot> results, bool includeInactive = false)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            foreach (Node node in nodes.Values)
            {
                int count = ToPublicCount(node.TotalCount);
                if (!includeInactive && count == 0)
                    continue;

                results.Add(new NotificationDotSnapshot(
                    node.Key,
                    ToPublicCount(node.DirectCount),
                    count));
            }

            results.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        }


        internal void UnregisterDefinition(string key, long id, int registrationGeneration)
        {
            if (registrationGeneration != definitionGeneration
                || !definitions.TryGetValue(key, out DefinitionRecord record)
                || record.Permanent
                || record.RegistrationId != id)
            {
                return;
            }

            using (BeginBatch())
            {
                for (int i = record.Links.Count - 1; i >= 0; i--)
                    RemoveLink(record.Links[i]);
            }

            definitions.Remove(key);
        }

        private string EnsureEnumKey<TEnum>(TEnum key) where TEnum : struct, Enum
        {
            EnsureEnum<TEnum>();
            return NotificationDotEnum.GetKey(key);
        }

        private void EnsureDefinition(NotificationDotDefinition definition)
        {
            if (definitions.ContainsKey(definition.Key))
                return;

            AddDefinition(definition.FreezeCopy(), permanent: true, registrationId: 0);
        }

        private void AddDefinition(
            NotificationDotDefinition definition,
            bool permanent,
            long registrationId)
        {
            Node target = GetOrCreateNode(definition.Key);
            var sources = new List<(Node node, NotificationDotDependency dependency)>(
                definition.Dependencies.Count);
            var uniqueSources = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < definition.Dependencies.Count; i++)
            {
                NotificationDotDependency dependency = definition.Dependencies[i];
                if (!uniqueSources.Add(dependency.SourceKey))
                {
                    throw new InvalidOperationException(
                        string.Concat("Duplicate notification dependency: ", dependency.SourceKey));
                }

                Node source = GetOrCreateNode(dependency.SourceKey);
                if (CanReach(target, source))
                {
                    throw new InvalidOperationException(
                        string.Concat(
                            "Notification dependency cycle: ",
                            dependency.SourceKey,
                            " -> ",
                            definition.Key));
                }

                sources.Add((source, dependency));
            }

            var record = new DefinitionRecord
            {
                Definition = definition,
                Permanent = permanent,
                RegistrationId = registrationId
            };
            definitions.Add(definition.Key, record);

            using (BeginBatch())
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    (Node source, NotificationDotDependency dependency) = sources[i];
                    var link = new DependencyLink
                    {
                        Source = source,
                        Target = target,
                        Mode = dependency.Mode,
                        Contribution = GetDependencyContribution(source.TotalCount, dependency.Mode)
                    };

                    if (!linksBySource.TryGetValue(source, out List<DependencyLink> links))
                    {
                        links = new List<DependencyLink>();
                        linksBySource.Add(source, links);
                    }

                    links.Add(link);
                    record.Links.Add(link);
                    ChangeDependencyCount(target, 0, link.Contribution);
                }
            }
        }

        private void RemoveLink(DependencyLink link)
        {
            if (linksBySource.TryGetValue(link.Source, out List<DependencyLink> links))
            {
                links.Remove(link);
                if (links.Count == 0)
                    linksBySource.Remove(link.Source);
            }

            ChangeDependencyCount(link.Target, link.Contribution, 0);
        }

        private bool CanReach(Node start, Node target)
        {
            var visited = new HashSet<Node>();
            var stack = new Stack<Node>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                Node current = stack.Pop();
                if (!visited.Add(current))
                    continue;
                if (current == target)
                    return true;

                if (current.Parent != null && current.Parent != root)
                    stack.Push(current.Parent);

                if (!linksBySource.TryGetValue(current, out List<DependencyLink> links))
                    continue;

                for (int i = 0; i < links.Count; i++)
                    stack.Push(links[i].Target);
            }

            return false;
        }

        private Node GetOrCreateNode(string key)
        {
            if (nodes.TryGetValue(key, out Node existing))
                return existing;

            int separator = key.LastIndexOf('/');
            Node parent = separator < 0 ? root : GetOrCreateNode(key.Substring(0, separator));
            var node = new Node(key, parent);
            nodes.Add(key, node);
            return node;
        }

        private void ChangeDependencyCount(Node node, long previous, long count)
        {
            if (previous == count)
                return;

            long previousDirect = node.DirectCount;
            node.DependencyCount += count - previous;
            ClampAcknowledgedCount(node);

            ApplyDelta(node, node.DirectCount - previousDirect);
        }

        private void SetManualCount(Node node, int count)
        {
            count = Math.Max(0, count);
            bool startsNewOccurrence =
                node.ManualCount == count && count > 0 && node.AcknowledgedCount > 0;
            if (node.ManualCount == count && !startsNewOccurrence)
                return;

            long previousDirect = node.DirectCount;
            node.ManualCount = count;
            if (startsNewOccurrence)
                node.AcknowledgedCount = 0;
            else
                ClampAcknowledgedCount(node);

            ApplyDelta(node, node.DirectCount - previousDirect);
        }

        private static void ClampAcknowledgedCount(Node node)
        {
            if (node.AcknowledgedCount > node.RawDirectCount)
                node.AcknowledgedCount = Math.Max(0, node.RawDirectCount);
        }

        private void ApplyDelta(Node node, long delta)
        {
            if (delta == 0)
                return;

            for (Node current = node; current != null && current != root; current = current.Parent)
            {
                int previous = ToPublicCount(current.TotalCount);
                if (batchDepth > 0)
                {
                    batchPreviousCounts ??= new Dictionary<Node, int>();
                    if (!batchPreviousCounts.ContainsKey(current))
                        batchPreviousCounts.Add(current, previous);
                }

                current.TotalCount += delta;
                if (current.TotalCount < 0)
                    current.TotalCount = 0;

                int count = ToPublicCount(current.TotalCount);
                UpdateDependencyLinks(current, count);
            }

            if (batchDepth > 0)
                return;

            for (Node current = node; current != null && current != root; current = current.Parent)
            {
                int count = ToPublicCount(current.TotalCount);
                int previous = ToPublicCount(current.TotalCount - delta);
                Publish(current, previous, count);
            }
        }

        private void UpdateDependencyLinks(Node source, int count)
        {
            if (!linksBySource.TryGetValue(source, out List<DependencyLink> links))
                return;

            for (int i = 0; i < links.Count; i++)
            {
                DependencyLink link = links[i];
                long previous = link.Contribution;
                long next = GetDependencyContribution(count, link.Mode);
                link.Contribution = next;
                ChangeDependencyCount(link.Target, previous, next);
            }
        }

        private void EndBatch()
        {
            if (batchDepth <= 0)
                return;

            batchDepth--;
            if (batchDepth > 0 || batchPreviousCounts == null)
                return;

            foreach (KeyValuePair<Node, int> pair in batchPreviousCounts)
                Publish(pair.Key, pair.Value, ToPublicCount(pair.Key.TotalCount));

            batchPreviousCounts.Clear();
        }

        private void Publish(Node node, int previous, int count)
        {
            if (previous == count)
                return;

            var change = new NotificationDotChange(node.Key, previous, count);
            node.Changed?.Invoke(change);
            Changed?.Invoke(change);
        }

        private static long GetDependencyContribution(
            long count,
            NotificationDotDependencyMode mode) =>
            mode == NotificationDotDependencyMode.Count
                ? Math.Max(0, count)
                : count > 0 ? 1 : 0;

        internal static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Notification key cannot be empty.", nameof(key));

            string normalized = key.Trim().Replace('\\', '/').Trim('/');
            while (normalized.Contains("//", StringComparison.Ordinal))
                normalized = normalized.Replace("//", "/");

            if (normalized.Length == 0)
                throw new ArgumentException("Notification key cannot be empty.", nameof(key));

            return normalized;
        }

        private static int ToPublicCount(long count) =>
            count <= 0 ? 0 : count >= int.MaxValue ? int.MaxValue : (int)count;
    }
}
