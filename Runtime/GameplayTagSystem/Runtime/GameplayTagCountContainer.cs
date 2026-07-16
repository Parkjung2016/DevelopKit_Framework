using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    public delegate void OnTagCountChangedDelegate(GameplayTag gameplayTag, int newCount);

    public enum GameplayTagEventType
    {
        NewOrRemoved,
        AnyCountChange
    }

    internal readonly struct DeferredTagChangedDelegate
    {
        public readonly GameplayTag Tag;
        public readonly int Count;
        public readonly OnTagCountChangedDelegate Callback;

        public DeferredTagChangedDelegate(GameplayTag tag, int count, OnTagCountChangedDelegate callback)
        {
            Tag = tag;
            Count = count;
            Callback = callback;
        }

        public void Execute()
        {
            Callback(Tag, Count);
        }
    }

    internal struct GameplayTagCountEntry
    {
        public int ExplicitCount;
        public int TotalCount;
        public OnTagCountChangedDelegate OnAnyChange;
        public OnTagCountChangedDelegate OnNewOrRemove;

        public readonly bool IsEmpty => ExplicitCount == 0 &&
                                        TotalCount == 0 &&
                                        OnAnyChange == null &&
                                        OnNewOrRemove == null;
    }

    public interface IReadOnlyGameplayTagCountContainer : IReadOnlyGameplayTagContainer
    {
        int GetExplicitTagCount(GameplayTag tag);
        int GetTagCount(GameplayTag tag);
    }

    public interface IGameplayTagCountContainer : IGameplayTagContainer, IReadOnlyGameplayTagCountContainer
    {
        event OnTagCountChangedDelegate OnAnyTagCountChange;
        event OnTagCountChangedDelegate OnAnyTagNewOrRemove;

        void RegisterTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback);

        void RemoveTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback);

        void RemoveAllTagEventCallbacks();

        /// <summary>제거와 추가를 하나의 변경 작업으로 처리합니다.</summary>
        void UpdateTags<TAdded, TRemoved>(in TAdded addedTags, in TRemoved removedTags)
            where TAdded : IReadOnlyGameplayTagContainer
            where TRemoved : IReadOnlyGameplayTagContainer;
    }

    /// <summary>태그별 명시 개수, 전체 개수, 콜백을 하나의 엔트리로 관리합니다.</summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(GameplayTagContainerDebugView))]
    public sealed class GameplayTagCountContainer : IGameplayTagCountContainer
    {
        private readonly Dictionary<GameplayTag, GameplayTagCountEntry> entries = new();
        private GameplayTagContainerIndices indices = GameplayTagContainerIndices.Create();
        private int generation;

        public bool IsEmpty
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.IsEmpty;
            }
        }

        public int ExplicitTagCount
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.ExplicitTagCount;
            }
        }

        public int TagCount
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.TagCount;
            }
        }

        public GameplayTagContainerIndices Indices
        {
            get
            {
                EnsureCurrentGeneration();
                return indices;
            }
        }

        public event OnTagCountChangedDelegate OnAnyTagNewOrRemove;
        public event OnTagCountChangedDelegate OnAnyTagCountChange;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Count (Explicit, Total) = ({ExplicitTagCount}, {TagCount})";

        public GameplayTagEnumerator GetExplicitTags()
        {
            EnsureCurrentGeneration();
            return new GameplayTagEnumerator(indices.Explicit);
        }

        public GameplayTagEnumerator GetTags()
        {
            EnsureCurrentGeneration();
            return new GameplayTagEnumerator(indices.Implicit);
        }

        public void GetParentTags(GameplayTag tag, List<GameplayTag> output)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetParentTags(indices.Implicit, tag, output);
        }

        public void GetChildTags(GameplayTag tag, List<GameplayTag> output)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetChildTags(indices.Implicit, tag, output);
        }

        public void GetExplicitParentTags(GameplayTag tag, List<GameplayTag> output)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetParentTags(indices.Explicit, tag, output);
        }

        public void GetExplicitChildTags(GameplayTag tag, List<GameplayTag> output)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetChildTags(indices.Explicit, tag, output);
        }

        public int GetTagCount(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();
            return entries.TryGetValue(tag, out GameplayTagCountEntry entry) ? entry.TotalCount : 0;
        }

        public int GetExplicitTagCount(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();
            return entries.TryGetValue(tag, out GameplayTagCountEntry entry) ? entry.ExplicitCount : 0;
        }

        public void RegisterTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            entries.TryGetValue(tag, out GameplayTagCountEntry entry);
            GetEventDelegate(ref entry, eventType) += callback;
            entries[tag] = entry;
        }

        public void RemoveTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback)
        {
            EnsureCurrentGeneration();
            if (tag.IsNone || callback == null || !entries.TryGetValue(tag, out GameplayTagCountEntry entry))
                return;

            GetEventDelegate(ref entry, eventType) -= callback;
            SetOrRemoveEntry(tag, entry);
        }

        public void RemoveAllTagEventCallbacks()
        {
            EnsureCurrentGeneration();
            using (ListPool<GameplayTag>.Rent(out List<GameplayTag> keys))
            {
                CopyKeys(keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    GameplayTag tag = keys[i];
                    GameplayTagCountEntry entry = entries[tag];
                    entry.OnAnyChange = null;
                    entry.OnNewOrRemove = null;
                    SetOrRemoveEntry(tag, entry);
                }
            }
        }

        public void AddTag(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            {
                AddTagInternal(tag, callbacks);
                ExecuteCallbacks(callbacks);
            }
        }

        public void AddTags<T>(in T other) where T : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            {
                foreach (GameplayTag tag in other.GetExplicitTags())
                    AddTagInternal(tag, callbacks);

                ExecuteCallbacks(callbacks);
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            {
                RemoveTagInternal(tag, callbacks);
                ExecuteCallbacks(callbacks);
            }
        }

        public void RemoveTags<T>(in T other) where T : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            {
                foreach (GameplayTag tag in other.GetExplicitTags())
                    RemoveTagInternal(tag, callbacks);

                ExecuteCallbacks(callbacks);
            }
        }

        public void UpdateTags<TAdded, TRemoved>(in TAdded addedTags, in TRemoved removedTags)
            where TAdded : IReadOnlyGameplayTagContainer
            where TRemoved : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            {
                foreach (GameplayTag tag in removedTags.GetExplicitTags())
                    RemoveTagInternal(tag, callbacks);
                foreach (GameplayTag tag in addedTags.GetExplicitTags())
                    AddTagInternal(tag, callbacks);

                ExecuteCallbacks(callbacks);
            }
        }

        public void Clear()
        {
            EnsureCurrentGeneration();
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> callbacks))
            using (ListPool<GameplayTag>.Rent(out List<GameplayTag> keys))
            {
                CopyKeys(keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    GameplayTag tag = keys[i];
                    GameplayTagCountEntry entry = entries[tag];
                    if (entry.TotalCount > 0)
                        QueueCountChanged(tag, 0, entry, wasAddedOrRemoved: true, callbacks);

                    entry.ExplicitCount = 0;
                    entry.TotalCount = 0;
                    SetOrRemoveEntry(tag, entry);
                }

                indices.Clear();
                ExecuteCallbacks(callbacks);
            }
        }

        public GameplayTagEnumerator GetEnumerator()
        {
            return GetTags();
        }

        IEnumerator<GameplayTag> IEnumerable<GameplayTag>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static ref OnTagCountChangedDelegate GetEventDelegate(
            ref GameplayTagCountEntry entry,
            GameplayTagEventType eventType)
        {
            switch (eventType)
            {
                case GameplayTagEventType.AnyCountChange:
                    return ref entry.OnAnyChange;
                case GameplayTagEventType.NewOrRemoved:
                    return ref entry.OnNewOrRemove;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType));
            }
        }

        private void AddTagInternal(
            GameplayTag tag,
            List<DeferredTagChangedDelegate> callbacks)
        {
            entries.TryGetValue(tag, out GameplayTagCountEntry explicitEntry);
            bool isNewExplicitTag = explicitEntry.ExplicitCount == 0;
            explicitEntry.ExplicitCount++;
            entries[tag] = explicitEntry;

            if (isNewExplicitTag)
                InsertIndex(indices.Explicit, tag.RuntimeIndex);

            foreach (GameplayTag hierarchyTag in tag.HierarchyTags)
            {
                entries.TryGetValue(hierarchyTag, out GameplayTagCountEntry entry);
                int previousCount = entry.TotalCount;
                entry.TotalCount = previousCount + 1;
                entries[hierarchyTag] = entry;

                bool added = previousCount == 0;
                if (added)
                    InsertIndex(indices.Implicit, hierarchyTag.RuntimeIndex);

                QueueCountChanged(hierarchyTag, entry.TotalCount, entry, added, callbacks);
            }
        }

        private void RemoveTagInternal(
            GameplayTag tag,
            List<DeferredTagChangedDelegate> callbacks)
        {
            if (!entries.TryGetValue(tag, out GameplayTagCountEntry explicitEntry) ||
                explicitEntry.ExplicitCount == 0)
            {
                GameplayTagUtility.WarnNotExplicitlyAddedTagRemoval(tag);
                return;
            }

            explicitEntry.ExplicitCount--;
            entries[tag] = explicitEntry;
            if (explicitEntry.ExplicitCount == 0)
                RemoveIndex(indices.Explicit, tag.RuntimeIndex);

            foreach (GameplayTag hierarchyTag in tag.HierarchyTags)
            {
                if (!entries.TryGetValue(hierarchyTag, out GameplayTagCountEntry entry) || entry.TotalCount == 0)
                    continue;

                entry.TotalCount--;
                bool removed = entry.TotalCount == 0;
                if (removed)
                    RemoveIndex(indices.Implicit, hierarchyTag.RuntimeIndex);

                QueueCountChanged(hierarchyTag, entry.TotalCount, entry, removed, callbacks);
                SetOrRemoveEntry(hierarchyTag, entry);
            }
        }

        private void QueueCountChanged(
            GameplayTag tag,
            int count,
            GameplayTagCountEntry entry,
            bool wasAddedOrRemoved,
            List<DeferredTagChangedDelegate> callbacks)
        {
            if (wasAddedOrRemoved)
            {
                if (entry.OnNewOrRemove != null)
                    callbacks.Add(new DeferredTagChangedDelegate(tag, count, entry.OnNewOrRemove));
                if (OnAnyTagNewOrRemove != null)
                    callbacks.Add(new DeferredTagChangedDelegate(tag, count, OnAnyTagNewOrRemove));
            }

            if (entry.OnAnyChange != null)
                callbacks.Add(new DeferredTagChangedDelegate(tag, count, entry.OnAnyChange));
            if (OnAnyTagCountChange != null)
                callbacks.Add(new DeferredTagChangedDelegate(tag, count, OnAnyTagCountChange));
        }

        private static void ExecuteCallbacks(List<DeferredTagChangedDelegate> callbacks)
        {
            for (int i = 0; i < callbacks.Count; i++)
                callbacks[i].Execute();
        }

        private static void InsertIndex(List<int> indicesList, int runtimeIndex)
        {
            int index = BinarySearchUtility.Search(indicesList, runtimeIndex);
            if (index < 0)
                indicesList.Insert(~index, runtimeIndex);
        }

        private static void RemoveIndex(List<int> indicesList, int runtimeIndex)
        {
            int index = BinarySearchUtility.Search(indicesList, runtimeIndex);
            if (index >= 0)
                indicesList.RemoveAt(index);
        }

        private void SetOrRemoveEntry(GameplayTag tag, GameplayTagCountEntry entry)
        {
            if (entry.IsEmpty)
                entries.Remove(tag);
            else
                entries[tag] = entry;
        }

        private void CopyKeys(List<GameplayTag> output)
        {
            output.Clear();
            foreach (GameplayTag tag in entries.Keys)
                output.Add(tag);
        }

        private void EnsureCurrentGeneration()
        {
            int currentGeneration = GameplayTagManager.Generation;
            if (generation == currentGeneration)
                return;

            if (generation <= 0 && entries.Count == 0)
            {
                generation = currentGeneration;
                return;
            }

            using (ListPool<GameplayTag>.Rent(out List<GameplayTag> keys))
            {
                CopyKeys(keys);
                indices.Clear();

                for (int i = 0; i < keys.Count; i++)
                {
                    GameplayTag key = keys[i];
                    GameplayTagCountEntry entry = entries[key];
                    entry.TotalCount = 0;
                    if (entry.ExplicitCount > 0 && !GameplayTagManager.RequestTag(key.Name, false).IsValid)
                        entry.ExplicitCount = 0;

                    SetOrRemoveEntry(key, entry);
                }

                for (int i = 0; i < keys.Count; i++)
                {
                    GameplayTag key = keys[i];
                    if (!entries.TryGetValue(key, out GameplayTagCountEntry explicitEntry) ||
                        explicitEntry.ExplicitCount == 0)
                    {
                        continue;
                    }

                    GameplayTag currentTag = GameplayTagManager.RequestTag(key.Name, false);
                    InsertIndex(indices.Explicit, currentTag.RuntimeIndex);
                    foreach (GameplayTag hierarchyTag in currentTag.HierarchyTags)
                    {
                        entries.TryGetValue(hierarchyTag, out GameplayTagCountEntry hierarchyEntry);
                        hierarchyEntry.TotalCount += explicitEntry.ExplicitCount;
                        entries[hierarchyTag] = hierarchyEntry;
                        InsertIndex(indices.Implicit, hierarchyTag.RuntimeIndex);
                    }
                }
            }

            generation = currentGeneration;
        }
    }
}