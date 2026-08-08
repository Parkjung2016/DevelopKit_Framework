using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime.PoolSystem;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary><see cref="IReadOnlyGameplayTagContainer"/>의 조회 기능을 제공합니다.</summary>
    public static class GameplayTagContainerExtensions
    {
        /// <summary>부모 아래에 태그가 정확히 하나 있을 때만 반환합니다.</summary>
        public static bool TryGetSingleChildTag<T>(
            this T container,
            GameplayTag parentTag,
            out GameplayTag childTag)
            where T : IReadOnlyGameplayTagContainer
        {
            using (ListPool<GameplayTag>.Rent(out List<GameplayTag> childTags))
            {
                container.GetChildTags(parentTag, childTags);
                if (childTags.Count == 1)
                {
                    childTag = childTags[0];
                    return true;
                }

                childTag = GameplayTag.None;
                return false;
            }
        }

        public static bool HasTag<T>(this T container, GameplayTag tag)
            where T : IReadOnlyGameplayTagContainer =>
            tag.IsValid
            && container.Indices.Implicit != null
            && BinarySearchUtility.Search(container.Indices.Implicit, tag.RuntimeIndex) >= 0;

        public static bool HasTagExact<T>(this T container, GameplayTag tag)
            where T : IReadOnlyGameplayTagContainer =>
            tag.IsValid
            && container.Indices.Explicit != null
            && BinarySearchUtility.Search(container.Indices.Explicit, tag.RuntimeIndex) >= 0;

        public static bool HasAny<T, TOther>(this T container, in TOther other)
            where T : IReadOnlyGameplayTagContainer
            where TOther : IReadOnlyGameplayTagContainer =>
            ContainsAny(container.Indices.Implicit, other?.Indices.Explicit);

        public static bool HasAnyExact<T, TOther>(this T container, in TOther other)
            where T : IReadOnlyGameplayTagContainer
            where TOther : IReadOnlyGameplayTagContainer =>
            ContainsAny(container.Indices.Explicit, other?.Indices.Explicit);

        public static bool HasAll<T, TOther>(this T container, in TOther other)
            where T : IReadOnlyGameplayTagContainer
            where TOther : IReadOnlyGameplayTagContainer =>
            ContainsAll(container.Indices.Implicit, other?.Indices.Explicit);

        public static bool HasAllExact<T, TOther>(this T container, in TOther other)
            where T : IReadOnlyGameplayTagContainer
            where TOther : IReadOnlyGameplayTagContainer =>
            ContainsAll(container.Indices.Explicit, other?.Indices.Explicit);

        private static bool ContainsAny(List<int> first, List<int> second)
        {
            if (first is null or { Count: 0 } || second is null or { Count: 0 })
                return false;

            if (first[^1] < second[0] || second[^1] < first[0])
                return false;

            int firstIndex = 0;
            int secondIndex = 0;
            while (firstIndex < first.Count && secondIndex < second.Count)
            {
                int firstValue = first[firstIndex];
                int secondValue = second[secondIndex];
                if (firstValue == secondValue)
                    return true;

                if (firstValue < secondValue)
                    firstIndex++;
                else
                    secondIndex++;
            }

            return false;
        }

        private static bool ContainsAll(List<int> values, List<int> required)
        {
            if (required is null or { Count: 0 })
                return true;
            if (values is null or { Count: 0 })
                return false;
            if (required[0] < values[0] || required[^1] > values[^1])
                return false;

            int valueIndex = 0;
            int requiredIndex = 0;
            while (valueIndex < values.Count && requiredIndex < required.Count)
            {
                int value = values[valueIndex];
                int target = required[requiredIndex];
                if (value == target)
                {
                    valueIndex++;
                    requiredIndex++;
                }
                else if (value < target)
                {
                    valueIndex++;
                }
                else
                {
                    return false;
                }
            }

            return requiredIndex == required.Count;
        }
    }
}