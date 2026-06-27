using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    /// <summary>Input System Cancel(UI/Back) 입력을 UIManager에 전달합니다.</summary>
    public sealed class UIBackInputListener : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference cancelAction;

        [SerializeField]
        private bool enabledInUpdateFallback = true;

        private InputAction boundAction;

        private void OnEnable()
        {
            if (cancelAction != null)
            {
                boundAction = cancelAction.action;
                boundAction.performed += OnCancelPerformed;
                boundAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (boundAction != null)
            {
                boundAction.performed -= OnCancelPerformed;
                boundAction.Disable();
                boundAction = null;
            }
        }

        private void Update()
        {
            if (!enabledInUpdateFallback || boundAction != null)
                return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                UIManager.Instance.TryHandleBack();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            UIManager.Instance.TryHandleBack();
        }
    }
}
