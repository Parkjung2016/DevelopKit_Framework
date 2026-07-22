using System;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public sealed class LoadingHandle : IDisposable
    {
        private UIManager owner;
        private readonly int operationId;

        internal LoadingHandle(UIManager owner, int operationId)
        {
            this.owner = owner;
            this.operationId = operationId;
        }

        public bool IsValid => owner != null && owner.IsLoadingOperationActive(operationId);

        public bool SetMessage(string message) =>
            owner != null && owner.SetLoadingMessage(operationId, message);

        public bool SetProgress(float progress) =>
            owner != null && owner.SetLoadingProgress(operationId, progress);

        public bool SetIndeterminate(bool isIndeterminate = true) =>
            owner != null && owner.SetLoadingIndeterminate(operationId, isIndeterminate);

        public bool Cancel()
        {
            if (owner == null)
                return false;

            UIManager currentOwner = owner;
            owner = null;
            bool cancelled = currentOwner.CancelLoadingOperation(operationId);
            return cancelled;
        }

        public bool Close()
        {
            if (owner == null)
                return false;

            bool closed = owner.CloseLoadingOperation(operationId);
            if (closed)
                owner = null;
            return closed;
        }

        public void Dispose() => Close();
    }
}
