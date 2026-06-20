namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public sealed class ScriptableStatDataProvider : IStatDataProvider
    {
        public IStatCatalog StatDatabase { get; }

        public ScriptableStatDataProvider(StatSetupSO setup) =>
            StatDatabase = setup?.StatDatabase;

        public ScriptableStatDataProvider(StatDatabaseSO statDatabase) =>
            StatDatabase = statDatabase;
    }
}
