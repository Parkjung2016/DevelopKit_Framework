using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [Serializable]
    public struct LootEntry
    {
        public int ItemId;
        public int MinCount;
        public int MaxCount;
        public float Weight;
    }

    [CreateAssetMenu(fileName = "SO_LootTable", menuName = "SO/InventorySystem/LootTable")]
    public class LootTableSO : ScriptableObject
    {
        [field: SerializeField] public string TableId { get; set; }
        [field: SerializeField, Tooltip("에디터 목록/프리뷰용 아이콘 (런타임 미사용)")]
        public Sprite EditorIcon { get; set; }
        [field: SerializeField] public LootEntry[] Entries { get; set; } = Array.Empty<LootEntry>();
        [field: SerializeField, Tooltip("한 번 루트 시도에서 항목을 뽑는 횟수")]
        public int RollCount { get; set; } = 1;
        [field: SerializeField, Tooltip("Roll Count가 2 이상일 때, 같은 항목이 여러 번 뽑힐 수 있는지")]
        public bool AllowDuplicateRolls { get; set; } = true;

        public LootTableDefinition ToDefinition() =>
            new(TableId, Entries, RollCount, AllowDuplicateRolls);
    }
}
