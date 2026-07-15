using System;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public static class SaveSlotId
    {
        private const int MaxLength = 100;
        private const string InvalidCharacters = "\\/:*?\"<>|";

        public static bool TryNormalize(string slotId, out string normalizedSlotId)
        {
            normalizedSlotId = null;
            if (string.IsNullOrWhiteSpace(slotId))
                return false;

            string value = slotId.Trim();
            if (value.Length > MaxLength || value == "." || value == "..")
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsControl(character) || InvalidCharacters.IndexOf(character) >= 0)
                    return false;
            }

            normalizedSlotId = value;
            return true;
        }
    }
}