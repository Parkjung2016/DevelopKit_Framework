using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 개수가 변경될 때 호출되는 콜백입니다.</summary>
    public delegate void OnTagCountChangedDelegate(GameplayTag gameplayTag, int newCount);

    /// <summary>태그 이벤트 콜백 등록 시 구분하는 이벤트 종류입니다.</summary>
    public enum GameplayTagEventType
    {
        /// <summary>태그가 새로 추가되거나 완전히 제거될 때만 호출합니다.</summary>
        NewOrRemoved,

        /// <summary>개수가 바뀔 때마다 호출합니다.</summary>
        AnyCountChange
    }

    /// <summary>지연 실행할 태그 변경 콜백 한 건입니다.</summary>
    internal readonly struct DeferredTagChangedDelegate
    {
        public readonly GameplayTag GameplayTag;
        public readonly int NewCount;
        public readonly OnTagCountChangedDelegate Delegate;

        public DeferredTagChangedDelegate(GameplayTag gameplayTag, int newCount, OnTagCountChangedDelegate callback)
        {
            GameplayTag = gameplayTag;
            NewCount = newCount;
            Delegate = callback;
        }

        public void Execute()
        {
            Delegate(GameplayTag, NewCount);
        }
    }

    /// <summary>태그별로 등록된 이벤트 콜백 묶음입니다.</summary>
    internal struct GameplayTagDelegateInfo
    {
        public OnTagCountChangedDelegate OnAnyChange;
        public OnTagCountChangedDelegate OnNewOrRemove;
    }

    /// <summary>읽기 전용 태그 개수 컨테이너입니다.</summary>
    public interface IReadOnlyGameplayTagCountContainer : IReadOnlyGameplayTagContainer
    {
        /// <summary>명시적으로 추가된 횟수를 반환합니다.</summary>
        int GetExplicitTagCount(GameplayTag tag);

        /// <summary>계층을 포함한 태그 참조 횟수를 반환합니다.</summary>
        int GetTagCount(GameplayTag tag);
    }

    /// <summary>태그 개수를 추적하고 변경 이벤트를 등록할 수 있는 컨테이너입니다.</summary>
    public interface IGameplayTagCountContainer : IGameplayTagContainer, IReadOnlyGameplayTagCountContainer
    {
        /// <summary>임의 태그의 개수가 바뀔 때 호출됩니다.</summary>
        event OnTagCountChangedDelegate OnAnyTagCountChange;

        /// <summary>임의 태그가 새로 추가되거나 완전히 제거될 때 호출됩니다.</summary>
        event OnTagCountChangedDelegate OnAnyTagNewOrRemove;

        /// <summary>특정 태그에 대한 이벤트 콜백을 등록합니다.</summary>
        void RegisterTagEventCallback(GameplayTag tag, GameplayTagEventType eventType, OnTagCountChangedDelegate callback);

        /// <summary>특정 태그에 등록된 이벤트 콜백을 해제합니다.</summary>
        void RemoveTagEventCallback(GameplayTag tag, GameplayTagEventType eventType, OnTagCountChangedDelegate callback);

        /// <summary>등록된 모든 태그 이벤트 콜백을 제거합니다.</summary>
        void RemoveAllTagEventCallbacks();
    }

    /// <summary>
    /// 동일 태그를 여러 번 추가할 수 있고, 계층(부모) 태그 개수도 함께 추적하는 컨테이너입니다.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [DebuggerTypeProxy(typeof(GameplayTagContainerDebugView))]
    public sealed class GameplayTagCountContainer : IGameplayTagCountContainer
    {
        private Dictionary<GameplayTag, GameplayTagDelegateInfo> tagDelegateInfoMap = new();
        private Dictionary<GameplayTag, int> tagCountMap = new();
        private Dictionary<GameplayTag, int> explicitTagCountMap = new();
        private GameplayTagContainerIndices indices = GameplayTagContainerIndices.Create();

        public bool IsEmpty => indices.IsEmpty;

        public int ExplicitTagCount => indices.ExplicitTagCount;

        public int TagCount => indices.TagCount;

        public GameplayTagContainerIndices Indices => indices;

        public event OnTagCountChangedDelegate OnAnyTagNewOrRemove;

        public event OnTagCountChangedDelegate OnAnyTagCountChange;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Count (Explicit, Total) = ({ExplicitTagCount}, {TagCount})";

        public GameplayTagEnumerator GetExplicitTags()
        {
            return new GameplayTagEnumerator(indices.Explicit);
        }

        public GameplayTagEnumerator GetTags()
        {
            return new GameplayTagEnumerator(indices.Implicit);
        }

        public void GetParentTags(GameplayTag tag, List<GameplayTag> parentTags)
        {
            GameplayTagContainerUtility.GetParentTags(indices.Implicit, tag, parentTags);
        }

        public void GetChildTags(GameplayTag tag, List<GameplayTag> childTags)
        {
            GameplayTagContainerUtility.GetChildTags(indices.Implicit, tag, childTags);
        }

        public void GetExplicitParentTags(GameplayTag tag, List<GameplayTag> parentTags)
        {
            GameplayTagContainerUtility.GetParentTags(indices.Explicit, tag, parentTags);
        }

        public void GetExplicitChildTags(GameplayTag tag, List<GameplayTag> childTags)
        {
            GameplayTagContainerUtility.GetChildTags(indices.Explicit, tag, childTags);
        }

        public int GetTagCount(GameplayTag tag)
        {
            tag.ValidateIsValid();
            tagCountMap.TryGetValue(tag, out int count);
            return count;
        }

        public int GetExplicitTagCount(GameplayTag tag)
        {
            tag.ValidateIsValid();
            explicitTagCountMap.TryGetValue(tag, out int count);
            return count;
        }

        public void RegisterTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback)
        {
            tag.ValidateIsValid();

            tagDelegateInfoMap.TryGetValue(tag, out GameplayTagDelegateInfo delegateInfo);
            GetEventDelegate(ref delegateInfo, eventType) += callback;
            tagDelegateInfoMap[tag] = delegateInfo;
        }

        public void RemoveTagEventCallback(
            GameplayTag tag,
            GameplayTagEventType eventType,
            OnTagCountChangedDelegate callback)
        {
            tag.ValidateIsValid();

            if (!tagDelegateInfoMap.TryGetValue(tag, out GameplayTagDelegateInfo delegateInfo))
                return;

            GetEventDelegate(ref delegateInfo, eventType) -= callback;
            tagDelegateInfoMap[tag] = delegateInfo;
        }

        public void RemoveAllTagEventCallbacks()
        {
            tagDelegateInfoMap.Clear();
        }

        public void AddTag(GameplayTag tag)
        {
            tag.ValidateIsValid();

            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> delegates))
            {
                AddTagInternal(tag, delegates);

                foreach (DeferredTagChangedDelegate deferred in delegates)
                    deferred.Execute();
            }
        }

        public void AddTags<T>(in T other) where T : IReadOnlyGameplayTagContainer
        {
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> delegates))
            {
                foreach (GameplayTag gameplayTag in other.GetExplicitTags())
                    AddTagInternal(gameplayTag, delegates);

                foreach (DeferredTagChangedDelegate deferred in delegates)
                    deferred.Execute();
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            tag.ValidateIsValid();

            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> delegates))
            {
                RemoveTagInternal(tag, delegates);

                for (int i = 0; i < delegates.Count; i++)
                    delegates[i].Execute();
            }
        }

        public void RemoveTags<T>(in T other) where T : IReadOnlyGameplayTagContainer
        {
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> delegates))
            {
                foreach (GameplayTag gameplayTag in other.GetExplicitTags())
                    RemoveTagInternal(gameplayTag, delegates);

                for (int i = 0; i < delegates.Count; i++)
                    delegates[i].Execute();
            }
        }

        public void Clear()
        {
            using (ListPool<DeferredTagChangedDelegate>.Rent(out List<DeferredTagChangedDelegate> delegates))
            {
                foreach (GameplayTag tag in GetTags())
                {
                    tagDelegateInfoMap.TryGetValue(tag, out GameplayTagDelegateInfo delegateInfo);

                    if (delegateInfo.OnNewOrRemove != null)
                        delegates.Add(new DeferredTagChangedDelegate(tag, 0, delegateInfo.OnNewOrRemove));

                    if (OnAnyTagNewOrRemove != null)
                        delegates.Add(new DeferredTagChangedDelegate(tag, 0, OnAnyTagNewOrRemove));
                }

                explicitTagCountMap.Clear();
                tagCountMap.Clear();
                indices.Clear();

                foreach (DeferredTagChangedDelegate deferred in delegates)
                    deferred.Execute();
            }
        }
        public GameplayTagEnumerator GetEnumerator()
        {
            return new GameplayTagEnumerator(indices.Implicit);
        }

        IEnumerator<GameplayTag> IEnumerable<GameplayTag>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static ref OnTagCountChangedDelegate GetEventDelegate(
            ref GameplayTagDelegateInfo delegateInfo,
            GameplayTagEventType eventType)
        {
            switch (eventType)
            {
                case GameplayTagEventType.AnyCountChange:
                    return ref delegateInfo.OnAnyChange;
                case GameplayTagEventType.NewOrRemoved:
                    return ref delegateInfo.OnNewOrRemove;
                default:
                    throw new ArgumentException(nameof(eventType));
            }
        }

        private void AddTagInternal(GameplayTag tag, List<DeferredTagChangedDelegate> tagChangeDelegates)
        {
            explicitTagCountMap.TryGetValue(tag, out int previousExplicitCount);
            explicitTagCountMap[tag] = previousExplicitCount + 1;

            if (previousExplicitCount == 0)
            {
                int index = ~BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
                indices.Explicit.Insert(index, tag.RuntimeIndex);
            }

            foreach (GameplayTag tagInHierarchy in tag.HierarchyTags)
            {
                tagDelegateInfoMap.TryGetValue(tagInHierarchy, out GameplayTagDelegateInfo delegateInfo);
                tagCountMap.TryGetValue(tagInHierarchy, out int previousCount);
                tagCountMap[tagInHierarchy] = previousCount + 1;

                if (previousCount == 0)
                {
                    int index = ~BinarySearchUtility.Search(indices.Implicit, tagInHierarchy.RuntimeIndex);
                    indices.Implicit.Insert(index, tagInHierarchy.RuntimeIndex);

                    if (delegateInfo.OnNewOrRemove != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 1, delegateInfo.OnNewOrRemove));

                    if (OnAnyTagNewOrRemove != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 1, OnAnyTagNewOrRemove));
                }

                if (delegateInfo.OnAnyChange != null)
                    tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, previousCount + 1, delegateInfo.OnAnyChange));

                if (OnAnyTagCountChange != null)
                    tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, previousCount + 1, OnAnyTagCountChange));
            }
        }

        private void RemoveTagInternal(GameplayTag tag, List<DeferredTagChangedDelegate> tagChangeDelegates)
        {
            if (!explicitTagCountMap.TryGetValue(tag, out int explicitCount))
            {
                GameplayTagUtility.WarnNotExplictlyAddedTagRemoval(tag);
                return;
            }

            if (explicitCount == 1)
            {
                int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
                indices.Explicit.RemoveAt(index);
                explicitTagCountMap.Remove(tag);
            }
            else
            {
                explicitTagCountMap[tag] = explicitCount - 1;
            }

            foreach (GameplayTag tagInHierarchy in tag.HierarchyTags)
            {
                tagDelegateInfoMap.TryGetValue(tagInHierarchy, out GameplayTagDelegateInfo delegateInfo);

                if (!tagCountMap.TryGetValue(tagInHierarchy, out int count))
                    break;

                if (count == 1)
                {
                    int index = BinarySearchUtility.Search(indices.Implicit, tagInHierarchy.RuntimeIndex);
                    indices.Implicit.RemoveAt(index);
                    tagCountMap.Remove(tagInHierarchy);

                    if (delegateInfo.OnNewOrRemove != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 0, delegateInfo.OnNewOrRemove));

                    if (OnAnyTagNewOrRemove != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 0, OnAnyTagNewOrRemove));

                    if (delegateInfo.OnAnyChange != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 0, delegateInfo.OnAnyChange));

                    if (OnAnyTagCountChange != null)
                        tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, 0, OnAnyTagCountChange));

                    continue;
                }

                tagCountMap[tagInHierarchy] = count - 1;

                if (delegateInfo.OnAnyChange != null)
                    tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, count - 1, delegateInfo.OnAnyChange));

                if (OnAnyTagCountChange != null)
                    tagChangeDelegates.Add(new DeferredTagChangedDelegate(tagInHierarchy, count - 1, OnAnyTagCountChange));
            }
        }
    }
}
