namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface ISaveEncryption
    {
        bool IsEnabled { get; }

        byte[] Encrypt(byte[] data);

        byte[] Decrypt(byte[] data);
    }
}