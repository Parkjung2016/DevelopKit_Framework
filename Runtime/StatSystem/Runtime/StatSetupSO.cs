using UnityEngine;

namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    [CreateAssetMenu(fileName = "SO_StatSetup", menuName = "PJDev/SO/StatSystem/Setup")]
    public class StatSetupSO : ScriptableObject
    {
        [field: SerializeField] public StatDatabaseSO StatDatabase { get; set; }

        public IStatDataProvider CreateDataProvider() => new ScriptableStatDataProvider(this);
    }
}
