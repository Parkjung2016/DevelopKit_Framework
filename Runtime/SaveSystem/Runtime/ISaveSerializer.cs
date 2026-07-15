namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface ISaveSerializer
    {
        byte[] Serialize<T>(T value);

        T Deserialize<T>(byte[] data);
    }
}