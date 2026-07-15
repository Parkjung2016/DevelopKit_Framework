using System.Threading;
using System.Threading.Tasks;

namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface IAsyncSaveStorage
    {
        Task<SaveStorageReadResult> ReadAsync(
            string slotId,
            CancellationToken cancellationToken = default);

        Task<bool> WriteAsync(
            string slotId,
            byte[] data,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            string slotId,
            CancellationToken cancellationToken = default);
    }
}