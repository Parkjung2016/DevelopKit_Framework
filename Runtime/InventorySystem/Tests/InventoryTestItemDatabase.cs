using System.Collections.Generic;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Tests
{
    public sealed class InventoryTestItemDatabase : IItemDatabase
    {
        public static readonly InventoryTestItemDatabase Shared = new();

        public const int GeneralItemId = 1000;
        public const int EquipmentItemId = 2000;
        public const int QuestItemId = 3000;
        public const int UnknownItemId = 9999;

        private readonly Dictionary<int, ItemDefinition> definitions;
        private InventoryTestItemDatabase()
        {
            definitions = new Dictionary<int, ItemDefinition>
            {
                [GeneralItemId] = new(GeneralItemId, maxStackSize: 5, isStackable: true, itemType: (ItemType)InventoryTestValues.GeneralType),
                [EquipmentItemId] = new(EquipmentItemId, maxStackSize: 1, isStackable: false, itemType: (ItemType)InventoryTestValues.EquipmentType),
                [QuestItemId] = new(QuestItemId, maxStackSize: 99, isStackable: true, itemType: (ItemType)InventoryTestValues.QuestType, canDrop: false)
            };
        }

        public void Register(ItemDefinition definition) => definitions[definition.ItemId] = definition;

        public bool TryGetDefinition(int itemId, out ItemDefinition definition) =>
            definitions.TryGetValue(itemId, out definition);
    }
}
