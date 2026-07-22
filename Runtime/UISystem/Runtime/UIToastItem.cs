using System;
using UnityEngine;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    [Serializable]
    public struct UIToastStyle
    {
        public ToastType Type;
        public Sprite Icon;
        public Color BackgroundColor;
        public Color TextColor;
    }

    public class UIToastItem : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text messageText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        public virtual void Show(in ToastRequest request, in UIToastStyle style)
        {
            EnsureCanvasGroup();

            if (messageText != null)
            {
                messageText.text = request.Message;
                messageText.color = style.TextColor;
            }

            if (iconImage != null)
            {
                Sprite icon = request.Icon != null ? request.Icon : style.Icon;
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }

            if (backgroundImage != null)
                backgroundImage.color = style.BackgroundColor;

            SetAlpha(0f);
            gameObject.SetActive(true);
        }

        public virtual void SetAlpha(float alpha)
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public virtual void ResetItem()
        {
            SetAlpha(0f);
            gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
