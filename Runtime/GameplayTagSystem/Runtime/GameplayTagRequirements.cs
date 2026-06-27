using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>필수·금지 태그 조건을 표현합니다.</summary>
    [Serializable]
    public struct GameplayTagRequirements
    {
        /// <summary>금지된 태그 컨테이너입니다.</summary>
        public GameplayTagContainer ForbiddenTags => forbiddenTags;

        /// <summary>필수 태그 컨테이너입니다.</summary>
        public GameplayTagContainer RequiredTags => requiredTags;

        [SerializeField]
        [FormerlySerializedAs("m_ForbiddenTags")]
        private GameplayTagContainer forbiddenTags;

        [SerializeField]
        [FormerlySerializedAs("m_RequiredTags")]
        private GameplayTagContainer requiredTags;

        /// <summary>필수·금지 태그가 모두 비어 있는지 여부입니다.</summary>
        public bool IsEmpty
        {
            get => (forbiddenTags == null || forbiddenTags.IsEmpty) &&
                  (requiredTags == null || requiredTags.IsEmpty);
        }

        public GameplayTagRequirements(GameplayTagContainer forbiddenTags, GameplayTagContainer requiredTags)
        {
            this.forbiddenTags = forbiddenTags;
            this.requiredTags = requiredTags;
        }

        /// <summary>컨테이너가 요구 조건을 만족하는지 확인합니다.</summary>
        public readonly bool Matches<T>(in T container) where T : IGameplayTagContainer
        {
            return !container.HasAny(forbiddenTags) && container.HasAll(requiredTags);
        }

        /// <summary>정적·동적 컨테이너를 합쳐 요구 조건을 만족하는지 확인합니다.</summary>
        public readonly bool Matches<T, U>(in T staticContainer, in U dynamicContainer) where T : IGameplayTagContainer where U : IGameplayTagContainer
        {
            bool hasAnyForbiddenTag = staticContainer.HasAny(forbiddenTags) || dynamicContainer.HasAny(forbiddenTags);
            if (hasAnyForbiddenTag)
                return false;

            if (requiredTags == null || requiredTags.IsEmpty)
                return true;

            using (GameplayTagContainerPool.Get(out GameplayTagContainer combined))
            {
                combined.AddTags(staticContainer);
                combined.AddTags(dynamicContainer);
                return combined.HasAll(requiredTags);
            }
        }
    }
}
