using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>태그 인덱스 컨테이너의 계층 조회를 처리합니다.</summary>
    internal static class GameplayTagContainerUtility
    {
        internal static void GetParentTags(List<int> tagIndices, GameplayTag tag, List<GameplayTag> output)
        {
            tag.ValidateIsValid();
            if (tagIndices == null || output == null)
                return;

            int index = BinarySearchUtility.Search(tagIndices, tag.RuntimeIndex);
            if (index < 0)
                index = ~index;

            for (int i = index - 1; i >= 0; i--)
            {
                GameplayTagDefinition definition =
                    GameplayTagManager.GetDefinitionFromRuntimeIndex(tagIndices[i]);
                if (!definition.IsParentOf(tag))
                    break;

                output.Add(definition.Tag);
            }
        }

        internal static void GetChildTags(List<int> tagIndices, GameplayTag tag, List<GameplayTag> output)
        {
            tag.ValidateIsValid();
            if (tagIndices == null || output == null)
                return;

            int index = BinarySearchUtility.Search(tagIndices, tag.RuntimeIndex);
            index = index < 0 ? ~index : index + 1;

            for (int i = index; i < tagIndices.Count; i++)
            {
                GameplayTagDefinition definition =
                    GameplayTagManager.GetDefinitionFromRuntimeIndex(tagIndices[i]);
                if (!definition.IsChildOf(tag))
                    break;

                output.Add(definition.Tag);
            }
        }
    }
}