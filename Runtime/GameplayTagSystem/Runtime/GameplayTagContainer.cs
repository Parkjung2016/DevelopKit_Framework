using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 컨테이너의 명시·암시 태그 인덱스 목록입니다.</summary>
    public struct GameplayTagContainerIndices
    {
        public readonly bool IsCreated => Explicit != null && Implicit != null;
        public readonly bool IsEmpty => !IsCreated || Explicit.Count == 0;
        public readonly int TagCount => IsCreated ? Implicit.Count : 0;
        public readonly int ExplicitTagCount => IsCreated ? Explicit.Count : 0;

        internal List<int> Explicit { get; private set; }
        internal List<int> Implicit { get; private set; }

        public static void Create(ref GameplayTagContainerIndices indices)
        {
            if (indices.IsCreated)
                return;

            indices = new GameplayTagContainerIndices()
            {
                Explicit = new(),
                Implicit = new()
            };
        }

        public static GameplayTagContainerIndices Create()
        {
            return new GameplayTagContainerIndices()
            {
                Explicit = new(),
                Implicit = new()
            };
        }

        internal readonly void Clear()
        {
            Explicit?.Clear();
            Implicit?.Clear();
        }

        internal readonly void CopyTo(in GameplayTagContainerIndices other)
        {
            other.Clear();
            other.Explicit.AddRange(Explicit);
            other.Implicit.AddRange(Implicit);
        }
    }

    /// <summary>읽기 전용 게임플레이 태그 컨테이너입니다.</summary>
    public interface IReadOnlyGameplayTagContainer : IEnumerable<GameplayTag>
    {
        /// <summary>컨테이너가 비어 있는지 여부입니다.</summary>
        bool IsEmpty { get; }

        /// <summary>직접 추가된(명시) 태그 개수입니다.</summary>
        int ExplicitTagCount { get; }

        /// <summary>계층을 포함한 전체 태그 개수입니다.</summary>
        int TagCount { get; }

        /// <summary>태그 인덱스 목록입니다.</summary>
        GameplayTagContainerIndices Indices { get; }

        /// <summary>모든 태그(암시 포함)를 열거합니다.</summary>
        GameplayTagEnumerator GetTags();

        /// <summary>명시적으로 추가된 태그만 열거합니다.</summary>
        GameplayTagEnumerator GetExplicitTags();

        /// <summary>지정 태그의 부모 태그를 수집합니다.</summary>
        void GetParentTags(GameplayTag tag, List<GameplayTag> parentTags);

        /// <summary>지정 태그의 자식 태그를 수집합니다.</summary>
        void GetChildTags(GameplayTag tag, List<GameplayTag> childTags);

        /// <summary>지정 태그의 명시 부모 태그를 수집합니다.</summary>
        void GetExplicitParentTags(GameplayTag tag, List<GameplayTag> parentTags);

        /// <summary>지정 태그의 명시 자식 태그를 수집합니다.</summary>
        void GetExplicitChildTags(GameplayTag tag, List<GameplayTag> childTags);
    }

    /// <summary>수정 가능한 게임플레이 태그 컨테이너입니다.</summary>
    public interface IGameplayTagContainer : IReadOnlyGameplayTagContainer
    {
        /// <summary>태그를 추가합니다.</summary>
        void AddTag(GameplayTag gameplayTag);

        /// <summary>태그를 제거합니다.</summary>
        void RemoveTag(GameplayTag gameplayTag);

        /// <summary>다른 컨테이너의 태그를 추가합니다.</summary>
        void AddTags<T>(in T other) where T : IReadOnlyGameplayTagContainer;

        /// <summary>다른 컨테이너에 있는 태그를 제거합니다.</summary>
        void RemoveTags<T>(in T other) where T : IReadOnlyGameplayTagContainer;

        /// <summary>모든 태그를 제거합니다.</summary>
        void Clear();
    }

    /// <summary>명시·암시 태그 집합을 보관하고 계층 기반 매칭을 제공합니다.</summary>
    [Serializable]
    [DebuggerTypeProxy(typeof(GameplayTagContainerDebugView))]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class GameplayTagContainer : IGameplayTagContainer, ISerializationCallbackReceiver, IEnumerable<GameplayTag>
    {
        public static GameplayTagContainer Empty { get; } = new();

        /// <inheritdoc />
        public bool IsEmpty
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.IsEmpty;
            }
        }

        /// <inheritdoc />
        public int ExplicitTagCount
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.ExplicitTagCount;
            }
        }

        /// <inheritdoc />
        public int TagCount
        {
            get
            {
                EnsureCurrentGeneration();
                return indices.TagCount;
            }
        }

        /// <inheritdoc />
        public GameplayTagContainerIndices Indices
        {
            get
            {
                EnsureCurrentGeneration();
                return indices;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"Count (Explicit, Total) = ({ExplicitTagCount}, {TagCount})";

        [SerializeField]
        private List<string> serializedExplicitTags;

        private GameplayTagContainerIndices indices = new();
        private int generation;

        /// <summary>빈 컨테이너를 생성합니다.</summary>
        public GameplayTagContainer()
        { }

        /// <summary>다른 컨테이너의 태그를 복사해 새 인스턴스를 만듭니다.</summary>
        public GameplayTagContainer(IReadOnlyGameplayTagContainer other)
        {
            Copy(this, other);
        }

        /// <summary>이 컨테이너의 복제본을 만듭니다.</summary>
        public GameplayTagContainer Clone()
        {
            GameplayTagContainer clone = new();
            Copy(clone, this);

            return clone;
        }

        /// <summary>소스 컨테이너의 태그를 대상 컨테이너로 복사합니다.</summary>
        public static void Copy<T>(GameplayTagContainer dest, in T src) where T : IReadOnlyGameplayTagContainer
        {
            if (dest == null)
                throw new ArgumentNullException(nameof(dest));

            if (src.IsEmpty)
            {
                dest.Clear();
                return;
            }

            GameplayTagContainerIndices.Create(ref dest.indices);
            src.Indices.CopyTo(dest.indices);
            dest.generation = GameplayTagManager.Generation;
            dest.UpdateSerializedTagNames();
        }

        /// <summary>두 컨테이너의 교집합을 담은 새 컨테이너를 만듭니다.</summary>
        public static GameplayTagContainer Intersection<T, U>(in T lhs, in U rhs) where T : IReadOnlyGameplayTagContainer where U : IReadOnlyGameplayTagContainer
        {
            GameplayTagContainer intersection = new();
            intersection.AddIntersection(lhs, rhs);
            return intersection;
        }

        public static void Intersection<T, U>(GameplayTagContainer output, in T lhs, in U rhs) where T : IReadOnlyGameplayTagContainer where U : IReadOnlyGameplayTagContainer
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (!output.IsEmpty)
                throw new ArgumentException("Output container must be empty.", nameof(output));

            output.AddIntersection(lhs, rhs);
        }

        /// <summary>두 컨테이너의 교집합을 이 컨테이너에 추가합니다.</summary>
        internal void AddIntersection<T, U>(in T lhs, in U rhs) where T : IReadOnlyGameplayTagContainer where U : IReadOnlyGameplayTagContainer
        {
            generation = GameplayTagManager.Generation;
            static void OrderedListIntersection(List<int> a, List<int> b, List<int> dst)
            {
                int i = 0, j = 0;
                while (i < a.Count && j < b.Count)
                {
                    int aElement = a[i], bElement = b[j];
                    if (aElement == bElement)
                    {
                        dst.Add(aElement);
                        i++;
                        j++;
                        continue;
                    }

                    if (aElement < bElement)
                    {
                        i++;
                        continue;
                    }

                    j++;
                }
            }

            if (lhs.IsEmpty || rhs.IsEmpty)
                return;

            if (!indices.IsCreated)
                indices = GameplayTagContainerIndices.Create();

            OrderedListIntersection(lhs.Indices.Explicit, rhs.Indices.Explicit, indices.Explicit);
            OrderedListIntersection(lhs.Indices.Implicit, rhs.Indices.Implicit, indices.Implicit);
            UpdateSerializedTagNames();
        }

        /// <summary>두 컨테이너의 합집합을 담은 새 컨테이너를 만듭니다.</summary>
        public static GameplayTagContainer Union<T, U>(in T lhs, in U rhs) where T : IReadOnlyGameplayTagContainer where U : IReadOnlyGameplayTagContainer
        {
            static void OrderedListUnion(List<int> a, List<int> b, List<int> dst)
            {
                dst.Capacity = Mathf.Max(dst.Capacity, a.Count + b.Count);

                int i = 0, j = 0;
                while (i < a.Count && j < b.Count)
                {
                    int aElement = a[i], bElement = b[j];
                    if (aElement == bElement)
                    {
                        dst.Add(aElement);
                        i++;
                        j++;
                        continue;
                    }

                    if (aElement < bElement)
                    {
                        dst.Add(aElement);
                        i++;
                        continue;
                    }

                    dst.Add(bElement);
                    j++;
                }

                for (; i < a.Count; i++)
                    dst.Add(a[i]);

                for (; j < b.Count; j++)
                    dst.Add(b[j]);
            }

            GameplayTagContainer union = new();
            union.generation = GameplayTagManager.Generation;
            GameplayTagContainerIndices.Create(ref union.indices);

            if (lhs.IsEmpty && rhs.IsEmpty)
                return union;

            if (lhs.IsEmpty)
                return new GameplayTagContainer(rhs);

            if (rhs.IsEmpty)
                return new GameplayTagContainer(lhs);

            OrderedListUnion(lhs.Indices.Explicit, rhs.Indices.Explicit, union.indices.Explicit);
            OrderedListUnion(lhs.Indices.Implicit, rhs.Indices.Implicit, union.indices.Implicit);
            union.UpdateSerializedTagNames();

            return union;
        }

        /// <summary>다른 컨테이너와 명시 태그를 비교해 추가·제거된 태그 목록을 채웁니다.</summary>
        public void GetDiffExplicitTags<T>(T other, List<GameplayTag> added, List<GameplayTag> removed) where T : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            GameplayTagContainerIndices otherIndices = other.Indices;

            List<int> currentContainerTagIndices = Indices.Explicit;
            List<int> otherContainerTagIndices = otherIndices.Explicit;

            int currentIndex = 0, otherIndex = 0;

            while (currentIndex < Indices.ExplicitTagCount && otherIndex < otherIndices.ExplicitTagCount)
            {
                int currentTagIndex = currentContainerTagIndices[currentIndex], otherTagIndex = otherContainerTagIndices[otherIndex];

                if (currentTagIndex == otherTagIndex)
                {
                    currentIndex++;
                    otherIndex++;
                    continue;
                }

                if (currentTagIndex < otherTagIndex)
                {
                    added.Add(GameplayTagManager.GetDefinitionFromRuntimeIndex(currentTagIndex).Tag);
                    currentIndex++;
                    continue;
                }

                removed.Add(GameplayTagManager.GetDefinitionFromRuntimeIndex(otherTagIndex).Tag);
                otherIndex++;
            }

            for (; currentIndex < Indices.ExplicitTagCount; currentIndex++)
                added.Add(GameplayTagManager.GetDefinitionFromRuntimeIndex(currentContainerTagIndices[currentIndex]).Tag);

            for (; otherIndex < otherIndices.ExplicitTagCount; otherIndex++)
                removed.Add(GameplayTagManager.GetDefinitionFromRuntimeIndex(otherContainerTagIndices[otherIndex]).Tag);
        }

        /// <inheritdoc />
        public GameplayTagEnumerator GetExplicitTags()
        {
            EnsureCurrentGeneration();
            return new GameplayTagEnumerator(indices.Explicit);
        }

        /// <inheritdoc />
        public GameplayTagEnumerator GetTags()
        {
            EnsureCurrentGeneration();
            return new GameplayTagEnumerator(indices.Implicit);
        }

        /// <inheritdoc />
        public void GetParentTags(GameplayTag tag, List<GameplayTag> parentTags)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetParentTags(indices.Implicit, tag, parentTags);
        }

        /// <inheritdoc />
        public void GetChildTags(GameplayTag tag, List<GameplayTag> childTags)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetChildTags(indices.Implicit, tag, childTags);
        }

        /// <inheritdoc />
        public void GetExplicitParentTags(GameplayTag tag, List<GameplayTag> parentTags)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetParentTags(indices.Explicit, tag, parentTags);
        }

        /// <inheritdoc />
        public void GetExplicitChildTags(GameplayTag tag, List<GameplayTag> childTags)
        {
            EnsureCurrentGeneration();
            GameplayTagContainerUtility.GetChildTags(indices.Explicit, tag, childTags);
        }

        /// <inheritdoc />
        public void Clear()
        {
            generation = GameplayTagManager.Generation;
            indices.Clear();
            serializedExplicitTags?.Clear();
        }

        /// <inheritdoc />
        public void AddTag(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();

            GameplayTagContainerIndices.Create(ref indices);
            int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
            if (index >= 0)
                return;

            indices.Explicit.Insert(~index, tag.RuntimeIndex);
            AddImplicitTagsFor(tag);
            UpdateSerializedTagNames();
        }

        /// <inheritdoc />
        public void AddTags<T>(in T container) where T : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            GameplayTagContainerIndices.Create(ref indices);

            foreach (GameplayTag tag in container.GetExplicitTags())
            {
                tag.ValidateIsValid();
                int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
                if (index >= 0)
                    continue;

                indices.Explicit.Insert(~index, tag.RuntimeIndex);
                AddImplicitTagsFor(tag);
            }

            UpdateSerializedTagNames();
        }

        /// <inheritdoc />
        public void RemoveTag(GameplayTag tag)
        {
            EnsureCurrentGeneration();
            tag.ValidateIsValid();

            if (!indices.IsCreated)
                return;

            int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
            if (index < 0)
            {
                GameplayTagUtility.WarnNotExplicitlyAddedTagRemoval(tag);
                return;
            }

            indices.Explicit.RemoveAt(index);
            FillImplicitTags();
            UpdateSerializedTagNames();
        }

        /// <inheritdoc />
        public void RemoveTags<T>(in T other)
            where T : IReadOnlyGameplayTagContainer
        {
            EnsureCurrentGeneration();
            if (!indices.IsCreated)
                return;

            foreach (GameplayTag tag in other.GetExplicitTags())
            {
                int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
                if (index < 0)
                {
                    GameplayTagUtility.WarnNotExplicitlyAddedTagRemoval(tag);
                    continue;
                }

                indices.Explicit.RemoveAt(index);
            }

            FillImplicitTags();
            UpdateSerializedTagNames();
        }

        private void AddImplicitTagsFor(GameplayTag tag)
        {
            ReadOnlySpan<GameplayTag> tags = tag.HierarchyTags;
            for (int i = tags.Length - 1; i >= 0; i--)
            {
                GameplayTag parent = tags[i];
                int index = BinarySearchUtility.Search(indices.Implicit, parent.RuntimeIndex);
                if (index >= 0)
                    break;

                indices.Implicit.Insert(~index, parent.RuntimeIndex);
            }
        }

        private void FillImplicitTags()
        {
            indices.Implicit.Clear();

            for (int i = 0; i < indices.Explicit.Count; i++)
            {
                GameplayTagDefinition definition = GameplayTagManager.GetDefinitionFromRuntimeIndex(indices.Explicit[i]);

                foreach (GameplayTag tag in definition.HierarchyTags)
                {
                    if (indices.Implicit.Count > 0 && indices.Implicit[^1] >= tag.RuntimeIndex)
                        continue;

                    indices.Implicit.Add(tag.RuntimeIndex);
                }
            }
        }


        private void EnsureCurrentGeneration()
        {
            int currentGeneration = GameplayTagManager.Generation;
            if (generation == currentGeneration)
                return;

            if (!indices.IsCreated || indices.Explicit.Count == 0)
            {
                generation = currentGeneration;
                return;
            }

            if (generation <= 0 || !GameplayTagManager.HasRuntimeIndexRemap(generation))
            {
                RebuildFromSerializedTags();
                generation = currentGeneration;
                return;
            }

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < indices.Explicit.Count; readIndex++)
            {
                if (!GameplayTagManager.TryRemapRuntimeIndex(
                        generation,
                        indices.Explicit[readIndex],
                        out int remappedIndex))
                {
                    continue;
                }

                if (writeIndex == 0 || indices.Explicit[writeIndex - 1] != remappedIndex)
                    indices.Explicit[writeIndex++] = remappedIndex;
            }

            if (writeIndex < indices.Explicit.Count)
                indices.Explicit.RemoveRange(writeIndex, indices.Explicit.Count - writeIndex);

            generation = currentGeneration;
            FillImplicitTags();
            UpdateSerializedTagNames();
        }

        private void RebuildFromSerializedTags()
        {
            GameplayTagContainerIndices.Create(ref indices);
            indices.Clear();
            if (serializedExplicitTags == null)
                return;

            for (int i = 0; i < serializedExplicitTags.Count; i++)
            {
                GameplayTag tag = GameplayTagManager.RequestTag(serializedExplicitTags[i], false);
                if (!tag.IsValid)
                    continue;

                int index = BinarySearchUtility.Search(indices.Explicit, tag.RuntimeIndex);
                if (index < 0)
                    indices.Explicit.Insert(~index, tag.RuntimeIndex);
            }

            FillImplicitTags();
            UpdateSerializedTagNames();
        }

        public void FillSerializedTags()
        {
            EnsureCurrentGeneration();
            UpdateSerializedTagNames();
        }

        private void UpdateSerializedTagNames()
        {
            serializedExplicitTags ??= new List<string>();
            serializedExplicitTags.Clear();
            if (!indices.IsCreated)
                return;

            for (int i = 0; i < indices.Explicit.Count; i++)
            {
                GameplayTagDefinition definition =
                    GameplayTagManager.GetDefinitionFromRuntimeIndex(indices.Explicit[i]);
                serializedExplicitTags.Add(definition.TagName);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            generation = GameplayTagManager.Generation;
            RebuildFromSerializedTags();
        }

        /// <summary>컬렉션 초기화 구문을 지원하기 위한 메서드입니다.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Add(GameplayTag tag)
        {
            AddTag(tag);
        }

        public IEnumerator<GameplayTag> GetEnumerator()
        {
            return GetTags();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// <see cref="GameplayTagContainer"/> 필드에 지정하면 태그 선택기가
    /// 지정한 부모 태그의 하위 태그만 표시합니다.
    /// </summary>
    public sealed class ShowOnlyChildTagOfAttribute : UnityEngine.PropertyAttribute
    {
        public string ParentTagName { get; }

        public ShowOnlyChildTagOfAttribute(string parentTagName)
        {
            ParentTagName = parentTagName;
        }
    }
}
