namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface ISaveSerializer
    {
        byte[] Serialize<T>(T value);

        bool TryDeserialize<T>(byte[] data, out T value);
    }
}
