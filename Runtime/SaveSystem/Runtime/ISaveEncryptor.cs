namespace PJDev.DevelopKit.Framework.SaveSystem.Runtime
{
    public interface ISaveEncryptor
    {
        byte[] Encrypt(byte[] plain);

        bool TryDecrypt(byte[] cipher, out byte[] plain);
    }
}
