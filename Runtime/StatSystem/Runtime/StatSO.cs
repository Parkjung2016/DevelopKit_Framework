using System.Collections.Generic;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_Stat", menuName = "SO/StatSystem/Stat")]
    public class StatSO : ScriptableObject
    {
        [field: SerializeField, Delayed] public string StatName { get; set; }
        [field: SerializeField] public string DisplayName { get; set; }
        [field: SerializeField] public float MinValue { get; set; }
        [field: SerializeField] public float MaxValue { get; set; }
        [field: SerializeField] public float BaseValue { get; set; }

        public StatDefinition ToDefinition() =>
            new(StatName, DisplayName, MinValue, MaxValue, BaseValue);

        public Stat CreateRuntime() => Stat.CreateFrom(ToDefinition());

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += RenameAsset;
        }

        private void RenameAsset()
        {
            if (this == null)
                return;

            string assetName = $"SO_{StatName}_Stat";
            UnityEditor.AssetDatabase.RenameAsset(UnityEditor.AssetDatabase.GetAssetPath(this), assetName);
        }
#endif
    }
}
