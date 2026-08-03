using TMPro;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.NotificationDotSystem.UI
{
    /// <summary>개수 텍스트만 필요한 기본 알림 프리팹 View입니다.</summary>
    public sealed class NotificationDotCountView : MonoBehaviour, INotificationDotView
    {
        [SerializeField] private TMP_Text countText;
        [SerializeField, Min(1)] private int maxDisplayCount = 99;
        [SerializeField] private bool hideSingleCount = true;

        public void Show(string key, int count)
        {
            if (countText == null)
                return;

            bool visible = !hideSingleCount || count > 1;
            countText.gameObject.SetActive(visible);
            if (visible)
                countText.text = count > maxDisplayCount ? $"{maxDisplayCount}+" : count.ToString();
        }

        public void Hide()
        {
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxDisplayCount = Mathf.Max(1, maxDisplayCount);
        }
#endif
    }
}