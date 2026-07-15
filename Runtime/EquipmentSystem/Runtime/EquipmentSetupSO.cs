using System;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    [Serializable]
    public struct EquipmentItemProfileEntry
    {
        public int ItemId;
        public string SlotCategory;
    }

    [CreateAssetMenu(fileName = "SO_EquipmentSetup", menuName = "PJDev/SO/EquipmentSystem/Setup")]
    public class EquipmentSetupSO : ScriptableObject
    {
        private static readonly string[] BuiltInSlotCategories =
        {
            EquipmentSlotCategories.Weapon,
            EquipmentSlotCategories.Head,
            EquipmentSlotCategories.Chest,
            EquipmentSlotCategories.Hands,
            EquipmentSlotCategories.Feet,
            EquipmentSlotCategories.Ring
        };

        [field: SerializeField] public string ContainerId { get; set; } = "equipment";
        [field: SerializeField] public ContainerKind Kind { get; set; } = ContainerKind.Equipment;
        [field: SerializeField] public int SlotCount { get; set; } = 6;
        [field: SerializeField] public ItemType EquipmentItemType { get; set; } = ItemType.Equipment;
        [field: SerializeField] public string EquipmentTagPrefix { get; set; } = "equip.";
        [field: SerializeField] public string[] SlotCategories { get; set; } = CreateDefaultSlotCategories();
        [field: SerializeField] public EquipmentItemProfileEntry[] ItemProfileOverrides { get; set; } = Array.Empty<EquipmentItemProfileEntry>();

        public int EffectiveSlotCount => Math.Max(1, SlotCount);

        public void Normalize()
        {
            SlotCount = EffectiveSlotCount;
            SlotCategories = CreateSlotCategorySnapshot();
        }

        public string[] CreateSlotCategorySnapshot()
        {
            int count = EffectiveSlotCount;
            var snapshot = new string[count];
            string[] configured = SlotCategories;
            bool useBuiltInDefaults = configured == null || configured.Length == 0;
            string[] source = useBuiltInDefaults ? BuiltInSlotCategories : configured;

            int copiedCount = Math.Min(source.Length, count);
            Array.Copy(source, snapshot, copiedCount);
            for (int i = copiedCount; i < count; i++)
                snapshot[i] = EquipmentSlotCategories.Any;

            return snapshot;
        }

        public IEquipmentItemProfileSource CreateProfileSource(IItemDatabase itemDatabase = null)
        {
            IEquipmentItemProfileSource overrideSource = CreateOverrideProfileSource();
            IEquipmentItemProfileSource catalogSource = ItemCatalog.Resolve(itemDatabase) is IItemCatalog catalog
                ? new CatalogTagEquipmentProfileSource(catalog, EquipmentTagPrefix)
                : null;

            if (overrideSource == null)
                return catalogSource ?? NullEquipmentProfileSource.Instance;

            return catalogSource == null
                ? overrideSource
                : new CompositeEquipmentProfileSource(overrideSource, catalogSource);
        }

        public InventoryContainerDescriptor CreateDescriptor(IEquipmentItemProfileSource profileSource = null)
        {
            IEquipmentItemProfileSource resolvedProfile = profileSource ?? CreateProfileSource();
            return CreateDescriptor(resolvedProfile, CreateSlotCategorySnapshot());
        }

        public InventoryContainer CreateContainer(IEquipmentItemProfileSource profileSource = null) =>
            CreateContainer(null, profileSource);

        public InventoryContainer CreateContainer(
            IItemDatabase itemDatabase,
            IEquipmentItemProfileSource profileSource = null)
        {
            IItemDatabase resolvedDatabase = ItemCatalog.Resolve(itemDatabase);
            IEquipmentItemProfileSource resolvedProfile =
                profileSource ?? CreateProfileSource(resolvedDatabase);
            string[] slotCategories = CreateSlotCategorySnapshot();
            InventoryContainerDescriptor descriptor = CreateDescriptor(
                resolvedProfile,
                slotCategories);

            return new InventoryContainer(EffectiveSlotCount, resolvedDatabase, descriptor);
        }

        public bool TryGetSlotCategory(int equipSlotIndex, out string slotCategory)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= EffectiveSlotCount)
            {
                slotCategory = EquipmentSlotCategories.Any;
                return false;
            }

            string[] configured = SlotCategories;
            if (configured != null && configured.Length > 0)
            {
                slotCategory = equipSlotIndex < configured.Length
                    ? configured[equipSlotIndex]
                    : EquipmentSlotCategories.Any;
                return true;
            }

            slotCategory = equipSlotIndex < BuiltInSlotCategories.Length
                ? BuiltInSlotCategories[equipSlotIndex]
                : EquipmentSlotCategories.Any;
            return true;
        }

        private InventoryContainerDescriptor CreateDescriptor(
            IEquipmentItemProfileSource profileSource,
            string[] slotCategories)
        {
            var slotRule = new EquipmentSlotRule(
                slotCategories,
                profileSource,
                EquipmentItemType);
            return new InventoryContainerDescriptor(ContainerId, Kind, slotRule);
        }

        private IEquipmentItemProfileSource CreateOverrideProfileSource()
        {
            if (ItemProfileOverrides == null || ItemProfileOverrides.Length == 0)
                return null;

            var categoriesByItemId = new System.Collections.Generic.Dictionary<int, string>(
                ItemProfileOverrides.Length);
            for (int i = 0; i < ItemProfileOverrides.Length; i++)
            {
                EquipmentItemProfileEntry entry = ItemProfileOverrides[i];
                if (entry.ItemId > 0 && !string.IsNullOrEmpty(entry.SlotCategory))
                    categoriesByItemId[entry.ItemId] = entry.SlotCategory;
            }

            return categoriesByItemId.Count == 0
                ? null
                : new DictionaryEquipmentProfileSource(categoriesByItemId);
        }

        private static string[] CreateDefaultSlotCategories() =>
            (string[])BuiltInSlotCategories.Clone();

#if UNITY_EDITOR
        private void OnValidate() => Normalize();
#endif
    }

    internal sealed class NullEquipmentProfileSource : IEquipmentItemProfileSource
    {
        public static readonly NullEquipmentProfileSource Instance = new();

        public bool TryGetSlotCategory(
            int itemId,
            in ItemDefinition definition,
            out string slotCategory)
        {
            slotCategory = null;
            return false;
        }
    }
}