namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public interface IAnimNotifyHandler
    {
        bool TryHandle(AnimNotifySO notify, AnimNotifyContext context);
    }
}
