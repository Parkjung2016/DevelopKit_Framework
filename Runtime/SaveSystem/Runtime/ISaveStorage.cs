namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface ISaveStorage
    {
        bool Exists(string slotId);

        bool TryRead(string slotId, out byte[] data);

        bool TryWrite(string slotId, byte[] data);

        bool TryDelete(string slotId);
    }
}
