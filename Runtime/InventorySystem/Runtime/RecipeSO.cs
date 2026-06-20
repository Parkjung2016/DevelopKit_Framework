using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_Recipe", menuName = "SO/InventorySystem/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        [field: SerializeField] public string RecipeId { get; set; }
        [field: SerializeField] public string DisplayName { get; set; }
        [field: SerializeField, Tooltip("에디터 목록/프리뷰용 아이콘 (런타임 미사용)")]
        public Sprite EditorIcon { get; set; }
        [field: SerializeField] public InventoryRecipeEntry[] Costs { get; set; } = Array.Empty<InventoryRecipeEntry>();
        [field: SerializeField] public InventoryRecipeEntry[] Rewards { get; set; } = Array.Empty<InventoryRecipeEntry>();

        public RecipeDefinition ToDefinition() =>
            RecipeDefinition.Create(RecipeId, Costs, Rewards, DisplayName);
    }
}
