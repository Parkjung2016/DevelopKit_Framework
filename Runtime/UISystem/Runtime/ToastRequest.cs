using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public enum ToastDisplayMode
    {
        Queue,
        Stack,
        Replace
    }

    public readonly struct ToastRequest
    {
        public string Message { get; }
        public ToastType Type { get; }
        public float Duration { get; }
        public Sprite Icon { get; }
        public string DuplicateKey { get; }

        public ToastRequest(
            string message,
            ToastType type = ToastType.Info,
            float duration = 0f,
            Sprite icon = null,
            string duplicateKey = null)
        {
            Message = message ?? string.Empty;
            Type = type;
            Duration = Mathf.Max(0f, duration);
            Icon = icon;
            DuplicateKey = duplicateKey;
        }

        internal string GetDuplicateKey() =>
            string.IsNullOrEmpty(DuplicateKey) ? $"{(int)Type}:{Message}" : DuplicateKey;
    }
}
