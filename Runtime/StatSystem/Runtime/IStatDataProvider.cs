namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    /// <summary>
    /// Stat runtime data entry point.
    /// Implement or compose from SO, CSV, bytes, server payload, etc.
    /// </summary>
    public interface IStatDataProvider
    {
        IStatCatalog StatDatabase { get; }
    }
}
