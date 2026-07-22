using System;
using UnityEngine;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public enum UIProgressTextMode
    {
        None,
        Percent,
        Value,
        ValueAndMax,
        Custom
    }

    [DisallowMultipleComponent]
    public class UIProgressBar : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text valueText;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 1f;
        [SerializeField] private float value;
        [SerializeField] private float animationDuration = 0.15f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private UIProgressTextMode textMode = UIProgressTextMode.Percent;
        [SerializeField] private string customTextFormat = "{0:0}/{1:0}";

        private float startValue;
        private float targetValue;
        private float animationTime;
        private bool isAnimating;

        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public float Value => value;
        public float TargetValue => targetValue;
        public float NormalizedValue => Mathf.InverseLerp(minValue, maxValue, value);
        public bool IsAnimating => isAnimating;

        public event Action<float> ValueChanged;

        protected virtual void Awake()
        {
            SanitizeRange();
            targetValue = Mathf.Clamp(value, minValue, maxValue);
            value = targetValue;
            RefreshVisuals();
        }

        protected virtual void OnValidate()
        {
            SanitizeRange();
            value = Mathf.Clamp(value, minValue, maxValue);
            targetValue = value;
            if (!Application.isPlaying)
                RefreshVisuals();
        }

        protected virtual void Update()
        {
            if (!isAnimating)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            animationTime += deltaTime;
            float duration = Mathf.Max(0.0001f, animationDuration);
            float progress = Mathf.Clamp01(animationTime / duration);
            ApplyValue(Mathf.Lerp(startValue, targetValue, progress));
            if (progress >= 1f)
                isAnimating = false;
        }

        public void SetRange(float min, float max, bool keepNormalizedValue = false)
        {
            float normalized = NormalizedValue;
            minValue = min;
            maxValue = Mathf.Max(min, max);
            float nextValue = keepNormalizedValue
                ? Mathf.Lerp(minValue, maxValue, normalized)
                : Mathf.Clamp(value, minValue, maxValue);
            SetValue(nextValue, false);
        }

        public void SetValue(float newValue, bool animate = true)
        {
            targetValue = Mathf.Clamp(newValue, minValue, maxValue);
            if (!animate || animationDuration <= 0f || !isActiveAndEnabled)
            {
                isAnimating = false;
                ApplyValue(targetValue);
                return;
            }

            startValue = value;
            animationTime = 0f;
            isAnimating = !Mathf.Approximately(startValue, targetValue);
            if (!isAnimating)
                RefreshVisuals();
        }

        public void SetNormalizedValue(float normalizedValue, bool animate = true) =>
            SetValue(Mathf.Lerp(minValue, maxValue, Mathf.Clamp01(normalizedValue)), animate);

        public void CompleteImmediately()
        {
            isAnimating = false;
            ApplyValue(targetValue);
        }

        protected virtual string FormatValue()
        {
            switch (textMode)
            {
                case UIProgressTextMode.None:
                    return string.Empty;
                case UIProgressTextMode.Percent:
                    return $"{NormalizedValue * 100f:0}%";
                case UIProgressTextMode.Value:
                    return $"{value:0.##}";
                case UIProgressTextMode.ValueAndMax:
                    return $"{value:0.##} / {maxValue:0.##}";
                case UIProgressTextMode.Custom:
                    return string.Format(customTextFormat, value, maxValue, NormalizedValue);
                default:
                    return string.Empty;
            }
        }

        protected virtual void RefreshVisuals()
        {
            float normalized = NormalizedValue;
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.SetValueWithoutNotify(normalized);
            }

            if (fillImage != null)
                fillImage.fillAmount = normalized;

            if (valueText != null)
            {
                valueText.gameObject.SetActive(textMode != UIProgressTextMode.None);
                valueText.text = FormatValue();
            }
        }

        private void ApplyValue(float newValue)
        {
            float clamped = Mathf.Clamp(newValue, minValue, maxValue);
            bool changed = !Mathf.Approximately(value, clamped);
            value = clamped;
            RefreshVisuals();
            if (changed)
                ValueChanged?.Invoke(value);
        }

        private void SanitizeRange()
        {
            if (maxValue < minValue)
                maxValue = minValue;
            animationDuration = Mathf.Max(0f, animationDuration);
            customTextFormat ??= string.Empty;
        }
    }
}
