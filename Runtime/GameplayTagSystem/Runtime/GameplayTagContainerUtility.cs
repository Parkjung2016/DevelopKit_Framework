using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>두 컨테이너를 조합한 태그 매칭 유틸리티입니다.</summary>
    public static class GameplayTagContainerUtility
    {
        /// <summary>두 컨테이너의 교집합이 요구 태그를 모두 만족하는지 확인합니다.</summary>
        public static bool HasAll<T, U, V>(this T containerA, in U containerB, in V other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
            where V : IReadOnlyGameplayTagContainer
        {
            if (containerA.IsEmpty && containerB.IsEmpty)
                return true;

            if (containerA.IsEmpty)
                return containerB.HasAll(other);

            if (containerB.IsEmpty)
                return containerA.HasAll(other);

            using (GameplayTagPools.Rent(out GameplayTagContainer intersection))
            {
                intersection.AddIntersection(containerA, containerB);
                bool hasAll = intersection.HasAll(other);
                intersection.Clear();

                return hasAll;
            }
        }

        /// <summary>두 컨테이너의 교집합이 요구 태그를 모두 명시적으로 만족하는지 확인합니다.</summary>
        public static bool HasAllExact<T, U, V>(this T containerA, in U containerB, in V other)
            where T : IReadOnlyGameplayTagContainer
            where U : IReadOnlyGameplayTagContainer
            where V : IReadOnlyGameplayTagContainer
        {
            if (containerA.IsEmpty && containerB.IsEmpty)
                return true;

            if (containerA.IsEmpty)
                return containerB.HasAllExact(other);

            if (containerB.IsEmpty)
                return containerA.HasAllExact(other);

            using (GameplayTagPools.Rent(out GameplayTagContainer intersection))
            {
                intersection.AddIntersection(containerA, containerB);
                bool hasAllExact = intersection.HasAllExact(other);
                intersection.Clear();

                return hasAllExact;
            }
        }

        internal static void GetParentTags(List<int> tagIndices, GameplayTag tag, List<GameplayTag> parentTags)
        {
            tag.ValidateIsValid();

            int index = BinarySearchUtility.Search(tagIndices, tag.RuntimeIndex);
            if (index < 0)
                index = ~index;

            for (int i = index - 1; i >= 0; i--)
            {
                GameplayTagDefinition otherTagDefinition
                   = GameplayTagManager.GetDefinitionFromRuntimeIndex(tagIndices[i]);

                if (!otherTagDefinition.IsParentOf(tag))
                    break;

                parentTags.Add(otherTagDefinition.Tag);
            }
        }

        internal static void GetChildTags(List<int> tagIndices, GameplayTag tag, List<GameplayTag> childTags)
        {
            tag.ValidateIsValid();

            if (tagIndices == null)
                return;

            int index = BinarySearchUtility.Search(tagIndices, tag.RuntimeIndex);
            index = index < 0 ? ~index : index + 1;

            for (int i = index; i < tagIndices.Count; i++)
            {
                GameplayTagDefinition otherTagDefinition
                   = GameplayTagManager.GetDefinitionFromRuntimeIndex(tagIndices[i]);

                if (!otherTagDefinition.IsChildOf(tag))
                    break;

                childTags.Add(otherTagDefinition.Tag);
            }
        }
    }
}
