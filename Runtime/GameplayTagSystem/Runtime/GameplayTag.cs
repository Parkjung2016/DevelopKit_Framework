using System;
using System.Diagnostics;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임플레이 태그를 나타내는 값 형식입니다.</summary>
    [Serializable]
    [DebuggerDisplay("{serializedTagName,nq}")]
    public partial struct GameplayTag : IEquatable<GameplayTag>, ISerializationCallbackReceiver
    {
        /// <summary>유효하지 않은 태그를 나타냅니다.</summary>
        public static readonly GameplayTag None = new() { definition = GameplayTagDefinition.NoneTagDefinition };

        public readonly bool IsNone => definition == null || definition == GameplayTagDefinition.NoneTagDefinition;

        public readonly bool IsValid => definition != null && definition.IsValid;

        public readonly bool IsLeaf => definition != null && definition.Children.Length == 0;

        public readonly int RuntimeIndex => definition.RuntimeIndex;

        internal readonly GameplayTagDefinition Definition => definition ?? GameplayTagDefinition.NoneTagDefinition;

        /// <inheritdoc cref="GameplayTagDefinition.ParentTags" />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> ParentTags => Definition.ParentTags;

        /// <inheritdoc cref="GameplayTagDefinition.ChildTags" />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> ChildTags => Definition.ChildTags;

        /// <inheritdoc cref="GameplayTagDefinition.HierarchyTags" />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly ReadOnlySpan<GameplayTag> HierarchyTags => Definition.HierarchyTags;

        /// <inheritdoc cref="GameplayTagDefinition.Label" />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly string Label => Definition.Label;

        /// <inheritdoc cref="GameplayTagDefinition.HierarchyLevel" />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly int HierarchyLevel => Definition.HierarchyLevel;

        /// <inheritdoc cref="GameplayTagDefinition.Description" />
        public readonly string Description => Definition.Description;

        /// <summary>이 태그의 부모 태그입니다. 예: "A.B.C"의 부모는 "A.B"입니다.</summary>
        public readonly GameplayTag ParentTag
        {
            get
            {
                GameplayTagDefinition parentDefinition = Definition.ParentTagDefinition;

                if (parentDefinition == null)
                    return None;

                return parentDefinition.Tag;
            }
        }

        /// <inheritdoc cref="GameplayTagDefinition.Flags" />
        public readonly GameplayTagFlags Flags => Definition.Flags;

        /// <summary>태그의 전체 이름입니다(부모 포함).</summary>
        public readonly string Name
        {
            get
            {
                ValidateIsNotNone();
                return serializedTagName;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [SerializeField]
        [FormerlySerializedAs("m_Name")]
        private string serializedTagName;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GameplayTagDefinition definition;

        internal GameplayTag(GameplayTagDefinition definition)
        {
            this.definition = definition ?? GameplayTagDefinition.NoneTagDefinition;
            serializedTagName = this.definition.TagName;
        }

        /// <inheritdoc cref="GameplayTagDefinition.IsChildOf(GameplayTag)"/>
        public readonly bool IsParentOf(in GameplayTag tag)
        {
            ValidateIsNotNone();
            return Definition.IsParentOf(tag);
        }

        /// <inheritdoc cref="GameplayTagDefinition.IsChildOf(GameplayTag)"/>
        public readonly bool IsChildOf(in GameplayTag parentTag)
        {
            ValidateIsNotNone();
            return Definition.IsChildOf(parentTag);
        }

        public readonly bool Equals(GameplayTag other)
        {
            return definition == other.definition;
        }

        public readonly override bool Equals(object obj)
        {
            if (obj is GameplayTag other)
                return definition == other.definition;

            if (obj is string otherStr)
                return serializedTagName == otherStr;

            return false;
        }

        public readonly override int GetHashCode()
        {
            return Definition.GetHashCode();
        }

        public readonly override string ToString()
        {
            if (IsNone)
                return "<None>";

            return serializedTagName;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(serializedTagName))
            {
                this = None;
                return;
            }

            this = GameplayTagManager.RequestTag(serializedTagName);
            if (!IsValid)
                CDebug.LogWarning($"등록되지 않은 태그 이름입니다: \"{serializedTagName}\".");
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (IsNone)
            {
                serializedTagName = null;
                return;
            }

            serializedTagName = Definition.TagName;
        }

        [Conditional("DEBUG")]
        private readonly void ValidateIsNotNone()
        {
            if (IsNone)
                throw new InvalidOperationException("Cannot perform operation on GameplayTag.None.");
        }

        [Conditional("DEBUG")]
        internal readonly void ValidateIsValid()
        {
            if (IsNone)
                throw new InvalidOperationException("Cannot perform operation on GameplayTag.None.");

            if (!IsValid)
                throw new InvalidOperationException($"GameplayTag \"{serializedTagName}\" is not valid.");
        }

        public static implicit operator GameplayTag(string tagName)
        {
            return GameplayTagManager.RequestTag(tagName);
        }

        public static bool operator ==(in GameplayTag lhs, in GameplayTag rhs)
        {
            if (!lhs.IsValid && !rhs.IsValid)
                return lhs.Name == rhs.Name;

            if (lhs.IsValid != rhs.IsValid)
                return false;

            return lhs.Definition == rhs.Definition;
        }

        public static bool operator !=(in GameplayTag lhs, in GameplayTag rhs)
        {
            if (!lhs.IsValid && !rhs.IsValid)
                return lhs.Name != rhs.Name;

            if (lhs.IsValid != rhs.IsValid)
                return true;

            return lhs.Definition != rhs.Definition;
        }
    }
}
