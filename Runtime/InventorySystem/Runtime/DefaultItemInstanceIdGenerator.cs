namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    /// <summary>Snowflake 기반 기본 <see cref="IItemInstanceIdGenerator"/>입니다.</summary>
    public sealed class DefaultItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        public static readonly DefaultItemInstanceIdGenerator Instance = new();

        private readonly SnowflakeItemInstanceIdGenerator snowflake = SnowflakeItemInstanceIdGenerator.Instance;

        public long Generate(int itemId) => snowflake.Generate(itemId);
    }
}
