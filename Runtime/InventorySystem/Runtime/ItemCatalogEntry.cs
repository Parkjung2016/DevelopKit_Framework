namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public readonly struct ItemCatalogEntry
    {
        public ItemDefinition Definition { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string IconKey { get; }
        public string[] Tags { get; }

        public int ItemId => Definition.ItemId;

        public ItemCatalogEntry(
            ItemDefinition definition,
            string displayName = null,
            string description = null,
            string iconKey = null,
            string[] tags = null)
        {
            Definition = definition;
            DisplayName = displayName;
            Description = description;
            IconKey = iconKey;
            Tags = tags ?? System.Array.Empty<string>();
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || Tags.Length == 0)
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i] == tag)
                    return true;
            }

            return false;
        }

        public static ItemCatalogEntry FromDefinition(in ItemDefinition definition, string[] tags = null) =>
            new(definition, tags: tags);
    }
}
