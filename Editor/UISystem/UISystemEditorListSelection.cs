using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.UISystem
{
    internal sealed class UISystemEditorListSelectionController
    {
        private readonly VisualElement listHost;
        private readonly HashSet<int> selected = new();
        private readonly Action onSelectionChanged;
        private readonly Action onDeleteRequested;
        private readonly VisualElement marqueeOverlay;
        private int anchor = -1;
        private int primary = -1;
        private int activePointerId = -1;
        private int capturedPointerId = -1;
        private Vector2 pointerDownLocal;
        private bool dragMarqueeActive;

        public UISystemEditorListSelectionController(
            VisualElement listHost,
            Action onSelectionChanged,
            Action onDeleteRequested = null)
        {
            this.listHost = listHost;
            this.onSelectionChanged = onSelectionChanged;
            this.onDeleteRequested = onDeleteRequested;

            listHost.focusable = true;
            listHost.RegisterCallback<PointerDownEvent>(OnPointerDown);
            listHost.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            listHost.RegisterCallback<PointerUpEvent>(OnPointerUp);
            listHost.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            listHost.RegisterCallback<KeyDownEvent>(OnKeyDown);

            marqueeOverlay = new VisualElement();
            marqueeOverlay.style.position = Position.Absolute;
            marqueeOverlay.style.backgroundColor = new Color(0.2f, 0.45f, 0.7f, 0.2f);
            marqueeOverlay.style.borderTopWidth = 1;
            marqueeOverlay.style.borderBottomWidth = 1;
            marqueeOverlay.style.borderLeftWidth = 1;
            marqueeOverlay.style.borderRightWidth = 1;
            marqueeOverlay.style.borderTopColor = new Color(0.2f, 0.45f, 0.7f, 0.8f);
            marqueeOverlay.style.borderBottomColor = marqueeOverlay.style.borderTopColor.value;
            marqueeOverlay.style.borderLeftColor = marqueeOverlay.style.borderTopColor.value;
            marqueeOverlay.style.borderRightColor = marqueeOverlay.style.borderTopColor.value;
            marqueeOverlay.style.display = DisplayStyle.None;
            marqueeOverlay.pickingMode = PickingMode.Ignore;
            listHost.Add(marqueeOverlay);
        }

        public int Primary => primary;

        public int Count => selected.Count;

        public bool IsSelected(int index) => selected.Contains(index);

        public IReadOnlyCollection<int> GetSelectedSnapshot() => new List<int>(selected);

        public void ClearSelection()
        {
            if (selected.Count == 0 && primary < 0)
                return;

            selected.Clear();
            anchor = -1;
            primary = -1;
            RefreshAllRowStyles();
            NotifySelectionChanged();
        }

        public void SetSelection(IEnumerable<int> indices, int primaryIndex)
        {
            selected.Clear();
            foreach (int index in indices)
            {
                if (index >= 0)
                    selected.Add(index);
            }

            primary = primaryIndex;
            if (primary < 0 && selected.Count > 0)
            {
                foreach (int index in selected)
                {
                    primary = index;
                    break;
                }
            }

            anchor = primary;
            RefreshAllRowStyles();
            NotifySelectionChanged();
        }

        public void SelectSingle(int index)
        {
            selected.Clear();
            selected.Add(index);
            anchor = index;
            primary = index;
            RefreshAllRowStyles();
            NotifySelectionChanged();
        }

        public void SelectAll()
        {
            List<int> ordered = CollectOrderedRowIndices();
            if (ordered.Count == 0)
                return;

            SetSelection(ordered, ordered[^1]);
        }

        private void ApplyClickSelection(int index, bool shiftKey, bool toggleKey)
        {
            listHost.Focus();

            if (shiftKey && anchor >= 0)
                SelectRange(anchor, index);
            else if (toggleKey)
                Toggle(index);
            else
                SelectSingle(index);
        }

        public void ClearListRows()
        {
            for (int i = listHost.childCount - 1; i >= 0; i--)
            {
                if (listHost[i] == marqueeOverlay)
                    continue;

                listHost.RemoveAt(i);
            }
        }

        public void PruneInvalidIndices(int maxExclusive)
        {
            selected.RemoveWhere(index => index < 0 || index >= maxExclusive);
            if (primary >= maxExclusive)
                primary = -1;

            if (primary < 0)
            {
                foreach (int index in selected)
                {
                    primary = index;
                    break;
                }
            }

            if (anchor >= maxExclusive)
                anchor = primary;
        }

        public void RefreshAllRowStyles()
        {
            ApplyRowStylesRecursive(listHost, selected);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            listHost.Focus();
            activePointerId = evt.pointerId;
            pointerDownLocal = listHost.WorldToLocal(evt.position);
            dragMarqueeActive = false;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            Vector2 current = listHost.WorldToLocal(evt.position);
            if (!dragMarqueeActive)
            {
                if (Vector2.Distance(current, pointerDownLocal) < 4f)
                    return;

                dragMarqueeActive = true;
                capturedPointerId = evt.pointerId;
                listHost.CapturePointer(evt.pointerId);
            }

            UpdateMarqueeVisual(pointerDownLocal, current);
            ApplyMarqueeSelection(
                pointerDownLocal,
                current,
                append: evt.ctrlKey || evt.commandKey || evt.shiftKey,
                notify: false);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            bool wasDrag = dragMarqueeActive;
            EndPointerInteraction(evt.pointerId, notifyMarquee: wasDrag);

            if (wasDrag)
                return;

            if (!TryGetRowIndexAt(evt.position, out int index))
                return;

            ApplyClickSelection(index, evt.shiftKey, evt.ctrlKey || evt.commandKey);
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            EndPointerInteraction(evt.pointerId, notifyMarquee: false);
        }

        private void EndPointerInteraction(int pointerId, bool notifyMarquee)
        {
            activePointerId = -1;
            dragMarqueeActive = false;
            marqueeOverlay.style.display = DisplayStyle.None;

            if (capturedPointerId >= 0 && listHost.HasPointerCapture(capturedPointerId))
                listHost.ReleasePointer(capturedPointerId);
            capturedPointerId = -1;

            RefreshAllRowStyles();
            if (notifyMarquee)
                NotifySelectionChanged();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if ((evt.keyCode == KeyCode.A) && (evt.ctrlKey || evt.commandKey))
            {
                if (evt.target is TextField)
                    return;

                evt.StopPropagation();
                SelectAll();
                return;
            }

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            if (selected.Count == 0)
                return;

            evt.StopPropagation();
            onDeleteRequested?.Invoke();
        }

        private void Toggle(int index)
        {
            if (!selected.Remove(index))
            {
                selected.Add(index);
                primary = index;
            }
            else if (primary == index)
            {
                primary = -1;
                foreach (int candidate in selected)
                {
                    primary = candidate;
                    break;
                }
            }

            anchor = index;
            RefreshAllRowStyles();
            NotifySelectionChanged();
        }

        private void SelectRange(int fromIndex, int toIndex)
        {
            List<int> ordered = CollectOrderedRowIndices();
            int fromPos = ordered.IndexOf(fromIndex);
            int toPos = ordered.IndexOf(toIndex);
            if (fromPos < 0 || toPos < 0)
            {
                SelectSingle(toIndex);
                return;
            }

            if (fromPos > toPos)
                (fromPos, toPos) = (toPos, fromPos);

            selected.Clear();
            for (int i = fromPos; i <= toPos; i++)
                selected.Add(ordered[i]);

            primary = toIndex;
            anchor = fromIndex;
            RefreshAllRowStyles();
            NotifySelectionChanged();
        }

        private void ApplyMarqueeSelection(Vector2 start, Vector2 end, bool append, bool notify)
        {
            Rect rect = RectFromPoints(start, end);
            List<int> hit = new();
            CollectRowsIntersecting(listHost, rect, hit);

            if (!append)
                selected.Clear();

            for (int i = 0; i < hit.Count; i++)
                selected.Add(hit[i]);

            if (hit.Count > 0)
            {
                primary = hit[^1];
                anchor = hit[0];
            }
            else if (!append)
            {
                primary = -1;
                anchor = -1;
            }

            RefreshAllRowStyles();
            if (notify)
                NotifySelectionChanged();
        }

        private bool TryGetRowIndexAt(Vector2 worldPosition, out int index)
        {
            index = -1;
            if (listHost.panel == null)
                return false;

            VisualElement picked = listHost.panel.Pick(worldPosition);
            if (picked == null || picked == marqueeOverlay || IsIgnoredClickTarget(picked))
                return false;

            for (VisualElement current = picked; current != null; current = current.parent)
            {
                if (current == listHost)
                    break;

                if (current.userData is int rowIndex)
                {
                    index = rowIndex;
                    return true;
                }
            }

            return false;
        }

        private static bool IsIgnoredClickTarget(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is Button || current is UnityEngine.UIElements.Toggle || current is TextField)
                    return true;
            }

            return false;
        }

        private void NotifySelectionChanged()
        {
            onSelectionChanged?.Invoke();
        }

        private void UpdateMarqueeVisual(Vector2 start, Vector2 end)
        {
            Rect rect = RectFromPoints(start, end);
            marqueeOverlay.style.display = DisplayStyle.Flex;
            marqueeOverlay.style.left = rect.xMin;
            marqueeOverlay.style.top = rect.yMin;
            marqueeOverlay.style.width = rect.width;
            marqueeOverlay.style.height = rect.height;
        }

        private static Rect RectFromPoints(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float xMax = Mathf.Max(a.x, b.x);
            float yMax = Mathf.Max(a.y, b.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void ApplyRowStylesRecursive(VisualElement parent, HashSet<int> selectedIndices)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child.userData is int index)
                    UISystemEditorListSelectionStyles.Apply(child, selectedIndices.Contains(index));

                ApplyRowStylesRecursive(child, selectedIndices);
            }
        }

        private void CollectRowsIntersecting(VisualElement parent, Rect localRect, List<int> hit)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child == marqueeOverlay)
                    continue;

                if (child.userData is int index && child.resolvedStyle.display != DisplayStyle.None)
                {
                    Rect bounds = child.worldBound;
                    Vector2 localMin = listHost.WorldToLocal(bounds.min);
                    Vector2 localMax = listHost.WorldToLocal(bounds.max);
                    Rect rowRect = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
                    if (localRect.Overlaps(rowRect))
                        hit.Add(index);
                }

                CollectRowsIntersecting(child, localRect, hit);
            }
        }

        public static List<int> CollectOrderedRowIndices(VisualElement listHost)
        {
            var ordered = new List<int>();
            CollectOrderedRowIndicesRecursive(listHost, ordered);
            return ordered;
        }

        private List<int> CollectOrderedRowIndices() => CollectOrderedRowIndices(listHost);

        private static void CollectOrderedRowIndicesRecursive(VisualElement parent, List<int> ordered)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                VisualElement child = parent[i];
                if (child.userData is int index)
                    ordered.Add(index);
                else
                    CollectOrderedRowIndicesRecursive(child, ordered);
            }
        }
    }

    internal static class UISystemEditorListSelectionStyles
    {
        public const string SelectedClass = "ui-system-list-row-selected";

        public static void PrepareRow(VisualElement row, bool selected)
        {
            row.AddToClassList("ui-system-list-row");
            row.pickingMode = PickingMode.Position;
            Apply(row, selected);
        }

        public static void Apply(VisualElement row, bool selected)
        {
            if (selected)
                row.AddToClassList(SelectedClass);
            else
                row.RemoveFromClassList(SelectedClass);

            row.style.backgroundColor = selected
                ? new Color(0.2f, 0.45f, 0.7f, 0.35f)
                : new Color(0f, 0f, 0f, 0.06f);
        }
    }

    internal static class UISystemEditorListSelectionDelete
    {
        public static void DeleteDescending(UnityEditor.SerializedProperty array, IEnumerable<int> indices, UnityEngine.Object undoTarget, string undoLabel)
        {
            var sorted = new List<int>();
            foreach (int index in indices)
            {
                if (index >= 0 && index < array.arraySize)
                    sorted.Add(index);
            }

            if (sorted.Count == 0)
                return;

            sorted.Sort((a, b) => b.CompareTo(a));
            UnityEditor.Undo.RecordObject(undoTarget, undoLabel);
            for (int i = 0; i < sorted.Count; i++)
                array.DeleteArrayElementAtIndex(sorted[i]);

            array.serializedObject.ApplyModifiedProperties();
            UnityEditor.EditorUtility.SetDirty(undoTarget);
        }
    }
}
