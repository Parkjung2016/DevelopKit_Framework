using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class WeightCapacityRule : IContainerCapacityRuleEx
    {
        private readonly float maxWeight;

        public float MaxWeight => maxWeight;

        public WeightCapacityRule(float maxWeight) => this.maxWeight = maxWeight;

        public bool CanAdd(InventoryContainer container, in ItemDefinition definition, int count)
        {
            if (container == null || maxWeight <= 0f)
                return true;

            float incomingWeight = definition.Weight * count;
            if (incomingWeight <= 0f)
                return true;

            return container.GetTotalWeight() + incomingWeight <= maxWeight + Mathf.Epsilon;
        }

        public bool CanAdd(in ItemDefinition definition, int count, int currentItemCount, int occupiedSlotCount) =>
            definition.Weight * count <= maxWeight;
    }
}
