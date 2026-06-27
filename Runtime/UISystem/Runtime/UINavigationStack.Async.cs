#if UNITASK_INSTALLED
using System.Threading;
using Cysharp.Threading.Tasks;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    internal sealed partial class UINavigationStack
    {
        public async UniTask PushAsync(UIScreenBase screen, object context, CancellationToken cancellationToken)
        {
            if (screen == null)
                return;

            UIScreenBase current = Peek;
            if (current != null && current != screen)
                await current.HideAsync(false, cancellationToken);

            if (!screens.Contains(screen))
                screens.Add(screen);
            else
            {
                screens.Remove(screen);
                screens.Add(screen);
            }

            await screen.ShowAsync(context, cancellationToken);
        }
    }
}
#endif
