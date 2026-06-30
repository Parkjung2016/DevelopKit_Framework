using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed class CompositeEquipmentProfileSource : IEquipmentItemProfileSource
    {
        private readonly IEquipmentItemProfileSource[] sources;

        public CompositeEquipmentProfileSource(params IEquipmentItemProfileSource[] sources) =>
            this.sources = sources ?? Array.Empty<IEquipmentItemProfileSource>();

        public bool TryGetSlotCategory(int itemId, in ItemDefinition definition, out string slotCategory)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                IEquipmentItemProfileSource source = sources[i];
                if (source != null && source.TryGetSlotCategory(itemId, definition, out slotCategory))
                    return true;
            }

            slotCategory = null;
            return false;
        }
    }
}
