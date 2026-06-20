namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public static class InventorySaveMigrator
    {
        public static void Migrate(ref InventoryGroupSaveData saveData)
        {
            if (saveData == null)
                return;

            if (saveData.Version <= 0)
                MigrateLegacyToV1(saveData);

            saveData.Version = InventorySaveVersions.Current;
        }

        public static void Migrate(ref InventoryContainerSaveData saveData)
        {
            if (saveData?.Slots == null)
                return;

            for (int i = 0; i < saveData.Slots.Length; i++)
            {
                InventorySlotSaveData slot = saveData.Slots[i];
                if (slot.Count < 0)
                    slot.Count = 0;

                saveData.Slots[i] = slot;
            }
        }

        private static void MigrateLegacyToV1(InventoryGroupSaveData saveData)
        {
            if (saveData.Containers == null)
                return;

            for (int i = 0; i < saveData.Containers.Length; i++)
                Migrate(ref saveData.Containers[i]);
        }
    }
}
