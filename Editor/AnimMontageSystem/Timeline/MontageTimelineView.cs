using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageTimelineView : VisualElement
    {
        private const float RulerHeight = 22f;
        private const float TrackHeight = 32f;
        private const float TrackGap = 6f;
        private const float TrackLabelWidth = 56f;
        private const float ContentPadding = 8f;
        private const float SnapStep = 0.05f;
        private const float EdgeHandleWidth = 6f;
        private const float MinSegmentDuration = 0.05f;

        private static readonly Color SegmentCoreColor = new(0.28f, 0.52f, 0.92f, 0.92f);
        private static readonly Color SegmentSelectedColor = new(0.38f, 0.64f, 1f, 0.98f);
        private static readonly Color BlendOverlayColor = new(0.08f, 0.12f, 0.22f, 0.55f);
        private static readonly Color TransitionColor = new(0.62f, 0.38f, 0.95f, 0.42f);
        private static readonly Color TrackRowColor = new(0.1f, 0.1f, 0.11f, 1f);

        private enum DragMode
        {
            None,
            Playhead,
            SegmentMove,
            SegmentBlendIn,
            SegmentBlendOut,
            TransitionCrossfade,
            NotifyMove,
            NotifyStateMove,
            NotifyStateResizeStart,
            NotifyStateResizeEnd
        }

        private readonly struct TransitionLayout
        {
            public TransitionLayout(int lowerSegmentIndex, Rect rect, float duration)
            {
                LowerSegmentIndex = lowerSegmentIndex;
                Rect = rect;
                Duration = duration;
            }

            public int LowerSegmentIndex { get; }
            public Rect Rect { get; }
            public float Duration { get; }
        }

        private readonly struct SegmentLayout
        {
            public SegmentLayout(int index, Rect body, Rect blendIn, Rect blendOut)
            {
                Index = index;
                Body = body;
                BlendIn = blendIn;
                BlendOut = blendOut;
            }

            public int Index { get; }
            public Rect Body { get; }
            public Rect BlendIn { get; }
            public Rect BlendOut { get; }
        }

        private readonly struct NotifyStateLayout
        {
            public NotifyStateLayout(int index, Rect body)
            {
                Index = index;
                Body = body;
            }

            public int Index { get; }
            public Rect Body { get; }
        }

        private readonly MontageEditorContext context;
        private readonly List<SegmentLayout> segmentLayouts = new();
        private readonly List<TransitionLayout> transitionLayouts = new();
        private readonly List<NotifyStateLayout> notifyStateLayouts = new();

        private float pixelsPerSecond = 120f;
        private float segmentTrackTop;
        private float notifyTrackTop;
        private float notifyStateTrackTop;

        private DragMode dragMode = DragMode.None;
        private int dragSegmentIndex = -1;
        private int dragNotifyIndex = -1;
        private int dragNotifyStateIndex = -1;
        private float dragAnchorTime;
        private float dragAnchorValue;
        private float dragNotifyStateDuration;

        public MontageTimelineView(MontageEditorContext context)
        {
            this.context = context;
            AddToClassList(AnimMontageEditorStyles.TimelineHostClass);
            style.flexGrow = 1;
            style.flexShrink = 1;
            style.flexBasis = 0;
            style.minHeight = 160;
            style.overflow = Overflow.Hidden;
            focusable = true;

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<WheelEvent>(OnWheel);

            context.Changed += RequestRepaint;
            context.PlayheadChanged += RequestRepaint;
        }

        private void RequestRepaint() => MarkDirtyRepaint();

        private void OnWheel(WheelEvent evt)
        {
            pixelsPerSecond = Mathf.Clamp(pixelsPerSecond - evt.delta.y * 4f, 40f, 400f);
            evt.StopPropagation();
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (context.Montage == null)
                return;

            focusController?.IgnoreEvent(evt);
            Vector2 local = evt.localPosition;

            if (TryHitPlayhead(local, out _))
            {
                BeginDrag(DragMode.Playhead, evt.pointerId);
                SetPlayheadFromX(local.x);
                evt.StopPropagation();
                return;
            }

            if (TryHitTransition(local, out dragSegmentIndex, out float transitionDuration))
            {
                BeginDrag(DragMode.TransitionCrossfade, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = transitionDuration;
                context.SetSelectedSegment(dragSegmentIndex);
                evt.StopPropagation();
                return;
            }

            if (TryHitSegment(local, out dragSegmentIndex, out DragMode segmentDrag))
            {
                BeginDrag(segmentDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                dragAnchorValue = GetSegmentDragValue(segmentDrag, dragSegmentIndex);
                context.SetSelectedSegment(dragSegmentIndex);
                evt.StopPropagation();
                return;
            }

            if (TryHitNotifyState(local, out dragNotifyStateIndex, out DragMode stateDrag))
            {
                BeginDrag(stateDrag, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                AnimNotifyStatePlacement placement = context.Montage.NotifyStates[dragNotifyStateIndex];
                dragNotifyStateDuration = placement.Duration;
                dragAnchorValue = stateDrag == DragMode.NotifyStateResizeEnd
                    ? placement.EndTime
                    : placement.StartTime;
                evt.StopPropagation();
                return;
            }

            if (TryHitNotify(local, out dragNotifyIndex))
            {
                BeginDrag(DragMode.NotifyMove, evt.pointerId);
                dragAnchorTime = XToTime(local.x);
                context.SetSelectedNotify(dragNotifyIndex);
                evt.StopPropagation();
                return;
            }

            if (evt.clickCount == 2 && local.y > RulerHeight)
            {
                AddNotifyAtTime(XToTime(local.x));
                evt.StopPropagation();
                return;
            }

            if (local.y <= RulerHeight)
            {
                BeginDrag(DragMode.Playhead, evt.pointerId);
                SetPlayheadFromX(local.x);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            float time = Snap(XToTime(evt.localPosition.x));
            switch (dragMode)
            {
                case DragMode.Playhead:
                    context.SetPlayhead(time);
                    break;

                case DragMode.NotifyMove when dragNotifyIndex >= 0:
                    ApplyNotifyTime(dragNotifyIndex, time);
                    break;

                case DragMode.SegmentMove when dragSegmentIndex >= 0:
                    ApplySegmentStartTime(dragSegmentIndex, time - dragAnchorTime + dragAnchorValue);
                    break;

                case DragMode.SegmentBlendIn when dragSegmentIndex >= 0:
                    ApplySegmentBlendIn(dragSegmentIndex, time);
                    break;

                case DragMode.SegmentBlendOut when dragSegmentIndex >= 0:
                    ApplySegmentBlendOut(dragSegmentIndex, time);
                    break;

                case DragMode.TransitionCrossfade when dragSegmentIndex >= 0:
                    ApplyTransitionCrossfade(dragSegmentIndex, dragAnchorValue + (time - dragAnchorTime));
                    break;

                case DragMode.NotifyStateMove when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        time - dragAnchorTime + dragAnchorValue,
                        time - dragAnchorTime + dragAnchorValue + dragNotifyStateDuration);
                    break;

                case DragMode.NotifyStateResizeStart when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        time,
                        context.Montage.NotifyStates[dragNotifyStateIndex].EndTime);
                    break;

                case DragMode.NotifyStateResizeEnd when dragNotifyStateIndex >= 0:
                    ApplyNotifyStateRange(
                        dragNotifyStateIndex,
                        context.Montage.NotifyStates[dragNotifyStateIndex].StartTime,
                        time);
                    break;
            }

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            this.ReleasePointer(evt.pointerId);
            dragMode = DragMode.None;
            dragSegmentIndex = -1;
            dragNotifyIndex = -1;
            dragNotifyStateIndex = -1;
            evt.StopPropagation();
        }

        private void BeginDrag(DragMode mode, int pointerId)
        {
            dragMode = mode;
            this.CapturePointer(pointerId);
        }

        private float GetSegmentDragValue(DragMode mode, int segmentIndex)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            return mode switch
            {
                DragMode.SegmentMove => segment.StartTime,
                DragMode.SegmentBlendIn => segment.BlendIn,
                DragMode.SegmentBlendOut => segment.EndTime - segment.BlendOut,
                _ => 0f
            };
        }

        private void ApplySegmentStartTime(int segmentIndex, float startTime)
        {
            Undo.RecordObject(context.Montage, "Move Montage Segment");
            SerializedObject so = new(context.Montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("startTime").floatValue = Snap(Mathf.Max(0f, startTime));
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplySegmentBlendIn(int segmentIndex, float time)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            float blendIn = Snap(Mathf.Clamp(time - segment.StartTime, 0f, MaxBlendDuration(segment)));
            WriteSegmentBlend(segmentIndex, blendIn, segment.BlendOut);
        }

        private void ApplySegmentBlendOut(int segmentIndex, float time)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            float blendOut = Snap(Mathf.Clamp(segment.EndTime - time, 0f, MaxBlendDuration(segment)));
            WriteSegmentBlend(segmentIndex, segment.BlendIn, blendOut);
        }

        private void WriteSegmentBlend(int segmentIndex, float blendIn, float blendOut)
        {
            MontageSegment segment = context.Montage.Segments[segmentIndex];
            float maxBlend = MaxBlendDuration(segment);
            blendIn = Mathf.Clamp(blendIn, 0f, maxBlend);
            blendOut = Mathf.Clamp(blendOut, 0f, maxBlend - blendIn);

            Undo.RecordObject(context.Montage, "Adjust Segment Blend");
            SerializedObject so = new(context.Montage);
            SerializedProperty segmentProperty = so.FindProperty("segments").GetArrayElementAtIndex(segmentIndex);
            segmentProperty.FindPropertyRelative("blendIn").floatValue = blendIn;
            segmentProperty.FindPropertyRelative("blendOut").floatValue = blendOut;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private void ApplyTransitionCrossfade(int lowerSegmentIndex, float duration)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null || lowerSegmentIndex < 0 || lowerSegmentIndex + 1 >= montage.Segments.Count)
                return;

            MontageSegment from = montage.Segments[lowerSegmentIndex];
            MontageSegment to = montage.Segments[lowerSegmentIndex + 1];
            if (from == null || to == null)
                return;

            float maxDuration = Mathf.Min(MaxBlendDuration(from), MaxBlendDuration(to));
            duration = Snap(Mathf.Clamp(duration, 0f, maxDuration));

            Undo.RecordObject(montage, "Adjust Transition Crossfade");
            SerializedObject so = new(montage);
            SerializedProperty segments = so.FindProperty("segments");
            segments.GetArrayElementAtIndex(lowerSegmentIndex).FindPropertyRelative("blendOut").floatValue = duration;
            segments.GetArrayElementAtIndex(lowerSegmentIndex + 1).FindPropertyRelative("blendIn").floatValue = duration;
            so.ApplyModifiedProperties();
            context.MarkDirty();
        }

        private static float MaxBlendDuration(MontageSegment segment) =>
            Mathf.Max(MinSegmentDuration, segment.Duration * 0.5f);

        private void ApplyNotifyTime(int notifyIndex, float time)
        {
            Undo.RecordObject(context.Montage, "Move Anim Notify");
            context.Montage.Notifies[notifyIndex].Time = Snap(time);
            context.MarkDirty();
        }

        private void ApplyNotifyStateRange(int notifyStateIndex, float startTime, float endTime)
        {
            startTime = Snap(Mathf.Max(0f, startTime));
            endTime = Snap(Mathf.Max(startTime + MinSegmentDuration, endTime));
            float maxTime = context.Montage.Length;
            endTime = Mathf.Min(endTime, maxTime);

            Undo.RecordObject(context.Montage, "Adjust Notify State");
            AnimNotifyStatePlacement placement = context.Montage.NotifyStates[notifyStateIndex];
            placement.StartTime = startTime;
            placement.EndTime = endTime;
            context.MarkDirty();
        }

        private void AddNotifyAtTime(float time)
        {
            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            Undo.RecordObject(montage, "Add Anim Notify");
            var so = new SerializedObject(montage);
            SerializedProperty prop = so.FindProperty("notifies");
            int index = prop.arraySize;
            prop.InsertArrayElementAtIndex(index);
            SerializedProperty element = prop.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("time").floatValue = Snap(time);
            element.FindPropertyRelative("trackId").stringValue = "Default";
            so.ApplyModifiedPropertiesWithoutUndo();
            context.MarkDirty();
            context.SetPlayhead(time);
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            segmentLayouts.Clear();
            transitionLayouts.Clear();
            notifyStateLayouts.Clear();

            var painter = ctx.painter2D;
            DrawBackground(painter, rect);

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            DrawRuler(painter, rect, montage.Length);
            float y = RulerHeight + TrackGap;
            segmentTrackTop = y;
            y = DrawSegmentTrack(painter, rect, y, montage);
            notifyTrackTop = y;
            y = DrawNotifyTrack(painter, rect, y, montage, "Default");
            notifyStateTrackTop = y;
            DrawNotifyStateTrack(painter, rect, y, montage);
            DrawPlayhead(painter, rect);
        }

        private void DrawBackground(Painter2D painter, Rect rect)
        {
            painter.fillColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawRuler(Painter2D painter, Rect rect, float length)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.15f);
            painter.lineWidth = 1f;
            float y = rect.yMin + RulerHeight;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + TrackLabelWidth, y));
            painter.LineTo(new Vector2(rect.xMax, y));
            painter.Stroke();

            float maxTime = Mathf.Max(length, 1f);
            int step = pixelsPerSecond >= 100f ? 1 : 5;
            for (float t = 0f; t <= maxTime; t += step)
            {
                float x = TimeToX(t);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin + 4f));
                painter.LineTo(new Vector2(x, y));
                painter.Stroke();
            }

            var labelGutter = new Rect(rect.xMin, rect.yMin, TrackLabelWidth, RulerHeight);
            painter.fillColor = new Color(0.09f, 0.09f, 0.1f, 1f);
            FillRect(painter, labelGutter);
        }

        private float DrawSegmentTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.34f, 0.62f, 0.35f));

            for (int i = 0; i < montage.Segments.Count - 1; i++)
            {
                MontageSegment current = montage.Segments[i];
                MontageSegment next = montage.Segments[i + 1];
                if (current?.Clip == null || next?.Clip == null)
                    continue;

                float crossfade = MontageSegmentBlending.GetCrossfadeDuration(current, next);
                if (crossfade <= 0f)
                    continue;

                float x0 = TimeToX(current.EndTime - crossfade);
                float x1 = TimeToX(current.EndTime + crossfade);
                var transitionRect = new Rect(x0, y + 4f, Mathf.Max(6f, x1 - x0), TrackHeight - 8f);
                painter.fillColor = TransitionColor;
                FillRoundedRect(painter, transitionRect, 4f);
                DrawDiagonalHatch(painter, transitionRect, new Color(1f, 1f, 1f, 0.08f));
                transitionLayouts.Add(new TransitionLayout(i, transitionRect, crossfade));
            }

            for (int i = 0; i < montage.Segments.Count; i++)
            {
                MontageSegment segment = montage.Segments[i];
                if (segment?.Clip == null)
                    continue;

                float x0 = TimeToX(segment.StartTime);
                float x1 = TimeToX(segment.EndTime);
                var body = new Rect(x0, y + 2f, Mathf.Max(4f, x1 - x0), TrackHeight - 4f);
                bool selected = context.SelectedSegmentIndex == i;

                painter.fillColor = selected ? SegmentSelectedColor : SegmentCoreColor;
                FillRoundedRect(painter, body, 3f);

                float blendInWidth = segment.BlendIn > 0f
                    ? Mathf.Clamp(segment.BlendIn * pixelsPerSecond, 3f, body.width * 0.5f)
                    : 0f;
                float blendOutWidth = segment.BlendOut > 0f
                    ? Mathf.Clamp(segment.BlendOut * pixelsPerSecond, 3f, body.width * 0.5f)
                    : 0f;

                var blendInRect = new Rect(body.xMin, body.yMin, blendInWidth, body.height);
                var blendOutRect = new Rect(body.xMax - blendOutWidth, body.yMin, blendOutWidth, body.height);

                if (blendInWidth > 0f)
                {
                    painter.fillColor = BlendOverlayColor;
                    FillRect(painter, blendInRect);
                    DrawBlendHandle(painter, blendInRect.xMax, body.yMin, body.height);
                }

                if (blendOutWidth > 0f)
                {
                    painter.fillColor = BlendOverlayColor;
                    FillRect(painter, blendOutRect);
                    DrawBlendHandle(painter, blendOutRect.xMin, body.yMin, body.height);
                }

                if (body.width > 28f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.18f);
                    painter.lineWidth = 1f;
                    float midY = body.yMin + body.height * 0.5f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMin + 6f, midY));
                    painter.LineTo(new Vector2(body.xMax - 6f, midY));
                    painter.Stroke();
                }

                segmentLayouts.Add(new SegmentLayout(i, body, blendInRect, blendOutRect));
            }

            return y + TrackHeight + TrackGap;
        }

        private float DrawNotifyTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.62f, 0.72f, 0.22f));
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = montage.Notifies[i];
                if (placement == null || placement.TrackId != trackId)
                    continue;

                Color color = placement.Notify != null ? placement.Notify.EditorColor : new Color(0.4f, 0.8f, 1f);
                DrawDiamond(painter, TimeToX(placement.Time), y + TrackHeight * 0.5f, 7f, color);
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyStateTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage)
        {
            DrawTrackRow(painter, rect, y, new Color(0.28f, 0.72f, 0.42f, 0.18f));
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement placement = montage.NotifyStates[i];
                if (placement?.NotifyState == null)
                    continue;

                float x0 = TimeToX(placement.StartTime);
                float x1 = TimeToX(placement.EndTime);
                var bar = new Rect(x0, y, Mathf.Max(4f, x1 - x0), TrackHeight);
                bool selected = context.SelectedNotifyStateIndex == i;
                painter.fillColor = selected
                    ? placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.85f)
                    : placement.NotifyState.EditorColor * new Color(1f, 1f, 1f, 0.55f);
                FillRoundedRect(painter, bar, 3f);
                notifyStateLayouts.Add(new NotifyStateLayout(i, bar));
            }
        }

        private void DrawTrackRow(Painter2D painter, Rect rect, float y, Color accentColor)
        {
            var row = new Rect(rect.xMin + TrackLabelWidth, y, rect.width - TrackLabelWidth, TrackHeight);
            painter.fillColor = TrackRowColor;
            FillRect(painter, row);

            var labelRect = new Rect(rect.xMin + 2f, y + 1f, TrackLabelWidth - 4f, TrackHeight - 2f);
            painter.fillColor = accentColor;
            FillRoundedRect(painter, labelRect, 3f);
        }

        private static void DrawBlendHandle(Painter2D painter, float x, float y, float height)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.55f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y + 4f));
            painter.LineTo(new Vector2(x, y + height - 4f));
            painter.Stroke();
        }

        private static void DrawDiagonalHatch(Painter2D painter, Rect rect, Color color)
        {
            painter.strokeColor = color;
            painter.lineWidth = 1f;
            const float step = 6f;
            for (float offset = rect.xMin - rect.height; offset < rect.xMax; offset += step)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(offset, rect.yMax));
                painter.LineTo(new Vector2(offset + rect.height, rect.yMin));
                painter.Stroke();
            }
        }

        private static void FillRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            if (radius <= 0f)
            {
                FillRect(painter, rect);
                return;
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
            painter.Arc(new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
            painter.Arc(new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawPlayhead(Painter2D painter, Rect rect)
        {
            float x = TimeToX(context.PlayheadTime);
            painter.strokeColor = new Color(1f, 0.35f, 0.35f, 0.95f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, rect.yMin));
            painter.LineTo(new Vector2(x, rect.yMax));
            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawDiamond(Painter2D painter, float cx, float cy, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - radius));
            painter.LineTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx, cy + radius));
            painter.LineTo(new Vector2(cx - radius, cy));
            painter.ClosePath();
            painter.Fill();
        }

        private bool TryHitPlayhead(Vector2 local, out float distance)
        {
            distance = Mathf.Abs(local.x - TimeToX(context.PlayheadTime));
            return distance <= 6f;
        }

        private bool TryHitTransition(Vector2 local, out int lowerSegmentIndex, out float duration)
        {
            lowerSegmentIndex = -1;
            duration = 0f;

            for (int i = transitionLayouts.Count - 1; i >= 0; i--)
            {
                TransitionLayout layout = transitionLayouts[i];
                if (!layout.Rect.Contains(local))
                    continue;

                lowerSegmentIndex = layout.LowerSegmentIndex;
                duration = layout.Duration;
                return true;
            }

            return false;
        }

        private bool TryHitSegment(Vector2 local, out int index, out DragMode mode)
        {
            index = -1;
            mode = DragMode.None;

            for (int i = segmentLayouts.Count - 1; i >= 0; i--)
            {
                SegmentLayout layout = segmentLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                index = layout.Index;
                if (layout.BlendIn.width > 0f && layout.BlendIn.Contains(local))
                {
                    mode = DragMode.SegmentBlendIn;
                    return true;
                }

                if (layout.BlendOut.width > 0f && layout.BlendOut.Contains(local))
                {
                    mode = DragMode.SegmentBlendOut;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentMove;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.SegmentBlendOut;
                    return true;
                }

                mode = DragMode.SegmentMove;
                return true;
            }

            return false;
        }

        private bool TryHitNotify(Vector2 local, out int index)
        {
            index = -1;
            if (context.Montage == null || local.y < notifyTrackTop || local.y > notifyTrackTop + TrackHeight)
                return false;

            float best = 999f;
            for (int i = 0; i < context.Montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = context.Montage.Notifies[i];
                if (placement == null)
                    continue;

                float dx = Mathf.Abs(local.x - TimeToX(placement.Time));
                if (dx < 8f && dx < best)
                {
                    best = dx;
                    index = i;
                }
            }

            return index >= 0;
        }

        private bool TryHitNotifyState(Vector2 local, out int index, out DragMode mode)
        {
            index = -1;
            mode = DragMode.None;

            for (int i = notifyStateLayouts.Count - 1; i >= 0; i--)
            {
                NotifyStateLayout layout = notifyStateLayouts[i];
                if (!layout.Body.Contains(local))
                    continue;

                index = layout.Index;
                if (Mathf.Abs(local.x - layout.Body.xMin) <= EdgeHandleWidth)
                {
                    mode = DragMode.NotifyStateResizeStart;
                    return true;
                }

                if (Mathf.Abs(local.x - layout.Body.xMax) <= EdgeHandleWidth)
                {
                    mode = DragMode.NotifyStateResizeEnd;
                    return true;
                }

                mode = DragMode.NotifyStateMove;
                return true;
            }

            return false;
        }

        private void SetPlayheadFromX(float x) => context.SetPlayhead(XToTime(x));

        private float TimeToX(float time) => TrackLabelWidth + ContentPadding + time * pixelsPerSecond;

        private float XToTime(float x) => Mathf.Max(0f, (x - TrackLabelWidth - ContentPadding) / pixelsPerSecond);

        private static float Snap(float time) => Mathf.Round(time / SnapStep) * SnapStep;
    }
}
