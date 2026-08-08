using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_InventorySetup", menuName = "PJDev/InventorySystem/Setup")]
    public class InventorySetupSO : ScriptableObject
    {
        [field: SerializeField] public InventoryConfigSO[] ContainerConfigs { get; set; } =
            System.Array.Empty<InventoryConfigSO>();
    }
}
