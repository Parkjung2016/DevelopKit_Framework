using System.Text;
using PJDev.DevelopKit.Framework.InventorySystem.Runtime;

namespace PJDev.DevelopKit.Framework.EquipmentSystem.Runtime
{
    public readonly struct EquipmentChangeEventArgs
    {
        public EquipmentChangeType ChangeType { get; }
        public int EquipSlotIndex { get; }
        public ItemStack PreviousStack { get; }
        public ItemStack CurrentStack { get; }
        public string EquipmentContainerId { get; }
        public string SourceContainerId { get; }
        public int SourceSlotIndex { get; }
        public InventoryChangeResult InventoryResult { get; }

        public EquipmentChangeEventArgs(
            EquipmentChangeType changeType,
            int equipSlotIndex,
            in ItemStack previousStack,
            in ItemStack currentStack,
            string equipmentContainerId,
            string sourceContainerId,
            int sourceSlotIndex,
            in InventoryChangeResult inventoryResult)
        {
            ChangeType = changeType;
            EquipSlotIndex = equipSlotIndex;
            PreviousStack = previousStack;
            CurrentStack = currentStack;
            EquipmentContainerId = equipmentContainerId;
            SourceContainerId = sourceContainerId;
            SourceSlotIndex = sourceSlotIndex;
            InventoryResult = inventoryResult;
        }

        public override string ToString() => ToDebugJson();

        public string ToDebugJson(bool pretty = true)
        {
            if (!pretty)
            {
                return
                    $"{{\"ChangeType\":\"{ChangeType}\",\"EquipSlotIndex\":{EquipSlotIndex},\"PreviousStack\":{FormatStack(PreviousStack, false)},\"CurrentStack\":{FormatStack(CurrentStack, false)},\"EquipmentContainerId\":{FormatString(EquipmentContainerId)},\"SourceContainerId\":{FormatString(SourceContainerId)},\"SourceSlotIndex\":{SourceSlotIndex},\"InventoryResult\":{InventoryResult.ToDebugJson(false)}}}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"ChangeType\": \"{ChangeType}\",");
            sb.AppendLine($"  \"EquipSlotIndex\": {EquipSlotIndex},");
            sb.AppendLine($"  \"PreviousStack\": {FormatStack(PreviousStack, true)},");
            sb.AppendLine($"  \"CurrentStack\": {FormatStack(CurrentStack, true)},");
            sb.AppendLine($"  \"EquipmentContainerId\": {FormatString(EquipmentContainerId)},");
            sb.AppendLine($"  \"SourceContainerId\": {FormatString(SourceContainerId)},");
            sb.AppendLine($"  \"SourceSlotIndex\": {SourceSlotIndex},");
            sb.AppendLine($"  \"InventoryResult\": {InventoryResult.ToDebugJson(false)}");
            sb.Append('}');
            return sb.ToString();
        }

        private static string FormatString(string value) =>
            string.IsNullOrEmpty(value) ? "null" : $"\"{value}\"";

        private static string FormatStack(ItemStack stack, bool pretty)
        {
            if (stack.IsEmpty)
                return "\"Empty\"";

            return pretty
                ? $"{{ \"ItemId\": {stack.ItemId}, \"Count\": {stack.Count}, \"InstanceId\": {stack.InstanceId} }}"
                : $"{{\"ItemId\":{stack.ItemId},\"Count\":{stack.Count},\"InstanceId\":{stack.InstanceId}}}";
        }
    }
}
