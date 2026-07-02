using System;
using UnityEngine;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

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
        [field: SerializeField] public string ContainerId { get; set; } = "equipment";
        [field: SerializeField] public ContainerKind Kind { get; set; } = ContainerKind.Equipment;
        [field: SerializeField] public int SlotCount { get; set; } = 6;
        [field: SerializeField] public ItemType EquipmentItemType { get; set; } = ItemType.Equipment;
        [field: SerializeField] public string EquipmentTagPrefix { get; set; } = "equip.";
        [field: SerializeField] public string[] SlotCategories { get; set; } = DefaultSlotCategories();
        [field: SerializeField] public EquipmentItemProfileEntry[] ItemProfileOverrides { get; set; } = Array.Empty<EquipmentItemProfileEntry>();

        public void Normalize()
        {
            if (SlotCount < 1)
                SlotCount = 1;

            if (SlotCategories == null || SlotCategories.Length == 0)
            {
                SlotCategories = DefaultSlotCategories();
                return;
            }

            if (SlotCategories.Length < SlotCount)
            {
                var resized = new string[SlotCount];
                Array.Copy(SlotCategories, resized, SlotCategories.Length);
                for (int i = SlotCategories.Length; i < SlotCount; i++)
                    resized[i] = EquipmentSlotCategories.Any;

                SlotCategories = resized;
                return;
            }

            if (SlotCategories.Length > SlotCount)
            {
                var trimmed = new string[SlotCount];
                Array.Copy(SlotCategories, trimmed, SlotCount);
                SlotCategories = trimmed;
            }
        }

        public IEquipmentItemProfileSource CreateProfileSource(IItemDatabase itemDatabase = null)
        {
            Normalize();
            IItemDatabase resolvedDatabase = ItemCatalog.Resolve(itemDatabase);

            var sources = new System.Collections.Generic.List<IEquipmentItemProfileSource>(2);
            if (ItemProfileOverrides is { Length: > 0 })
            {
                var map = new System.Collections.Generic.Dictionary<int, string>();
                for (int i = 0; i < ItemProfileOverrides.Length; i++)
                {
                    EquipmentItemProfileEntry entry = ItemProfileOverrides[i];
                    if (entry.ItemId <= 0 || string.IsNullOrEmpty(entry.SlotCategory))
                        continue;

                    map[entry.ItemId] = entry.SlotCategory;
                }

                if (map.Count > 0)
                    sources.Add(new DictionaryEquipmentProfileSource(map));
            }

            if (resolvedDatabase is IItemCatalog catalog)
                sources.Add(new CatalogTagEquipmentProfileSource(catalog, EquipmentTagPrefix));

            return sources.Count switch
            {
                0 => NullEquipmentProfileSource.Instance,
                1 => sources[0],
                _ => new CompositeEquipmentProfileSource(sources.ToArray())
            };
        }

        public InventoryContainerDescriptor CreateDescriptor(IEquipmentItemProfileSource profileSource = null)
        {
            Normalize();
            IEquipmentItemProfileSource resolvedProfile = profileSource ?? CreateProfileSource(null);
            var slotRule = new EquipmentSlotRule(SlotCategories, resolvedProfile, EquipmentItemType);
            return new InventoryContainerDescriptor(ContainerId, Kind, slotRule);
        }

        /// <summary>
        /// 장비 컨테이너를 생성합니다. <paramref name="itemDatabase"/>를 생략하면
        /// <see cref="ItemCatalog"/>가 등록된 경우 현재 카탈로그를 컨테이너에 고정합니다.
        /// </summary>
        public InventoryContainer CreateContainer(IEquipmentItemProfileSource profileSource = null) =>
            CreateContainer(null, profileSource);

        /// <summary>
        /// 장비 컨테이너를 생성합니다. <paramref name="itemDatabase"/>가 null이고 전역 <see cref="ItemCatalog"/>가 준비되면
        /// <see cref="ItemCatalog.Current"/>를 컨테이너에 고정합니다. 미등록 시 null을 넘겨 런타임 resolve에 맡깁니다.
        /// </summary>
        public InventoryContainer CreateContainer(IItemDatabase itemDatabase, IEquipmentItemProfileSource profileSource = null)
        {
            Normalize();
            IItemDatabase resolvedDatabase = ItemCatalog.Resolve(itemDatabase);
            IEquipmentItemProfileSource resolvedProfile = profileSource ?? CreateProfileSource(resolvedDatabase);
            return new InventoryContainer(SlotCount, resolvedDatabase, CreateDescriptor(resolvedProfile));
        }

        private static string[] DefaultSlotCategories() =>
            new[]
            {
                EquipmentSlotCategories.Weapon,
                EquipmentSlotCategories.Head,
                EquipmentSlotCategories.Chest,
                EquipmentSlotCategories.Hands,
                EquipmentSlotCategories.Feet,
                EquipmentSlotCategories.Ring
            };

#if UNITY_EDITOR
        private void OnValidate() => Normalize();
#endif
    }

    internal sealed class NullEquipmentProfileSource : IEquipmentItemProfileSource
    {
        public static readonly NullEquipmentProfileSource Instance = new();

        public bool TryGetSlotCategory(int itemId, in ItemDefinition definition, out string slotCategory)
        {
            slotCategory = null;
            return false;
        }
    }
}
