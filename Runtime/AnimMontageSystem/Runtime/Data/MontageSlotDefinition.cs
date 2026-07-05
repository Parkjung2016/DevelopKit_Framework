using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [Serializable]
    public sealed class MontageSlotDefinition
    {
        [SerializeField] private string slotName = "DefaultSlot";
        [SerializeField] private int groupIndex;

        public string SlotName => slotName;
        public int GroupIndex => groupIndex;
    }
}
