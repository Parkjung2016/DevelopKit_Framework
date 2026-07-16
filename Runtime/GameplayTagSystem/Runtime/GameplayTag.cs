using System;
using System.Diagnostics;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>계층형 이름으로 식별되는 게임플레이 태그 값입니다.</summary>
    [Serializable]
    [DebuggerDisplay("{serializedTagName,nq}")]
    public struct GameplayTag : IEquatable<GameplayTag>, ISerializationCallbackReceiver
    {
        /// <summary>태그가 없음을 나타내는 값입니다.</summary>
        public static readonly GameplayTag None = new(GameplayTagDefinition.NoneTagDefinition);

        /// <summary>태그가 없는 값인지 확인합니다.</summary>
        public readonly bool IsNone => definition == GameplayTagDefinition.NoneTagDefinition ||
                                       (definition == null && string.IsNullOrEmpty(serializedTagName));

        /// <summary>현재 등록된 태그인지 확인합니다.</summary>
        public readonly bool IsValid
        {
            get
            {
                GameplayTagDefinition current = Definition;
                return current != GameplayTagDefinition.NoneTagDefinition && current.IsValid;
            }
        }

        public readonly bool IsLeaf => IsValid && Definition.Children.Length == 0;

        /// <summary>런타임 조회용 인덱스입니다. 유효하지 않은 태그는 -1입니다.</summary>
        public readonly int RuntimeIndex => IsValid ? Definition.RuntimeIndex : -1;

        internal readonly GameplayTagDefinition Definition
        {
            get
            {
                if (definition == null || definition == GameplayTagDefinition.NoneTagDefinition)
                    return GameplayTagDefinition.NoneTagDefinition;

                if (definition.Generation == 0 || definition.Generation == GameplayTagManager.Generation)
                    return definition;

                return GameplayTagManager.TryGetCurrentDefinition(Name, out GameplayTagDefinition current)
                    ? current
                    : GameplayTagDefinition.NoneTagDefinition;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> ParentTags => Definition.ParentTags;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> ChildTags => Definition.ChildTags;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> HierarchyTags => Definition.HierarchyTags;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly string Label => Definition.Label;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly int HierarchyLevel => Definition.HierarchyLevel;

        public readonly string Description => Definition.Description;

        /// <summary>바로 위 부모 태그입니다. 부모가 없으면 None입니다.</summary>
        public readonly GameplayTag ParentTag
        {
            get
            {
                GameplayTagDefinition parentDefinition = Definition.ParentTagDefinition;
                return parentDefinition == null ? None : parentDefinition.Tag;
            }
        }

        public readonly GameplayTagFlags Flags => Definition.Flags;

        /// <summary>태그의 전체 이름입니다. None은 빈 문자열입니다.</summary>
        public readonly string Name => serializedTagName ?? string.Empty;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [SerializeField]
        private string serializedTagName;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GameplayTagDefinition definition;

        internal GameplayTag(GameplayTagDefinition definition)
        {
            this.definition = definition ?? GameplayTagDefinition.NoneTagDefinition;
            serializedTagName = this.definition == GameplayTagDefinition.NoneTagDefinition
                ? null
                : this.definition.TagName;
        }

        private GameplayTag(string name)
        {
            definition = null;
            serializedTagName = name;
        }

        internal static GameplayTag CreateInvalid(string name)
        {
            return new GameplayTag(name);
        }

        public readonly bool IsParentOf(in GameplayTag tag)
        {
            ValidateIsValid();
            return Definition.IsParentOf(tag);
        }

        public readonly bool IsChildOf(in GameplayTag parentTag)
        {
            ValidateIsValid();
            return Definition.IsChildOf(parentTag);
        }

        public readonly bool Equals(GameplayTag other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public readonly override bool Equals(object obj)
        {
            if (obj is GameplayTag other)
                return Equals(other);

            return obj is string otherName && string.Equals(Name, otherName, StringComparison.Ordinal);
        }

        public readonly override int GetHashCode()
        {
            if (IsNone)
                return StringComparer.Ordinal.GetHashCode(string.Empty);

            return definition != null
                ? definition.GetHashCode()
                : StringComparer.Ordinal.GetHashCode(Name);
        }

        public readonly override string ToString()
        {
            return IsNone ? "<None>" : Name;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(serializedTagName))
            {
                this = None;
                return;
            }

            this = GameplayTagManager.RequestTag(serializedTagName, logWarningIfNotFound: false);
            if (!IsValid)
                CDebug.LogWarning($"등록되지 않은 게임플레이 태그입니다: \"{serializedTagName}\".");
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (IsNone)
            {
                serializedTagName = null;
                return;
            }

            if (IsValid)
                serializedTagName = definition.TagName;
        }

        [Conditional("DEBUG")]
        internal readonly void ValidateIsValid()
        {
            if (IsNone)
                throw new InvalidOperationException("GameplayTag.None에는 이 작업을 수행할 수 없습니다.");

            if (!IsValid)
                throw new InvalidOperationException($"등록되지 않은 게임플레이 태그입니다: \"{Name}\".");
        }

        public static implicit operator GameplayTag(string tagName)
        {
            return GameplayTagManager.RequestTag(tagName);
        }

        public static bool operator ==(in GameplayTag lhs, in GameplayTag rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(in GameplayTag lhs, in GameplayTag rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}