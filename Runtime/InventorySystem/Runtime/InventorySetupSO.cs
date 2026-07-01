using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_InventorySetup", menuName = "PJDev/SO/InventorySystem/Setup")]
    public class InventorySetupSO : ScriptableObject
    {
        [field: SerializeField] public InventoryConfigSO[] ContainerConfigs { get; set; } =
            System.Array.Empty<InventoryConfigSO>();

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
