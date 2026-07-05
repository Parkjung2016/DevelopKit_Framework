using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public static class ItemInstanceFactoryBuilderExtensions
    {
        /// <summary>장비 SlotCategory 라우터를 등록합니다. ProfileSource는 장착 규칙과 동일한 소스를 사용하세요.</summary>
        public static ItemInstanceFactoryBuilder ConfigureEquipment(
            this ItemInstanceFactoryBuilder builder,
            IEquipmentItemProfileSource profileSource,
            Action<EquipmentSlotItemInstanceFactory> configure)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (profileSource == null)
                throw new ArgumentNullException(nameof(profileSource));
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var equipment = new EquipmentSlotItemInstanceFactory(profileSource);
            configure(equipment);
            builder.For(ItemType.Equipment, equipment);
            return builder;
        }
    }
}
