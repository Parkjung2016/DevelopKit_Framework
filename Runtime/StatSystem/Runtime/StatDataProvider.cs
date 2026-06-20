namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public sealed class StatDataProvider : IStatDataProvider
    {
        public IStatCatalog StatDatabase { get; }

        public StatDataProvider(IStatCatalog statDatabase) => StatDatabase = statDatabase;

        public static StatDataProvider FromDatabase(IStatCatalog statDatabase) => new(statDatabase);
    }
}
