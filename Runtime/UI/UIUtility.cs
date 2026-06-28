using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PJDev.UI
{
    /// <summary>uGUI 컴포넌트 조작용 정적 함수입니다. 확장 메서드와 동일한 동작을 제공합니다.</summary>
    public static class UIUtility
    {
        public static void BindButton(Button button, UnityAction onClick)
        {
            if (button == null || onClick == null)
                return;

            button.onClick.RemoveListener(onClick);
            button.onClick.AddListener(onClick);
        }

        public static void UnbindButton(Button button, UnityAction onClick)
        {
            if (button == null || onClick == null)
                return;

            button.onClick.RemoveListener(onClick);
        }

        public static void BindToggle(Toggle toggle, UnityAction<bool> onValueChanged)
        {
            if (toggle == null || onValueChanged == null)
                return;

            toggle.onValueChanged.RemoveListener(onValueChanged);
            toggle.onValueChanged.AddListener(onValueChanged);
        }

        public static void UnbindToggle(Toggle toggle, UnityAction<bool> onValueChanged)
        {
            if (toggle == null || onValueChanged == null)
                return;

            toggle.onValueChanged.RemoveListener(onValueChanged);
        }

        public static void BindSlider(Slider slider, UnityAction<float> onValueChanged)
        {
            if (slider == null || onValueChanged == null)
                return;

            slider.onValueChanged.RemoveListener(onValueChanged);
            slider.onValueChanged.AddListener(onValueChanged);
        }

        public static void UnbindSlider(Slider slider, UnityAction<float> onValueChanged)
        {
            if (slider == null || onValueChanged == null)
                return;

            slider.onValueChanged.RemoveListener(onValueChanged);
        }

        public static void BindInputField(InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.RemoveListener(onValueChanged);
            inputField.onValueChanged.AddListener(onValueChanged);
        }

        public static void UnbindInputField(InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.RemoveListener(onValueChanged);
        }

        public static void BindTMPInputField(TMP_InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.RemoveListener(onValueChanged);
            inputField.onValueChanged.AddListener(onValueChanged);
        }

        public static void UnbindTMPInputField(TMP_InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.RemoveListener(onValueChanged);
        }

        public static void BindTMPInputFieldEndEdit(TMP_InputField inputField, UnityAction<string> onEndEdit)
        {
            if (inputField == null || onEndEdit == null)
                return;

            inputField.onEndEdit.RemoveListener(onEndEdit);
            inputField.onEndEdit.AddListener(onEndEdit);
        }

        public static void UnbindTMPInputFieldEndEdit(TMP_InputField inputField, UnityAction<string> onEndEdit)
        {
            if (inputField == null || onEndEdit == null)
                return;

            inputField.onEndEdit.RemoveListener(onEndEdit);
        }

        public static void SetText(Text text, string value)
        {
            if (text == null)
                return;

            text.text = value ?? string.Empty;
        }

        public static void SetText(TMP_Text text, string value)
        {
            if (text == null)
                return;

            text.text = value ?? string.Empty;
        }

        public static void SetText(TextMeshProUGUI text, string value) => SetText((TMP_Text)text, value);

        public static void SetText(TextMeshPro text, string value) => SetText((TMP_Text)text, value);

        public static void SetText(GameObject target, string value)
        {
            if (target == null)
                return;

            TMP_Text tmp = target.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                SetText(tmp, value);
                return;
            }

            SetText(target.GetComponent<Text>(), value);
        }

        public static void SetText(Component target, string value)
        {
            if (target == null)
                return;

            switch (target)
            {
                case TMP_Text tmp:
                    SetText(tmp, value);
                    break;
                case Text text:
                    SetText(text, value);
                    break;
                default:
                    SetText(target.gameObject, value);
                    break;
            }
        }

        public static void SetTMPInputField(TMP_InputField inputField, string value, bool notify = false)
        {
            if (inputField == null)
                return;

            if (notify)
                inputField.text = value ?? string.Empty;
            else
                inputField.SetTextWithoutNotify(value ?? string.Empty);
        }

        public static void SetToggle(Toggle toggle, bool isOn, bool notify = false)
        {
            if (toggle == null)
                return;

            toggle.SetIsOnWithoutNotify(isOn);
            if (notify)
                toggle.onValueChanged.Invoke(isOn);
        }

        public static void SetSlider(Slider slider, float value, bool notify = false)
        {
            if (slider == null)
                return;

            if (notify)
                slider.value = value;
            else
                slider.SetValueWithoutNotify(value);
        }

        public static void SetImage(Image image, Sprite sprite, bool preserveEnabledState = true)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            if (!preserveEnabledState)
                image.enabled = sprite != null;
        }

        public static void SetImageFill(Image image, float fillAmount)
        {
            if (image == null)
                return;

            image.fillAmount = Mathf.Clamp01(fillAmount);
        }

        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        public static void SetAlpha(CanvasGroup canvasGroup, float alpha, bool affectInteractable = false)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = Mathf.Clamp01(alpha);

            if (affectInteractable)
            {
                bool visible = canvasGroup.alpha > 0.001f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        public static void SetColor(Graphic graphic, Color color)
        {
            if (graphic == null)
                return;

            graphic.color = color;
        }

        public static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable == null)
                return;

            selectable.interactable = interactable;
        }

        public static void SetVisible(GameObject target, bool visible)
        {
            if (target != null)
                target.SetActive(visible);
        }

        public static void SetVisible(Component target, bool visible)
        {
            if (target != null)
                target.gameObject.SetActive(visible);
        }
    }
}
