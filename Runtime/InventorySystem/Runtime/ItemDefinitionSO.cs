using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_Item", menuName = "SO/InventorySystem/Item")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public int ItemId { get; set; }
        [field: SerializeField] public string DisplayName { get; set; }
        [field: SerializeField, TextArea] public string Description { get; set; }
        [field: SerializeField] public Sprite Icon { get; set; }
        [field: SerializeField] public ItemType ItemType { get; set; } = ItemType.General;
        [field: SerializeField] public int MaxStackSize { get; set; } = 99;
        [field: SerializeField] public bool IsStackable { get; set; } = true;
        [field: SerializeField] public bool CanDrop { get; set; } = true;
        [field: SerializeField] public bool CanTrade { get; set; } = true;
        [field: SerializeField] public float Weight { get; set; }
        [field: SerializeField] public string[] Tags { get; set; }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || Tags == null || Tags.Length == 0)
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i] == tag)
                    return true;
            }

            return false;
        }

        public ItemDefinition ToDefinition() =>
            new(
                ItemId,
                MaxStackSize,
                IsStackable,
                ItemType,
                CanDrop,
                CanTrade,
                Weight);
    }
}
