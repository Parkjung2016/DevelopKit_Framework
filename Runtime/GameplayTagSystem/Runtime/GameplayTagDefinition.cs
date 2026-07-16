using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        public ReadOnlySpan<GameplayTagDefinition> Children => children;

        /// <summary>루트부터 바로 위 부모까지의 태그입니다.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> ParentTags => parentTags;

        /// <summary>이 태그 아래에 등록된 모든 자식 태그입니다.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> ChildTags => childTags;

        /// <summary>루트부터 자기 자신까지의 태그입니다.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ReadOnlySpan<GameplayTag> HierarchyTags => hierarchyTags;

        public string TagName { get; }
        public string Description { get; internal set; }
        public GameplayTagFlags Flags { get; }
        public string Label { get; }
        public int HierarchyLevel { get; }
        public int RuntimeIndex { get; private set; }
        public int Generation { get; private set; }
        public GameplayTagDefinition ParentTagDefinition { get; private set; }

        private GameplayTag[] parentTags = Array.Empty<GameplayTag>();
        private GameplayTag[] childTags = Array.Empty<GameplayTag>();
        private GameplayTag[] hierarchyTags = Array.Empty<GameplayTag>();
        private GameplayTagDefinition[] children = Array.Empty<GameplayTagDefinition>();
        private readonly List<IGameplayTagSource> sources = new();
        private readonly int nameHash;

        private GameplayTagDefinition()
        {
            TagName = "<None>";
            Description = string.Empty;
            Label = "None";
            HierarchyLevel = 0;
            RuntimeIndex = 0;
            nameHash = StringComparer.Ordinal.GetHashCode(TagName);
        }

        public GameplayTagDefinition(string name, string description, GameplayTagFlags flags = GameplayTagFlags.None)
        {
            TagName = name;
            Description = description;
            Flags = flags;
            Label = GameplayTagUtility.GetLabel(name);
            HierarchyLevel = GameplayTagUtility.GetHierarchyLevelFromName(name);
            nameHash = StringComparer.Ordinal.GetHashCode(name);
        }

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
            if (parent == null)
            {
                parentTags = Array.Empty<GameplayTag>();
                return;
            }

            int parentCount = 0;
            for (GameplayTagDefinition current = parent; current != null; current = current.ParentTagDefinition)
                parentCount++;

            parentTags = new GameplayTag[parentCount];
            GameplayTagDefinition currentParent = parent;
            for (int i = parentTags.Length - 1; i >= 0; i--)
            {
                parentTags[i] = currentParent.Tag;
                currentParent = currentParent.ParentTagDefinition;
            }
        }

        public void SetChildren(List<GameplayTagDefinition> childDefinitions)
        {
            int count = childDefinitions?.Count ?? 0;
            if (count == 0)
            {
                children = Array.Empty<GameplayTagDefinition>();
                childTags = Array.Empty<GameplayTag>();
                return;
            }

            children = new GameplayTagDefinition[count];
            childTags = new GameplayTag[count];
            for (int i = 0; i < count; i++)
            {
                GameplayTagDefinition child = childDefinitions[i];
                children[i] = child;
                childTags[i] = child.Tag;
            }
        }

        public void SetHierarchyTags(GameplayTag[] tags)
        {
            hierarchyTags = tags ?? Array.Empty<GameplayTag>();
        }

        public void SetRuntimeIndex(int index)
        {
            RuntimeIndex = index;
        }

        public void SetGeneration(int generation)
        {
            Generation = generation;
        }

        public void AddSource(IGameplayTagSource source)
        {
            if (source != null && !sources.Contains(source))
                sources.Add(source);
        }

        public override int GetHashCode()
        {
            return nameHash;
        }

        public IGameplayTagSource GetSource(int index)
        {
            if ((uint)index >= (uint)sources.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return sources[index];
        }

        public IReadOnlyList<IGameplayTagSource> GetAllSources()
        {
            return sources;
        }
    }
}