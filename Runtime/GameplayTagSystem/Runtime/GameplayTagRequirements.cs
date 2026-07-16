using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>필수 태그와 금지 태그 조건을 묶어 표현합니다.</summary>
    [Serializable]
    public struct GameplayTagRequirements
    {
        /// <summary>보유하면 조건이 실패하는 태그입니다.</summary>
        public readonly GameplayTagContainer ForbiddenTags => forbiddenTags;

        /// <summary>조건을 통과하려면 모두 보유해야 하는 태그입니다.</summary>
        public readonly GameplayTagContainer RequiredTags => requiredTags;

        [SerializeField]
        private GameplayTagContainer forbiddenTags;

        [SerializeField]
        private GameplayTagContainer requiredTags;

        /// <summary>필수 태그와 금지 태그가 모두 비어 있는지 확인합니다.</summary>
        public readonly bool IsEmpty => (forbiddenTags == null || forbiddenTags.IsEmpty) &&
                                        (requiredTags == null || requiredTags.IsEmpty);

        public GameplayTagRequirements(GameplayTagContainer forbiddenTags, GameplayTagContainer requiredTags)
        {
            this.forbiddenTags = forbiddenTags;
            this.requiredTags = requiredTags;
        }

        /// <summary>컨테이너 하나가 조건을 만족하는지 확인합니다.</summary>
        public readonly bool Matches<T>(in T container) where T : IGameplayTagContainer
        {
            return !container.HasAny(forbiddenTags) && container.HasAll(requiredTags);
        }

        /// <summary>두 컨테이너를 합친 태그가 조건을 만족하는지 확인합니다.</summary>
        public readonly bool Matches<T, U>(in T first, in U second)
            where T : IGameplayTagContainer
            where U : IGameplayTagContainer
        {
            if (first.HasAny(forbiddenTags) || second.HasAny(forbiddenTags))
                return false;

            if (requiredTags == null || requiredTags.IsEmpty)
                return true;

            List<int> firstTags = first.Indices.Implicit;
            List<int> secondTags = second.Indices.Implicit;
            List<int> required = requiredTags.Indices.Explicit;

            for (int i = 0; i < required.Count; i++)
            {
                int runtimeIndex = required[i];
                bool existsInFirst = firstTags != null && BinarySearchUtility.Search(firstTags, runtimeIndex) >= 0;
                bool existsInSecond = secondTags != null && BinarySearchUtility.Search(secondTags, runtimeIndex) >= 0;
                if (!existsInFirst && !existsInSecond)
                    return false;
            }

            return true;
        }
    }
}