using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    internal sealed class GameplayTagRegistrationError
    {
        public string Message { get; }
        public IGameplayTagSource Source { get; }
        public string TagName { get; }

        public GameplayTagRegistrationError(string message, IGameplayTagSource source, string tagName)
        {
            Message = message;
            Source = source;
            TagName = tagName;
        }
    }

    internal sealed class GameplayTagRegistrationContext
    {
        private readonly List<GameplayTagDefinition> definitions = new();
        private readonly Dictionary<string, GameplayTagDefinition> tagsByName = new(StringComparer.Ordinal);
        private readonly List<GameplayTagRegistrationError> registrationErrors = new();
        private bool definitionsGenerated;

        public bool RegisterTag(string name, string description, GameplayTagFlags flags, IGameplayTagSource source)
        {
            Assert.IsFalse(definitionsGenerated, "태그 정의를 생성한 뒤에는 새 태그를 등록할 수 없습니다.");
            Assert.IsNotNull(source, "태그를 등록하려면 소스가 필요합니다.");
            return RegisterTagInternal(name, description, flags, source);
        }

        private bool RegisterTagInternal(
            string name,
            string description,
            GameplayTagFlags flags,
            IGameplayTagSource source)
        {
            if (!GameplayTagUtility.IsNameValid(name, out string errorMessage))
            {
                registrationErrors.Add(new GameplayTagRegistrationError(errorMessage, source, name));
                return false;
            }

            if (tagsByName.TryGetValue(name, out GameplayTagDefinition existing))
            {
                string existingSource = existing.Source?.Name ?? "Generated";
                string duplicateSource = source?.Name ?? "Generated";
                registrationErrors.Add(new GameplayTagRegistrationError(
                    $"태그 '{name}'은(는) '{existingSource}'에 이미 등록되어 있어 " +
                    $"'{duplicateSource}'에서 다시 등록할 수 없습니다.",
                    source,
                    name));
                return false;
            }

            GameplayTagDefinition definition = new(name, description, flags, source);
            tagsByName.Add(name, definition);
            definitions.Add(definition);
            return true;
        }

        public GameplayTagDefinition[] GenerateDefinitions()
        {
            if (definitionsGenerated)
                throw new InvalidOperationException("태그 정의는 한 번만 생성할 수 있습니다.");

            definitionsGenerated = true;
            RegisterMissingParents();
            definitions.Sort(static (a, b) => string.Compare(a.TagName, b.TagName, StringComparison.Ordinal));
            definitions.Insert(0, GameplayTagDefinition.NoneTagDefinition);
            SetRuntimeIndices();
            BuildRelationships();
            BuildHierarchyTags();
            return definitions.ToArray();
        }

        private void RegisterMissingParents()
        {
            GameplayTagDefinition[] registeredDefinitions = definitions.ToArray();
            for (int i = 0; i < registeredDefinitions.Length; i++)
            {
                GameplayTagDefinition definition = registeredDefinitions[i];
                string[] hierarchyNames = GameplayTagUtility.GetHierarchyNames(definition.TagName);
                GameplayTagFlags inheritedFlags = definition.Flags;

                for (int j = hierarchyNames.Length - 1; j >= 0; j--)
                {
                    string name = hierarchyNames[j];
                    if (tagsByName.TryGetValue(name, out GameplayTagDefinition parent))
                    {
                        inheritedFlags |= parent.Flags;
                        continue;
                    }

                    RegisterTagInternal(name, string.Empty, inheritedFlags, definition.Source);
                }
            }
        }

        private void BuildRelationships()
        {
            Dictionary<GameplayTagDefinition, List<GameplayTagDefinition>> childrenByParent = new();

            // 인덱스 0은 None 태그입니다.
            for (int i = 1; i < definitions.Count; i++)
            {
                GameplayTagDefinition definition = definitions[i];
                if (!GameplayTagUtility.TryGetParentName(definition.TagName, out string parentName) ||
                    !tagsByName.TryGetValue(parentName, out GameplayTagDefinition parent))
                {
                    continue;
                }

                if (!childrenByParent.TryGetValue(parent, out List<GameplayTagDefinition> children))
                {
                    children = new List<GameplayTagDefinition>();
                    childrenByParent.Add(parent, children);
                }

                children.Add(definition);
                definition.SetParent(parent);
            }

            foreach (KeyValuePair<GameplayTagDefinition, List<GameplayTagDefinition>> pair in childrenByParent)
                pair.Key.SetChildren(pair.Value);
        }

        private void BuildHierarchyTags()
        {
            for (int i = 1; i < definitions.Count; i++)
            {
                GameplayTagDefinition definition = definitions[i];
                ReadOnlySpan<GameplayTag> parents = definition.ParentTags;
                GameplayTag[] hierarchy = new GameplayTag[parents.Length + 1];
                parents.CopyTo(hierarchy);
                hierarchy[^1] = definition.Tag;
                definition.SetHierarchyTags(hierarchy);
            }
        }

        private void SetRuntimeIndices()
        {
            for (int i = 0; i < definitions.Count; i++)
                definitions[i].SetRuntimeIndex(i);
        }

        public IReadOnlyList<GameplayTagRegistrationError> GetRegistrationErrors()
        {
            return registrationErrors;
        }
    }
}