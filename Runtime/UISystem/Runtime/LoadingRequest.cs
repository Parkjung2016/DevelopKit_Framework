using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public readonly struct LoadingRequest
    {
        public string Message { get; }
        public float Progress { get; }
        public bool IsIndeterminate { get; }
        public bool BlockInput { get; }
        public Action CancelRequested { get; }

        public bool CanCancel => CancelRequested != null;

        public LoadingRequest(
            string message,
            float progress = 0f,
            bool isIndeterminate = true,
            bool blockInput = true,
            Action cancelRequested = null)
        {
            Message = message ?? string.Empty;
            Progress = Mathf.Clamp01(progress);
            IsIndeterminate = isIndeterminate;
            BlockInput = blockInput;
            CancelRequested = cancelRequested;
        }
    }

    public readonly struct LoadingViewData
    {
        public string Message { get; }
        public float Progress { get; }
        public bool IsIndeterminate { get; }
        public bool BlockInput { get; }
        public Action Cancel { get; }

        public bool CanCancel => Cancel != null;

        internal LoadingViewData(
            string message,
            float progress,
            bool isIndeterminate,
            bool blockInput,
            Action cancel)
        {
            Message = message ?? string.Empty;
            Progress = Mathf.Clamp01(progress);
            IsIndeterminate = isIndeterminate;
            BlockInput = blockInput;
            Cancel = cancel;
        }
    }
}
