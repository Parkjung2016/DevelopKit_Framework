using System;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>
    /// Unity ScriptableObject 에셋을 런타임 API에 연결하는 선택적 확장입니다. 핵심 로직은 SO에 의존하지 않습니다.
    /// </summary>
    public static class InventoryAuthoringExtensions
    {
        public static void RegisterGlobalItemCatalog(this InventorySetupSO setup)
        {
            if (setup?.ItemDatabase != null)
                ItemCatalog.Set(setup.ItemDatabase);
        }

        public static InventoryContainerConfig[] CreateContainerConfigs(this InventorySetupSO setup)
        {
            if (setup == null)
                return Array.Empty<InventoryContainerConfig>();

            InventoryConfigSO[] configs = setup.ContainerConfigs ?? Array.Empty<InventoryConfigSO>();
            if (configs.Length == 0)
                return Array.Empty<InventoryContainerConfig>();

            var result = new InventoryContainerConfig[configs.Length];
            for (int i = 0; i < configs.Length; i++)
                result[i] = configs[i] != null
                    ? configs[i].ToContainerConfig()
                    : new InventoryContainerConfig(20, InventoryContainerDescriptor.Main());

            return result;
        }

        public static InventoryContainerConfig ToContainerConfig(this InventoryConfigSO config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new InventoryContainerConfig(config.SlotCount, config.CreateDescriptor());
        }

        public static bool CanCraft(this InventoryGroup group, RecipeSO recipe, out InventoryFailReason reason) =>
            recipe == null
                ? FailCraft(out reason, InventoryFailReason.InvalidCount)
                : group.CanCraft(recipe.ToDefinition(), out reason);

        public static InventoryChangeResult TryCraft(this InventoryGroup group, RecipeSO recipe) =>
            recipe == null
                ? InventoryChangeResult.Fail(InventoryChangeType.Craft, InventoryFailReason.InvalidCount)
                : group.TryCraft(recipe.ToDefinition());

        public static InventoryChangeResult TryGrantLoot(this InventoryGroup group, LootTableSO table, Random random = null) =>
            table == null
                ? InventoryChangeResult.Fail(InventoryChangeType.Add, InventoryFailReason.InvalidCount)
                : group.TryGrantLoot(table.ToDefinition(), random);

        public static ItemStack[] RollLoot(this LootTableSO table, Random random = null) =>
            table == null ? Array.Empty<ItemStack>() : LootRoller.Roll(table.ToDefinition(), null, random);

        private static bool FailCraft(out InventoryFailReason reason, InventoryFailReason value)
        {
            reason = value;
            return false;
        }
    }
}
