using System;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public sealed class DelegateEquipmentVisualDataSource : IEquipmentVisualDataSource
    {
        private readonly Func<int, EquipmentVisualRecord> OnGetByItemId;

        public DelegateEquipmentVisualDataSource(Func<int, EquipmentVisualRecord> OnGetByItemId)
        {
            this.OnGetByItemId = OnGetByItemId ?? throw new ArgumentNullException(nameof(OnGetByItemId));
        }

        public bool TryGetByItemId(int itemId, out EquipmentVisualRecord record)
        {
            record = OnGetByItemId(itemId);
            return !record.IsEmpty;
        }
    }
}
