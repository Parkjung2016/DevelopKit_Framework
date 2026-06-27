using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    [DebuggerDisplay("{TagName,nq}")]
    internal sealed class GameplayTagDefinition
    {
        public static GameplayTagDefinition NoneTagDefinition { get; } = new();

        public GameplayTag Tag => new(this);

        public bool IsValid => RuntimeIndex >= 0;

        public int SourceCount => sources.Count;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTagDefinition> Children => new(children);

        /// <summary>
        /// 이 태그의 부모 태그입니다. 예: "A.B.C"이면 ["A", "A.B", "A.B.C"]입니다.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> ParentTags => new(parentTags);

        /// <summary>
        /// 이 태그의 자식 태그입니다. 예: "A.B.C"이면 ["A.B.C.D", "A.B.C.E"]입니다.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> ChildTags => new(childTags);

        /// <summary>
        /// 이 태그 계층에 포함된 태그입니다. 예: "A.B.C"이면 ["A", "A.B", "A.B.C"]입니다.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> HierarchyTags => new(hierarchyTags);

        /// <summary>태그 전체 이름(부모 포함)입니다.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string TagName { get; }

        /// <summary>개발 중 태그에 대한 추가 설명입니다.</summary>
        public string Description { get; internal set; }

        /// <summary>태그 플래그입니다.</summary>
        public GameplayTagFlags Flags { get; }

        /// <summary>부모를 제외한 태그 라벨입니다.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Label { get; }

        /// <summary>태그 계층 깊이(부모 태그 개수)입니다.</summary>
        public int HierarchyLevel { get; }

        public int RuntimeIndex { get; internal set; }

        public GameplayTagDefinition ParentTagDefinition { get; private set; }

        private GameplayTag[] parentTags = Array.Empty<GameplayTag>();
        private GameplayTag[] childTags = Array.Empty<GameplayTag>();
        private GameplayTag[] hierarchyTags = Array.Empty<GameplayTag>();
        private GameplayTagDefinition[] children = Array.Empty<GameplayTagDefinition>();
        private List<IGameplayTagSource> sources = new();
        private int nameHash;

        private GameplayTagDefinition()
        {
            TagName = "<None>";
            Description = string.Empty;
            Label = "None";
            HierarchyLevel = 0;
            RuntimeIndex = 0;
            ParentTagDefinition = null;
            parentTags = Array.Empty<GameplayTag>();
            childTags = Array.Empty<GameplayTag>();
            hierarchyTags = Array.Empty<GameplayTag>();
            children = Array.Empty<GameplayTagDefinition>();
            nameHash = TagName.GetHashCode();
        }

        public GameplayTagDefinition(string name, string description, GameplayTagFlags flags = GameplayTagFlags.None)
        {
            TagName = name;
            Description = description;
            Flags = flags;
            nameHash = name.GetHashCode();

            Label = GameplayTagUtility.GetLabel(name);
            HierarchyLevel = GameplayTagUtility.GetHeirarchyLevelFromName(name);
        }

        public static GameplayTagDefinition CreateInvalidDefinition(string name)
        {
            GameplayTagDefinition invalidDefinition = new(name, "Invalid Tag");
            invalidDefinition.SetRuntimeIndex(-1);
            return invalidDefinition;
        }

        /// <summary>지정한 태그의 자식인지 여부를 반환합니다.</summary>
        /// <param name="tag">부모 후보 태그입니다.</param>
        public bool IsChildOf(GameplayTag tag)
        {
            if (RuntimeIndex <= tag.RuntimeIndex)
                return false;

            if (parentTags.Length > 1 && tag.RuntimeIndex < parentTags[0].RuntimeIndex)
                return false;

            for (int i = 0; i < parentTags.Length; i++)
            {
                if (parentTags[i] == tag)
                    return true;
            }

            return false;
        }

        /// <summary>지정한 태그의 부모인지 여부를 반환합니다.</summary>
        /// <param name="tag">자식 후보 태그입니다.</param>
        public bool IsParentOf(GameplayTag tag)
        {
            if (RuntimeIndex >= tag.RuntimeIndex)
                return false;

            if (childTags.Length > 1 && tag.RuntimeIndex > childTags[^1].RuntimeIndex)
                return false;

            for (int i = 0; i < childTags.Length; i++)
            {
                if (childTags[i] == tag)
                    return true;
            }

            return false;
        }

        public void SetParent(GameplayTagDefinition parent)
        {
            ParentTagDefinition = parent;
            List<GameplayTag> tags = new();

            GameplayTagDefinition current = parent;
            while (current != null)
            {
                tags.Add(current.Tag);
                current = current.ParentTagDefinition;
            }

            tags.Reverse();
            parentTags = tags.ToArray();
        }

        public void SetChildren(List<GameplayTagDefinition> children)
        {
            this.children = children.ToArray();
            childTags = children.Select(c => c.Tag).ToArray();
        }

        public void SetHierarchyTags(GameplayTag[] hierarchyTags)
        {
            this.hierarchyTags = hierarchyTags;
        }

        public void SetRuntimeIndex(int index)
        {
            RuntimeIndex = index;
        }

        public void AddSource(IGameplayTagSource source)
        {
            if (!sources.Contains(source))
                sources.Add(source);
        }

        public override int GetHashCode()
        {
            return nameHash;
        }

        public IGameplayTagSource GetSource(int index)
        {
            if (index < 0 || index >= sources.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

            return sources[index];
        }

        public IEnumerable<IGameplayTagSource> GetAllSources()
        {
            return sources.AsReadOnly();
        }
    }
}
