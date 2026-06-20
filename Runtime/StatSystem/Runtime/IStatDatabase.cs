namespace PJDev.DevelopKit.Framework.StatSystem.Runtime
{
    public interface IStatDatabase
    {
        bool TryGetDefinition(string statName, out StatDefinition definition);
    }
}
