using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PJDev.UI
{
    /// <summary>uGUI 컴포넌트 확장 메서드입니다.</summary>
    public static class UIExtensions
    {
        public static void BindButton(this Button button, UnityAction onClick) =>
            UIUtility.BindButton(button, onClick);

        public static void UnbindButton(this Button button, UnityAction onClick) =>
            UIUtility.UnbindButton(button, onClick);

        public static void BindToggle(this Toggle toggle, UnityAction<bool> onValueChanged) =>
            UIUtility.BindToggle(toggle, onValueChanged);

        public static void UnbindToggle(this Toggle toggle, UnityAction<bool> onValueChanged) =>
            UIUtility.UnbindToggle(toggle, onValueChanged);

        public static void BindSlider(this Slider slider, UnityAction<float> onValueChanged) =>
            UIUtility.BindSlider(slider, onValueChanged);

        public static void UnbindSlider(this Slider slider, UnityAction<float> onValueChanged) =>
            UIUtility.UnbindSlider(slider, onValueChanged);

        public static void BindInputField(this InputField inputField, UnityAction<string> onValueChanged) =>
            UIUtility.BindInputField(inputField, onValueChanged);

        public static void UnbindInputField(this InputField inputField, UnityAction<string> onValueChanged) =>
            UIUtility.UnbindInputField(inputField, onValueChanged);

        public static void BindTMPInputField(this TMP_InputField inputField, UnityAction<string> onValueChanged) =>
            UIUtility.BindTMPInputField(inputField, onValueChanged);

        public static void UnbindTMPInputField(this TMP_InputField inputField, UnityAction<string> onValueChanged) =>
            UIUtility.UnbindTMPInputField(inputField, onValueChanged);

        public static void BindTMPInputFieldEndEdit(this TMP_InputField inputField, UnityAction<string> onEndEdit) =>
            UIUtility.BindTMPInputFieldEndEdit(inputField, onEndEdit);

        public static void UnbindTMPInputFieldEndEdit(this TMP_InputField inputField, UnityAction<string> onEndEdit) =>
            UIUtility.UnbindTMPInputFieldEndEdit(inputField, onEndEdit);

        public static void BindEvents(this UIEventSubscriptions subscriptions, Button button, UnityAction onClick) =>
            subscriptions.BindButton(button, onClick);

        public static void BindEvents(this UIEventSubscriptions subscriptions, Toggle toggle, UnityAction<bool> onValueChanged) =>
            subscriptions.BindToggle(toggle, onValueChanged);

        public static void BindEvents(this UIEventSubscriptions subscriptions, Slider slider, UnityAction<float> onValueChanged) =>
            subscriptions.BindSlider(slider, onValueChanged);

        public static void BindEvents(this UIEventSubscriptions subscriptions, TMP_InputField inputField, UnityAction<string> onValueChanged) =>
            subscriptions.BindTMPInputField(inputField, onValueChanged);

        public static void BindEvent(this UIEventSubscriptions subscriptions, Action bind, Action unbind) =>
            subscriptions.Bind(bind, unbind);

        public static void BindEvent(this UIEventSubscriptions subscriptions, UnityEvent unityEvent, UnityAction callback) =>
            subscriptions.Bind(unityEvent, callback);

        public static void BindEvent<T>(this UIEventSubscriptions subscriptions, UnityEvent<T> unityEvent, UnityAction<T> callback) =>
            subscriptions.Bind(unityEvent, callback);

        public static void BindEvent<THandler>(
            this UIEventSubscriptions subscriptions,
            THandler handler,
            Action<THandler> bind,
            Action<THandler> unbind) =>
            subscriptions.Bind(handler, bind, unbind);

        public static void SetText(this Text text, string value) =>
            UIUtility.SetText(text, value);

        public static void SetText(this TMP_Text text, string value) =>
            UIUtility.SetText(text, value);

        public static void SetText(this TextMeshProUGUI text, string value) =>
            UIUtility.SetText(text, value);

        public static void SetText(this TextMeshPro text, string value) =>
            UIUtility.SetText(text, value);

        public static void SetText(this GameObject target, string value) =>
            UIUtility.SetText(target, value);

        public static void SetText(this Component target, string value) =>
            UIUtility.SetText(target, value);

        public static void SetTMPInputField(this TMP_InputField inputField, string value, bool notify = false) =>
            UIUtility.SetTMPInputField(inputField, value, notify);

        public static void SetToggle(this Toggle toggle, bool isOn, bool notify = false) =>
            UIUtility.SetToggle(toggle, isOn, notify);

        public static void SetSlider(this Slider slider, float value, bool notify = false) =>
            UIUtility.SetSlider(slider, value, notify);

        public static void SetImage(this Image image, Sprite sprite, bool preserveEnabledState = true) =>
            UIUtility.SetImage(image, sprite, preserveEnabledState);

        public static void SetImageFill(this Image image, float fillAmount) =>
            UIUtility.SetImageFill(image, fillAmount);

        public static void SetAlpha(this Graphic graphic, float alpha) =>
            UIUtility.SetAlpha(graphic, alpha);

        public static void SetAlpha(this CanvasGroup canvasGroup, float alpha, bool affectInteractable = false) =>
            UIUtility.SetAlpha(canvasGroup, alpha, affectInteractable);

        public static void SetColor(this Graphic graphic, Color color) =>
            UIUtility.SetColor(graphic, color);

        public static void SetInteractable(this Selectable selectable, bool interactable) =>
            UIUtility.SetInteractable(selectable, interactable);

        public static void SetVisible(this GameObject target, bool visible) =>
            UIUtility.SetVisible(target, visible);

        public static void SetVisible(this Component target, bool visible) =>
            UIUtility.SetVisible(target, visible);
    }
}
