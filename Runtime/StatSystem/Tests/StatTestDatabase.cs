using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.StatSystem.Tests
{
    public sealed class StatTestDatabase : InMemoryStatDatabase
    {
        public static readonly StatTestDatabase Shared = new();

        public const string HpStatName = "HP";
        public const string AtkStatName = "ATK";
        public const string DefStatName = "DEF";
        public const string UnknownStatName = "UNKNOWN";

        private StatTestDatabase()
        {
            Register(new StatDefinition(HpStatName, "Health", 0f, 100f, 50f));
            Register(new StatDefinition(AtkStatName, "Attack", 0f, 999f, 10f));
            Register(new StatDefinition(DefStatName, "Defense", 0f, 500f, 5f));
        }
    }
}
