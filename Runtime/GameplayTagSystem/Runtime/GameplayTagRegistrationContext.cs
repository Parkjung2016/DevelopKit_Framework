using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<GameplayTagDefinition> definitions = new();
        private Dictionary<string, GameplayTagDefinition> tagsByName = new();
        private List<GameplayTagRegistrationError> registrationErrors = new();

        public bool RegisterTag(string name, string description, GameplayTagFlags flags, IGameplayTagSource source)
        {
            Assert.IsNotNull(source, "Source cannot be null when registering a tag.");
            return RegisterTagInternal(name, description, flags, source);
        }

        private bool RegisterTagInternal(string name, string description, GameplayTagFlags flags, IGameplayTagSource source)
        {
            if (!GameplayTagUtility.IsNameValid(name, out string errorMessage))
            {
                registrationErrors.Add(new GameplayTagRegistrationError(errorMessage, source, name));
                return false;
            }

            if (tagsByName.TryGetValue(name, out GameplayTagDefinition existingDefinition))
            {
                existingDefinition.Description ??= description;

                if (source != null)
                    existingDefinition.AddSource(source);

                return true;
            }

            GameplayTagDefinition definition = new(name, description, flags);

            if (source != null)
                definition.AddSource(source);

            tagsByName.Add(name, definition);
            definitions.Add(definition);

            return true;
        }

        public GameplayTagDefinition[] GenerateDefinitions()
        {
            RegisterMissingParents();
            SortDefinitionsAlphabetically();
            RegisterNoneTag();
            SetTagRuntimeIndices();
            FillParentsAndChildren();
            SetHierarchyTags();

            return definitions.ToArray();
        }

        private void RegisterNoneTag()
        {
            definitions.Insert(0, GameplayTagDefinition.NoneTagDefinition);
        }

        private void RegisterMissingParents()
        {
            List<GameplayTagDefinition> definitionList = new(definitions);
            foreach (GameplayTagDefinition definition in definitionList)
            {
                string[] parentTagNames = GameplayTagUtility.GetHeirarchyNames(definition.TagName);

                GameplayTagFlags flags = definition.Flags;
                foreach (string parentTagName in Enumerable.Reverse(parentTagNames))
                {
                    if (tagsByName.TryGetValue(parentTagName, out GameplayTagDefinition parentTag))
                    {
                        flags |= parentTag.Flags;
                        continue;
                    }

                    RegisterTagInternal(parentTagName, string.Empty, flags, null);
                }
            }
        }

        private void SortDefinitionsAlphabetically()
        {
            definitions.Sort((a, b) => string.Compare(a.TagName, b.TagName, StringComparison.OrdinalIgnoreCase));
        }

        private void FillParentsAndChildren()
        {
            Dictionary<GameplayTagDefinition, List<GameplayTagDefinition>> childrenLists = new();

            // 첫 번째 정의(None 태그)는 건너뜁니다.
            for (int i = 1; i < definitions.Count; i++)
            {
                GameplayTagDefinition definition = definitions[i];

                if (!GameplayTagUtility.TryGetParentName(definition.TagName, out string parentTagName))
                    continue;

                if (!tagsByName.TryGetValue(parentTagName, out GameplayTagDefinition parentDefinition))
                    continue;

                if (!GameplayTagSourceUtility.SharesFileSource(parentDefinition, definition))
                    continue;

                if (!childrenLists.TryGetValue(parentDefinition, out List<GameplayTagDefinition> children))
                {
                    children = new();
                    childrenLists.Add(parentDefinition, children);
                }

                children.Add(definition);
                definition.SetParent(parentDefinition);
            }

            foreach ((GameplayTagDefinition definition, List<GameplayTagDefinition> children) in childrenLists)
                definition.SetChildren(children);
        }

        private void SetHierarchyTags()
        {
            for (int i = 1; i < definitions.Count; i++)
            {
                GameplayTagDefinition definition = definitions[i];

                List<GameplayTag> hierarcyTags = new();

                if (definition.ParentTagDefinition != null)
                    hierarcyTags.AddRange(definition.ParentTagDefinition.HierarchyTags.ToArray());

                hierarcyTags.Add(definition.Tag);
                definition.SetHierarchyTags(hierarcyTags.ToArray());
            }
        }

        private void SetTagRuntimeIndices()
        {
            for (int i = 0; i < definitions.Count; i++)
                definitions[i].SetRuntimeIndex(i);
        }

        public IEnumerable<GameplayTagRegistrationError> GetRegistrationErrors()
        {
            return registrationErrors;
        }
    }
}
