using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_InventorySetup", menuName = "PJDev/SO/InventorySystem/Setup")]
    public class InventorySetupSO : ScriptableObject
    {
        [field: SerializeField] public ItemDatabaseSO ItemDatabase { get; set; }
        [field: SerializeField] public RecipeDatabaseSO RecipeDatabase { get; set; }
        [field: SerializeField] public LootTableDatabaseSO LootTableDatabase { get; set; }
        [field: SerializeField] public InventoryConfigSO[] ContainerConfigs { get; set; } = System.Array.Empty<InventoryConfigSO>();

        public IInventoryDataProvider CreateDataProvider() =>
            new ScriptableInventoryDataProvider(this);

        public InventoryContainerDescriptor[] CreateDescriptors()
        {
            InventoryConfigSO[] configs = ContainerConfigs ?? System.Array.Empty<InventoryConfigSO>();
            var descriptors = new InventoryContainerDescriptor[configs.Length];

            for (int i = 0; i < configs.Length; i++)
                descriptors[i] = configs[i] != null ? configs[i].CreateDescriptor() : InventoryContainerDescriptor.Main();

            return descriptors;
        }
    }
}
