using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public static class ItemInstanceFactoryBuilderExtensions
    {
        /// <summary>장비 슬롯 카테고리별 인스턴스 팩토리를 등록합니다.</summary>
        public static ItemInstanceFactoryBuilder ConfigureEquipment(
            this ItemInstanceFactoryBuilder builder,
            IEquipmentItemProfileSource profileSource,
            Action<EquipmentSlotItemInstanceFactory> configure,
            ItemType equipmentItemType = ItemType.Equipment)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (profileSource == null)
                throw new ArgumentNullException(nameof(profileSource));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var equipment = new EquipmentSlotItemInstanceFactory(profileSource, equipmentItemType);
            configure(equipment);
            builder.For(equipmentItemType, equipment);
            return builder;
        }
    }
}
