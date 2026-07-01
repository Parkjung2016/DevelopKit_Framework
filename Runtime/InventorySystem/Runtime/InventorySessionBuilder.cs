using System;
using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventorySessionBuilder
    {
        private const int DefaultMainSlotCount = 20;

        public static InventoryGroup CreateGroup(
            IReadOnlyList<InventoryContainerConfig> containerConfigs,
            IItemContainerRouter router = null,
            IInventoryDataProvider dataProvider = null)
        {
            var group = dataProvider != null
                ? new InventoryGroup(dataProvider, router)
                : new InventoryGroup(itemDatabase: null, router);

            RegisterContainers(group, containerConfigs, out _);
            return group;
        }

        public static void RegisterContainers(
            InventoryGroup group,
            IReadOnlyList<InventoryContainerConfig> containerConfigs,
            out InventoryContainer primaryContainer)
        {
            primaryContainer = null;
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            if (containerConfigs is { Count: > 0 })
            {
                for (int i = 0; i < containerConfigs.Count; i++)
                {
                    InventoryContainerConfig config = containerConfigs[i];
                    var container = new InventoryContainer(config.SlotCount, null, config.Descriptor);
                    group.RegisterContainer(container);
                    SelectPrimary(ref primaryContainer, container);
                }

                return;
            }

            var fallback = new InventoryContainer(DefaultMainSlotCount, null, InventoryContainerDescriptor.Main());
            group.RegisterContainer(fallback);
            primaryContainer = fallback;
        }

        private static void SelectPrimary(ref InventoryContainer primary, InventoryContainer candidate)
        {
            if (candidate == null)
                return;

            if (primary == null || candidate.Kind == (ContainerKind)InventoryEnumCore.MainContainerKindValue)
                primary = candidate;
        }
    }
}
