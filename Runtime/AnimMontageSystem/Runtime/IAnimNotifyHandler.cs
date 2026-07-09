namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public interface IAnimNotifyHandler
    {
        bool TryHandle(AnimNotify notify, AnimNotifyContext context);
    }
}
