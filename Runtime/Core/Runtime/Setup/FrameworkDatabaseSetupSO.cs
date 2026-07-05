using PJDev.DevelopKit.Framework.InventorySystem.Runtime;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Core.Runtime
{
    [CreateAssetMenu(fileName = "SO_FrameworkDatabaseSetup", menuName = "PJDev/SO/Framework/Database Setup")]
    public sealed class FrameworkDatabaseSetupSO : ScriptableObject
    {
        [field: SerializeField] public InventoryDatabaseSetupSO InventoryDatabases { get; set; }
        [field: SerializeField] public StatDatabaseSO StatDatabase { get; set; }

        public void RegisterAll()
        {
            FrameworkGlobals.RegisterDatabases(InventoryDatabases);
            FrameworkGlobals.RegisterStatDatabase(StatDatabase);
        }
    }
}
