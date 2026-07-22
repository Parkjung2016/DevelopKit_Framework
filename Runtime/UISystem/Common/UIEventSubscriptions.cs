using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PJDev.UI
{
    /// <summary>UI 이벤트 구독을 모아 두었다가 한 번에 해제합니다.</summary>
    public sealed class UIEventSubscriptions
    {
        private readonly List<Action> unbinders = new();

        public int Count => unbinders.Count;

        public void Clear()
        {
            for (int i = unbinders.Count - 1; i >= 0; i--)
                unbinders[i]?.Invoke();

            unbinders.Clear();
        }

        public void Add(Action unbind)
        {
            if (unbind != null)
                unbinders.Add(unbind);
        }

        public void Bind(Action bind, Action unbind)
        {
            bind?.Invoke();
            Add(unbind);
        }

        public void Bind(UnityEvent unityEvent, UnityAction callback)
        {
            if (unityEvent == null || callback == null)
                return;

            unityEvent.AddListener(callback);
            Add(() => unityEvent.RemoveListener(callback));
        }

        public void Bind<T>(UnityEvent<T> unityEvent, UnityAction<T> callback)
        {
            if (unityEvent == null || callback == null)
                return;

            unityEvent.AddListener(callback);
            Add(() => unityEvent.RemoveListener(callback));
        }

        public void Bind<THandler>(THandler handler, Action<THandler> bind, Action<THandler> unbind)
        {
            if (handler == null || bind == null || unbind == null)
                return;

            bind(handler);
            Add(() => unbind(handler));
        }

        public void BindButton(Button button, UnityAction onClick)
        {
            if (button == null || onClick == null)
                return;

            button.onClick.AddListener(onClick);
            Add(() =>
            {
                if (button != null)
                    button.onClick.RemoveListener(onClick);
            });
        }

        public void BindToggle(Toggle toggle, UnityAction<bool> onValueChanged)
        {
            if (toggle == null || onValueChanged == null)
                return;

            toggle.onValueChanged.AddListener(onValueChanged);
            Add(() =>
            {
                if (toggle != null)
                    toggle.onValueChanged.RemoveListener(onValueChanged);
            });
        }

        public void BindSlider(Slider slider, UnityAction<float> onValueChanged)
        {
            if (slider == null || onValueChanged == null)
                return;

            slider.onValueChanged.AddListener(onValueChanged);
            Add(() =>
            {
                if (slider != null)
                    slider.onValueChanged.RemoveListener(onValueChanged);
            });
        }

        public void BindInputField(InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.AddListener(onValueChanged);
            Add(() =>
            {
                if (inputField != null)
                    inputField.onValueChanged.RemoveListener(onValueChanged);
            });
        }

        public void BindInputFieldEndEdit(InputField inputField, UnityAction<string> onEndEdit)
        {
            if (inputField == null || onEndEdit == null)
                return;

            inputField.onEndEdit.AddListener(onEndEdit);
            Add(() =>
            {
                if (inputField != null)
                    inputField.onEndEdit.RemoveListener(onEndEdit);
            });
        }

        public void BindTMPInputField(TMP_InputField inputField, UnityAction<string> onValueChanged)
        {
            if (inputField == null || onValueChanged == null)
                return;

            inputField.onValueChanged.AddListener(onValueChanged);
            Add(() =>
            {
                if (inputField != null)
                    inputField.onValueChanged.RemoveListener(onValueChanged);
            });
        }

        public void BindTMPInputFieldEndEdit(TMP_InputField inputField, UnityAction<string> onEndEdit)
        {
            if (inputField == null || onEndEdit == null)
                return;

            inputField.onEndEdit.AddListener(onEndEdit);
            Add(() =>
            {
                if (inputField != null)
                    inputField.onEndEdit.RemoveListener(onEndEdit);
            });
        }

        public void BindScrollRect(ScrollRect scrollRect, UnityAction<Vector2> onValueChanged)
        {
            if (scrollRect == null || onValueChanged == null)
                return;

            scrollRect.onValueChanged.AddListener(onValueChanged);
            Add(() =>
            {
                if (scrollRect != null)
                    scrollRect.onValueChanged.RemoveListener(onValueChanged);
            });
        }
    }
}
