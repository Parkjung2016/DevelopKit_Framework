using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PJDev.UI
{
    /// <summary>
    /// UIView가 아닌 일반 UI MonoBehaviour용 이벤트 바인딩 베이스입니다.
    /// 화면 뷰는 <see cref="PJDev.DevelopKit.Framework.UISystem.Runtime.UIViewBase"/>를 사용하세요.
    /// </summary>
    [Obsolete("화면 UI는 UIViewBase.BindEvent()를 사용하세요. UIView 외부 컴포넌트에서만 사용합니다.")]
    public abstract class UIBindBehaviour : MonoBehaviour
    {
        private readonly UIEventSubscriptions eventSubscriptions = new();

        protected UIEventSubscriptions Events => eventSubscriptions;

        protected virtual void OnEnable()
        {
            eventSubscriptions.Clear();
            BindEvent();
        }

        protected virtual void OnDisable() => UnBindEvent();

        protected abstract void BindEvent();

        protected void UnBindEvent() => eventSubscriptions.Clear();

        protected void BindEvent(Action bind, Action unbind) =>
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

        protected void BindTMPInputField(TMP_InputField inputField, UnityAction<string> onValueChanged) =>
            eventSubscriptions.BindTMPInputField(inputField, onValueChanged);

        protected void BindTMPInputFieldEndEdit(TMP_InputField inputField, UnityAction<string> onEndEdit) =>
            eventSubscriptions.BindTMPInputFieldEndEdit(inputField, onEndEdit);

        protected void BindScrollRect(ScrollRect scrollRect, UnityAction<Vector2> onValueChanged) =>
            eventSubscriptions.BindScrollRect(scrollRect, onValueChanged);
    }
}
