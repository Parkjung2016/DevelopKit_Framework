#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>Snowflake 기반 기본 <see cref="IItemInstanceIdGenerator"/>입니다.</summary>
#if UNITY_6000_5_OR_NEWER
    [AutoStaticsCleanup]
#endif
    public sealed partial class DefaultItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        private static DefaultItemInstanceIdGenerator instance = new();

        public static DefaultItemInstanceIdGenerator Instance => instance;

        private readonly SnowflakeItemInstanceIdGenerator snowflake = SnowflakeItemInstanceIdGenerator.Instance;

        public long Generate(int itemId) => snowflake.Generate(itemId);
    }
}
