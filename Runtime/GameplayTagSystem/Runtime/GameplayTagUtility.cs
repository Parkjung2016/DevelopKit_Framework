using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime
{
    /// <summary>게임플레이 태그 이름·계층 관련 유틸리티입니다.</summary>
    public static class GameplayTagUtility
    {
        internal static void WarnNotExplictlyAddedTagRemoval(GameplayTag gameplayTag)
        {
            CDebug.LogWarning(
                $"Attempted to remove tag {gameplayTag} from tag count container, but it is not explicitly added to the container.");
        }

        internal static void WarnNotExplicitTagsRemoval(GameplayTagEnumerator tags)
        {
            foreach (GameplayTag tag in tags)
                WarnNotExplictlyAddedTagRemoval(tag);
        }

        /// <summary>
        /// 지정한 태그 계층의 모든 태그 이름을 반환합니다.
        /// 예: "A.B.C"이면 ["A", "A.B", "A.B.C"]입니다.
        /// </summary>
        public static string[] GetHeirarchyNames(string tagName)
        {
            ValidateName(tagName);

            int level = GetHeirarchyLevelFromName(tagName);
            string[] names = new string[level];
            names[--level] = tagName;

            for (int i = tagName.Length - 1; i >= 0; i--)
            {
                if (tagName[i] == '.')
                {
                    string name = tagName[..i];
                    names[--level] = name;

                    if (level == -1)
                        break;
                }
            }

            return names;
        }

        /// <summary>태그 이름에서 부모 태그 이름을 추출합니다.</summary>
        public static bool TryGetParentName(string name, out string parentName)
        {
            ValidateName(name);

            for (int i = name.Length - 1; i >= 0; i--)
            {
                if (name[i] == '.')
                {
                    parentName = name[..i];
                    return true;
                }
            }

            parentName = null;
            return false;
        }

        /// <summary>태그 이름의 계층 깊이를 반환합니다.</summary>
        public static int GetHeirarchyLevelFromName(string name)
        {
            ValidateName(name);

            int level = 1;
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] == '.')
                {
                    level++;
                }
            }

            return level;
        }

        /// <summary>부모를 제외한 태그 라벨을 반환합니다.</summary>
        public static string GetLabel(string name)
        {
            ValidateName(name);

            int indexOfPoint = name.LastIndexOf('.');
            if (indexOfPoint == -1)
                return name;

            return name[(indexOfPoint + 1)..];
        }

        /// <summary>태그 이름이 유효한지 검사합니다.</summary>
        public static bool IsNameValid(string name, out string errorMessage)
        {
            static bool IsValidLabelCharacter(char c)
            {
                return char.IsLetterOrDigit(c) || c == '_';
            }

            static bool AcceptLabel(string name, ref int position)
            {
                if (position >= name.Length || !IsValidLabelCharacter(name[position]))
                    return false;

                position++;
                while (position < name.Length && IsValidLabelCharacter(name[position]))
                {
                    position++;
                }

                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                errorMessage = "Tag name cannot be null or empty.";
                return false;
            }

            int position = 0;
            if (AcceptLabel(name, ref position))
            {
                while (position < name.Length && name[position] == '.')
                {
                    position++;
                    if (!AcceptLabel(name, ref position))
                    {
                        errorMessage = $"Invalid tag name '{name}'. Unexpected character at position {position}.";
                        return false;
                    }
                }
            }

            if (position == name.Length)
            {
                errorMessage = null;
                return true;
            }

            errorMessage = $"Invalid tag name '{name}'. Unexpected character at position {position}.";
            return false;
        }

        /// <summary>태그 이름이 유효하지 않으면 예외를 던집니다.</summary>
        public static void ValidateName(string name)
        {
            if (!IsNameValid(name, out string errorMessage))
                throw new ArgumentException(errorMessage);
        }
    }
}
