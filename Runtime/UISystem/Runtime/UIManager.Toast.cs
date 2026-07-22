using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public sealed partial class UIManager
    {
        public UIViewResult<UIToastView> ShowToast(
            string message,
            ToastType type = ToastType.Info,
            float duration = 0f,
            Sprite icon = null,
            string duplicateKey = null,
            string viewId = UIBuiltInViewIds.Toast) =>
            ShowToast(new ToastRequest(message, type, duration, icon, duplicateKey), viewId);

        public UIViewResult<UIToastView> ShowToast(
            in ToastRequest request,
            string viewId = UIBuiltInViewIds.Toast)
        {
            if (string.IsNullOrWhiteSpace(viewId))
                return UIViewResult<UIToastView>.Failed("Toast view ID cannot be empty.");

            return OpenPopup<UIToastView>(
                viewId,
                new UIViewOpenOptions(request, layerId: UILayers.System));
        }
    }
}
