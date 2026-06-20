using System.Threading;

namespace PJDev.DevelopKit.Framework.InventorySystem.Runtime
{
    public sealed class DefaultItemInstanceIdGenerator : IItemInstanceIdGenerator
    {
        public static readonly DefaultItemInstanceIdGenerator Instance = new();

        private long counter;

        public long Generate(int itemId) => Interlocked.Increment(ref counter);
    }
}
