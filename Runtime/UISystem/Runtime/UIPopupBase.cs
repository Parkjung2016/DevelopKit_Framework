using UnityEngine;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>모달·팝업 공통 베이스입니다. Dimmer 자식을 자동으로 찾거나 생성합니다.</summary>
    public class UIPopupBase : UIViewBase
    {
        private const string DimmerObjectName = "Dimmer";

        private CanvasGroup dimmer;

        protected override void Reset()
        {
            base.Reset();
            EnsureDimmer(createIfMissing: true);
        }

        protected override void Awake()
        {
            EnsureDimmer(createIfMissing: true);
            base.Awake();
        }

        protected override void OnBeforeVisible()
        {
            base.OnBeforeVisible();
            SetDimmerVisible(true);
        }

        protected override void OnBeforeHidden()
        {
            SetDimmerVisible(false);
            base.OnBeforeHidden();
        }

        private void EnsureDimmer(bool createIfMissing)
        {
            if (TryResolveDimmer())
                return;

            if (!createIfMissing)
                return;

            var dimmerObject = new GameObject(DimmerObjectName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            dimmerObject.transform.SetParent(transform, false);
            dimmerObject.transform.SetAsFirstSibling();

            if (dimmerObject.transform is RectTransform rect)
                StretchRectToParent(rect);

            var image = dimmerObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = true;

            dimmer = dimmerObject.GetComponent<CanvasGroup>();
            dimmer.alpha = 0f;
            dimmer.interactable = false;
            dimmer.blocksRaycasts = false;
        }

        private bool TryResolveDimmer()
        {
            if (dimmer != null)
                return true;

            Transform dimmerTransform = transform.Find(DimmerObjectName);
            if (dimmerTransform == null)
                return false;

            dimmer = dimmerTransform.GetComponent<CanvasGroup>();
            if (dimmer == null)
                dimmer = dimmerTransform.gameObject.AddComponent<CanvasGroup>();

            return dimmer != null;
        }

        private void SetDimmerVisible(bool visible)
        {
            if (!TryResolveDimmer())
                return;

            dimmer.alpha = visible ? 1f : 0f;
            dimmer.interactable = visible;
            dimmer.blocksRaycasts = visible;
        }
    }
}
