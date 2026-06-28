using PJDev.UI;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public abstract partial class UIViewBase
    {
        private readonly UIEventSubscriptions eventSubscriptions = new();

        /// <summary>수동 구독·해제에 사용하는 이벤트 목록입니다.</summary>
        protected UIEventSubscriptions Events => eventSubscriptions;

        protected virtual void OnEnable()
        {
            eventSubscriptions.Clear();
            BindEvent();
        }

        protected virtual void OnDisable() => UnBindEvent();

        /// <summary>버튼·토글 등 UI 이벤트를 구독합니다. <see cref="UnBindEvent"/>에서 해제됩니다.</summary>
        protected virtual void BindEvent()
        {
        }

        /// <summary>구독한 UI 이벤트를 모두 해제합니다.</summary>
        protected void UnBindEvent() => eventSubscriptions.Clear();

        protected void BindEvent(System.Action bind, System.Action unbind) =>
            eventSubscriptions.Bind(bind, unbind);

        protected void BindEvent(UnityEvent unityEvent, UnityAction callback) =>
            eventSubscriptions.Bind(unityEvent, callback);

        protected void BindEvent<T>(UnityEvent<T> unityEvent, UnityAction<T> callback) =>
            eventSubscriptions.Bind(unityEvent, callback);

        protected void BindEvent<THandler>(THandler handler, Action<THandler> bind, Action<THandler> unbind) =>
            eventSubscriptions.Bind(handler, bind, unbind);

        protected void BindButton(Button button, UnityAction onClick) =>
            eventSubscriptions.BindButton(button, onClick);

        protected void BindToggle(Toggle toggle, UnityAction<bool> onValueChanged) =>
            eventSubscriptions.BindToggle(toggle, onValueChanged);

        protected void BindSlider(Slider slider, UnityAction<float> onValueChanged) =>
            eventSubscriptions.BindSlider(slider, onValueChanged);

        protected void BindInputField(InputField inputField, UnityAction<string> onValueChanged) =>
            eventSubscriptions.BindInputField(inputField, onValueChanged);

        protected void BindInputFieldEndEdit(InputField inputField, UnityAction<string> onEndEdit) =>
            eventSubscriptions.BindInputFieldEndEdit(inputField, onEndEdit);

        protected void BindScrollRect(ScrollRect scrollRect, UnityAction<Vector2> onValueChanged) =>
            eventSubscriptions.BindScrollRect(scrollRect, onValueChanged);
    }
}
