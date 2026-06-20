using System.Collections.Generic;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventorySaveVersions
    {
        public const int Current = 1;
    }

    public sealed class InventoryImportReport
    {
        private readonly List<string> warnings = new();

        public IReadOnlyList<string> Warnings => warnings;
        public bool HasWarnings => warnings.Count > 0;
        public InventoryChangeResult LastResult { get; internal set; }

        internal void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }
    }
}
