using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using PJDev.DevelopKit.Framework.PoolSystem.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary><see cref="IReadOnlyGameplayTagContainer"/> 확장 메서드입니다.</summary>
    public static class GameplayTagContainerExtensionMethods
    {
        /// <summary>부모 태그의 유일한 자식 태그를 반환합니다.</summary>
        public static bool TryGetSingleChildTag<T>(this T container, GameplayTag parentTag,
            out GameplayTag childTag, bool warnIfMultiple = true)
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

                if (childTags.Count > 1 && warnIfMultiple)
                {
                    CDebug.LogWarning($"Multiple child tags found for parent tag '{parentTag}'." +
                       $" Returning the first one. Consider using GetChildTags() instead.");

                    childTag = childTags[0];
                    return true;
                }

                childTag = default;
                return false;
            }
        }

        /// <summary>계층을 포함해 태그를 보유하는지 확인합니다.</summary>
        public static bool HasTag<T>(this T container, GameplayTag gameplayTag)
            where T : IReadOnlyGameplayTagContainer
        {
            return container.Indices.Implicit != null
               && BinarySearchUtility.Search(container.Indices.Implicit, gameplayTag.RuntimeIndex) >= 0;
        }

        /// <summary>명시적으로 태그를 보유하는지 확인합니다.</summary>
        public static bool HasTagExact<T>(this T container, GameplayTag gameplayTag)
            where T : IReadOnlyGameplayTagContainer
        {
            return container.Indices.Explicit != null
               && BinarySearchUtility.Search(container.Indices.Explicit, gameplayTag.RuntimeIndex) >= 0;
        }

        /// <summary>다른 컨테이너의 태그 중 하나라도 보유하는지 확인합니다.</summary>
        public static bool HasAny<T, U>(this T container, in U other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
        {
            return HasAnyInternal(container.Indices.Implicit, other?.Indices.Explicit);
        }

        /// <summary>다른 컨테이너의 태그 중 하나라도 명시적으로 보유하는지 확인합니다.</summary>
        public static bool HasAnyExact<T, U>(this T container, in U other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
        {
            return HasAnyInternal(container.Indices.Explicit, other?.Indices.Explicit);
        }

        private static bool HasAnyInternal(List<int> a, List<int> b)
        {
            if (a is null or { Count: 0 } || b is null or { Count: 0 })
                return false;

            // 범위로 조기 종료
            if (a[^1] < b[0] || b[^1] < a[0])
                return false;

            int i = 0, j = 0;
            while (i < a.Count && j < b.Count)
            {
                int av = a[i];
                int bv = b[j];

                if (av == bv)
                    return true;
                if (av < bv)
                    i++;
                else
                    j++;
            }

            return false;
        }

        private static bool HasAllInternal(List<int> a, List<int> b)
        {
            if (b is null or { Count: 0 })
                return true;

            if (a is null or { Count: 0 })
                return false;

            // 범위로 조기 종료
            if (b[0] < a[0] || b[^1] > a[^1])
                return false;

            int i = 0, j = 0;
            while (i < a.Count && j < b.Count)
            {
                int av = a[i];
                int bv = b[j];

                if (av == bv)
                {
                    j++;
                    i++;
                }
                else if (av < bv)
                    i++;
                else
                    return false;
            }

            return j == b.Count;
        }

        /// <summary>다른 컨테이너의 모든 태그를 계층 포함해 보유하는지 확인합니다.</summary>
        public static bool HasAll<T, U>(this T container, in U other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
        {
            return HasAllInternal(container.Indices.Implicit, other?.Indices.Explicit);
        }

        /// <summary>두 컨테이너의 교집합이 요구 태그를 모두 만족하는지 확인합니다.</summary>
        public static bool HasAll<T, U, V>(this T container, in U otherA, in V otherB)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
            where V : IReadOnlyGameplayTagContainer
        {
            if (otherA.IsEmpty && otherB.IsEmpty)
                return true;

            if (otherA.IsEmpty)
                return HasAll(container, otherB);

            if (otherB.IsEmpty)
                return HasAll(container, otherA);

            using (GameplayTagPools.Rent(out GameplayTagContainer intersection))
            {
                intersection.AddIntersection(otherA, otherB);
                bool hasAll = HasAll(container, intersection);
                intersection.Clear();

                return hasAll;
            }
        }

        /// <summary>다른 컨테이너의 모든 태그를 명시적으로 보유하는지 확인합니다.</summary>
        public static bool HasAllExact<T, U>(this T container, in U other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
        {
            return HasAllInternal(container.Indices.Explicit, other?.Indices.Explicit);
        }
    }
}
