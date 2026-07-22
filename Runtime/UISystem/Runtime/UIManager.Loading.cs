using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.UISystem.Runtime
{
    public sealed partial class UIManager
    {
        private sealed class LoadingOperation
        {
            public string ViewId;
            public string Message;
            public float Progress;
            public bool IsIndeterminate;
            public bool BlockInput;
            public Action CancelRequested;
        }

        private readonly Dictionary<int, LoadingOperation> loadingOperations = new();
        private readonly List<int> loadingOrder = new();
        private readonly Dictionary<string, UILoadingView> loadingViews = new(StringComparer.Ordinal);
        private int nextLoadingOperationId;

        public int ActiveLoadingCount => loadingOperations.Count;

        public LoadingHandle ShowLoading(
            string message = null,
            bool isIndeterminate = true,
            bool blockInput = true,
            Action cancelRequested = null,
            string viewId = UIBuiltInViewIds.Loading) =>
            ShowLoading(
                new LoadingRequest(message, 0f, isIndeterminate, blockInput, cancelRequested),
                viewId);

        public LoadingHandle ShowLoading(
            in LoadingRequest request,
            string viewId = UIBuiltInViewIds.Loading)
        {
            if (string.IsNullOrWhiteSpace(viewId))
            {
                Debug.LogError("Loading view ID cannot be empty.");
                return new LoadingHandle(null, 0);
            }

            int id = AllocateLoadingOperationId();
            loadingOperations.Add(id, new LoadingOperation
            {
                ViewId = viewId,
                Message = request.Message,
                Progress = request.Progress,
                IsIndeterminate = request.IsIndeterminate,
                BlockInput = request.BlockInput,
                CancelRequested = request.CancelRequested
            });
            loadingOrder.Add(id);

            if (!RefreshLoadingView(viewId, out string errorMessage))
            {
                loadingOperations.Remove(id);
                loadingOrder.Remove(id);
                Debug.LogError(errorMessage);
                return new LoadingHandle(null, 0);
            }

            return new LoadingHandle(this, id);
        }

        internal bool IsLoadingOperationActive(int id) => loadingOperations.ContainsKey(id);

        internal bool SetLoadingMessage(int id, string message)
        {
            if (!loadingOperations.TryGetValue(id, out LoadingOperation operation))
                return false;

            operation.Message = message ?? string.Empty;
            RefreshLoadingView(operation.ViewId, out _);
            return true;
        }

        internal bool SetLoadingProgress(int id, float progress)
        {
            if (!loadingOperations.TryGetValue(id, out LoadingOperation operation))
                return false;

            operation.Progress = Mathf.Clamp01(progress);
            operation.IsIndeterminate = false;
            RefreshLoadingView(operation.ViewId, out _);
            return true;
        }

        internal bool SetLoadingIndeterminate(int id, bool isIndeterminate)
        {
            if (!loadingOperations.TryGetValue(id, out LoadingOperation operation))
                return false;

            operation.IsIndeterminate = isIndeterminate;
            RefreshLoadingView(operation.ViewId, out _);
            return true;
        }

        internal bool CancelLoadingOperation(int id)
        {
            if (!loadingOperations.TryGetValue(id, out LoadingOperation operation))
                return false;

            try
            {
                operation.CancelRequested?.Invoke();
            }
            finally
            {
                CloseLoadingOperation(id);
            }

            return true;
        }

        internal bool CloseLoadingOperation(int id)
        {
            if (!loadingOperations.Remove(id, out LoadingOperation operation))
                return false;

            loadingOrder.Remove(id);
            if (TryGetLatestLoadingOperation(operation.ViewId, out int _, out LoadingOperation _))
            {
                RefreshLoadingView(operation.ViewId, out _);
                return true;
            }

            if (loadingViews.TryGetValue(operation.ViewId, out UILoadingView view) && view != null)
                Close(view);
            else
                ClosePopup(operation.ViewId);

            return true;
        }

        private bool RefreshLoadingView(string viewId, out string errorMessage)
        {
            errorMessage = null;
            if (!TryGetLatestLoadingOperation(viewId, out int id, out LoadingOperation operation))
                return false;

            var data = new LoadingViewData(
                operation.Message,
                operation.Progress,
                operation.IsIndeterminate,
                operation.BlockInput,
                operation.CancelRequested != null ? () => CancelLoadingOperation(id) : null);

            if (loadingViews.TryGetValue(viewId, out UILoadingView current)
                && current != null
                && current.IsVisible)
            {
                current.Apply(data);
                return true;
            }

            UIViewResult<UILoadingView> result = OpenPopup<UILoadingView>(
                viewId,
                new UIViewOpenOptions(data, layerId: UILayers.System));
            if (!result.IsSuccess)
            {
                errorMessage = result.ErrorMessage;
                return false;
            }

            loadingViews[viewId] = result.Handle.View;
            return true;
        }

        private bool TryGetLatestLoadingOperation(
            string viewId,
            out int id,
            out LoadingOperation operation)
        {
            for (int i = loadingOrder.Count - 1; i >= 0; i--)
            {
                int candidateId = loadingOrder[i];
                if (!loadingOperations.TryGetValue(candidateId, out LoadingOperation candidate)
                    || !string.Equals(candidate.ViewId, viewId, StringComparison.Ordinal))
                {
                    continue;
                }

                id = candidateId;
                operation = candidate;
                return true;
            }

            id = 0;
            operation = null;
            return false;
        }

        private int AllocateLoadingOperationId()
        {
            do
            {
                nextLoadingOperationId++;
                if (nextLoadingOperationId <= 0)
                    nextLoadingOperationId = 1;
            } while (loadingOperations.ContainsKey(nextLoadingOperationId));

            return nextLoadingOperationId;
        }

        private void ClearLoadingOperations(bool closeViews)
        {
            loadingOperations.Clear();
            loadingOrder.Clear();

            if (closeViews)
            {
                foreach (UILoadingView view in loadingViews.Values)
                {
                    if (view != null && view.IsVisible)
                        Close(view, immediate: true);
                }
            }

            loadingViews.Clear();
        }
    }
}
