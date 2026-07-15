using System.Reflection;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed partial class MontageTimelineView
    {
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            segmentLayouts.Clear();
            notifyLayouts.Clear();
            notifyStateLayouts.Clear();
            trackRows.Clear();

            var painter = ctx.painter2D;
            DrawBackground(painter, rect);

            AnimMontageSO montage = context.Montage;
            if (montage == null)
                return;

            float montageLength = montage.Length;
            DrawRuler(painter, rect, montageLength);
            DrawTimeGrid(painter, rect, montageLength);
            FillOrderedTimelineTracks(montage, orderedTrackBuffer, allTrackBuffer);
            totalTrackContentHeight = orderedTrackBuffer.Count * (TrackHeight + TrackGap);
            ClampViewStartY();

            float y = RulerHeight + TrackGap - viewStartY;
            foreach (TrackIdentity track in orderedTrackBuffer)
            {
                switch (track.Kind)
                {
                    case TrackKind.Segment:
                        y = DrawSegmentTrack(painter, rect, y, montage, track.TrackId);
                        break;

                    case TrackKind.Notify:
                        y = DrawNotifyTrack(painter, rect, y, montage, track.TrackId);
                        break;

                    case TrackKind.NotifyState:
                        y = DrawNotifyStateTrack(painter, rect, y, montage, track.TrackId);
                        break;

                }
            }

            DrawSnapGuide(painter, rect);
            DrawPlayhead(painter, rect);
            DrawBoxSelection(painter);
            DrawPlayModeLockedOverlay(painter, rect);
        }

        private static void DrawPlayModeLockedOverlay(Painter2D painter, Rect rect)
        {
            if (!EditorApplication.isPlaying)
                return;

            painter.fillColor = new Color(0f, 0f, 0f, 0.28f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
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

        private void DrawTimeGrid(Painter2D painter, Rect rect, float length)
        {
            float maxTime = Mathf.Max(length, 1f);
            float minorStep = pixelsPerSecond >= 180f ? 0.25f : pixelsPerSecond >= 90f ? 0.5f : 1f;
            float majorStep = pixelsPerSecond >= 90f ? 1f : 5f;

            for (float t = 0f; t <= maxTime; t += minorStep)
            {
                bool major = Mathf.Abs(t / majorStep - Mathf.Round(t / majorStep)) < 0.001f;
                painter.strokeColor = major
                    ? new Color(1f, 1f, 1f, 0.09f)
                    : new Color(1f, 1f, 1f, 0.035f);
                painter.lineWidth = major ? 1.2f : 1f;
                float x = TimeToX(t);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, RulerHeight));
                painter.LineTo(new Vector2(x, rect.yMax));
                painter.Stroke();
            }
        }

        private float DrawSegmentTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.34f, 0.62f, 0.35f), TrackKind.Segment, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.Segment, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);

            FillSegmentIndicesForTrack(montage, trackId);
            for (int i = 0; i < segmentIndexBuffer.Count; i++)
            {
                int segmentIndex = segmentIndexBuffer[i];
                MontageSegment segment = montage.Segments[segmentIndex];
                if (segment == null || !segment.IsEmptyState && segment.Clip == null)
                    continue;

                float x0 = TimeToX(segment.StartTime);
                float x1 = TimeToX(segment.EndTime);
                var body = new Rect(x0, y + 2f, Mathf.Max(4f, x1 - x0), TrackHeight - 4f);
                if (!TryClipRect(body, contentRect, out Rect clippedBody))
                    continue;

                bool selected = context.IsSegmentSelected(segmentIndex);
                Color segmentColor = segment.HasCustomColor
                    ? segment.CustomColor
                    : segment.IsEmptyState
                        ? EmptySegmentColor
                        : SegmentCoreColor;

                painter.fillColor = selected ? HighlightColor(segmentColor) : segmentColor;
                FillRoundedRect(painter, clippedBody, 3f);
                painter.strokeColor = selected ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.22f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, clippedBody, 3f);

                if (segment.IsEmptyState)
                {
                    DrawEmptyStateGlyph(painter, clippedBody);
                }
                else
                {
                    DrawSegmentAutoBlendOverlay(
                        painter,
                        clippedBody,
                        montage,
                        segment,
                        segmentIndex,
                        contentRect);
                    DrawSegmentLoopBadge(painter, clippedBody, segment.IsLoopingClip);
                }

                if (clippedBody.width > 28f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
                    painter.lineWidth = 1f;
                    float midY = clippedBody.yMin + clippedBody.height * 0.5f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(clippedBody.xMin + 6f, midY));
                    painter.LineTo(new Vector2(clippedBody.xMax - 6f, midY));
                    painter.Stroke();
                }

                DrawSegmentEdgeTicks(painter, clippedBody);

                segmentLayouts.Add(new SegmentLayout(segmentIndex, clippedBody));
            }

            return y + TrackHeight + TrackGap;
        }

        private static void DrawEmptyStateGlyph(Painter2D painter, Rect segmentRect)
        {
            if (segmentRect.width < 20f)
                return;

            float centerX = segmentRect.center.x;
            float top = segmentRect.center.y - 6f;
            float bottom = segmentRect.center.y + 6f;
            painter.strokeColor = new Color(0.92f, 0.95f, 1f, 0.82f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX - 3f, top));
            painter.LineTo(new Vector2(centerX - 3f, bottom));
            painter.MoveTo(new Vector2(centerX + 3f, top));
            painter.LineTo(new Vector2(centerX + 3f, bottom));
            painter.Stroke();
        }
        private static void DrawSegmentLoopBadge(Painter2D painter, Rect segmentRect, bool isLooping)
        {
            if (segmentRect.width < 42f)
                return;

            float width = isLooping ? 20f : 18f;
            Rect badge = new(
                Mathf.Max(segmentRect.xMin + 4f, segmentRect.xMax - width - 4f),
                segmentRect.yMin + 4f,
                width,
                14f);

            painter.fillColor = isLooping
                ? new Color(0.08f, 0.46f, 0.24f, 0.9f)
                : new Color(0.08f, 0.08f, 0.09f, 0.72f);
            FillRoundedRect(painter, badge, 3f);
            painter.strokeColor = isLooping
                ? new Color(0.82f, 1f, 0.86f, 1f)
                : new Color(1f, 1f, 1f, 0.78f);
            painter.lineWidth = 1.2f;

            if (isLooping)
            {
                float cy = badge.center.y;
                painter.BeginPath();
                painter.Arc(new Vector2(badge.center.x - 1f, cy), 4f, 35f, 315f);
                painter.Stroke();
                DrawTriangle(painter, badge.center.x + 5f, cy - 2.5f, 3f, painter.strokeColor);
                return;
            }

            painter.BeginPath();
            painter.MoveTo(new Vector2(badge.xMin + 5f, badge.center.y));
            painter.LineTo(new Vector2(badge.xMax - 5f, badge.center.y));
            painter.Stroke();
        }

        private float DrawNotifyTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.18f, 0.62f, 0.72f, 0.22f), TrackKind.Notify, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.Notify, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);
            for (int i = 0; i < montage.Notifies.Count; i++)
            {
                AnimNotifyPlacement placement = montage.Notifies[i];
                if (placement == null || placement.TrackId != trackId)
                    continue;

                Color color = placement.HasCustomColor
                    ? placement.CustomColor
                    : placement.Notify != null ? placement.Notify.EditorColor : new Color(0.4f, 0.8f, 1f);
                float x = TimeToX(placement.Time);
                AudioClip audioClip = GetAudioClipFromNotify(placement.Notify);
                float visibleEndX = audioClip != null ? TimeToX(placement.Time + audioClip.length) : x;
                if (visibleEndX < contentRect.xMin - 8f || x > contentRect.xMax + 8f)
                    continue;

                DrawNotifyAudioWaveform(painter, placement, audioClip, y, contentRect, color);
                if (x >= contentRect.xMin - 8f && x <= contentRect.xMax + 8f)
                    DrawDiamond(painter, x, y + TrackHeight * 0.5f, 7f, color);
                var hitRect = new Rect(x - 9f, y + TrackHeight * 0.5f - 9f, 18f, 18f);
                notifyLayouts.Add(new NotifyLayout(i, hitRect));
                if (context.IsNotifySelected(i) && x >= contentRect.xMin - 8f && x <= contentRect.xMax + 8f)
                {
                    painter.strokeColor = new Color(1f, 1f, 1f, 0.78f);
                    painter.lineWidth = 1.5f;
                    StrokeDiamond(painter, x, y + TrackHeight * 0.5f, 9.5f);
                }
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyAudioWaveform(
            Painter2D painter,
            AnimNotifyPlacement placement,
            AudioClip clip,
            float y,
            Rect contentRect,
            Color color)
        {
            if (clip == null || clip.length <= 0f)
                return;

            float x0 = TimeToX(placement.Time);
            float x1 = TimeToX(placement.Time + clip.length);
            var body = new Rect(x0, y + 5f, Mathf.Max(8f, x1 - x0), TrackHeight - 10f);
            if (!TryClipRect(body, contentRect, out Rect clippedBody))
                return;

            painter.fillColor = new Color(color.r, color.g, color.b, 0.16f);
            FillRoundedRect(painter, clippedBody, 3f);
            painter.strokeColor = new Color(color.r, color.g, color.b, 0.5f);
            painter.lineWidth = 1f;
            StrokeRoundedRect(painter, clippedBody, 3f);

            float[] peaks = GetAudioWaveformPeaks(clip);
            if (peaks == null || peaks.Length == 0 || clippedBody.width < 6f)
                return;

            painter.strokeColor = new Color(0.72f, 0.9f, 1f, 0.82f);
            painter.lineWidth = 1f;
            float centerY = clippedBody.center.y;
            float amplitude = clippedBody.height * 0.42f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(clippedBody.width), 4, peaks.Length);
            for (int i = 0; i < columns; i++)
            {
                int peakIndex = Mathf.Clamp(Mathf.RoundToInt(i / Mathf.Max(1f, columns - 1f) * (peaks.Length - 1)), 0, peaks.Length - 1);
                float x = Mathf.Lerp(clippedBody.xMin + 2f, clippedBody.xMax - 2f, i / Mathf.Max(1f, columns - 1f));
                float fallback = 0.16f + 0.1f * Mathf.Sin(i * 0.55f);
                float h = Mathf.Max(1f, Mathf.Max(peaks[peakIndex], fallback) * amplitude);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, centerY - h));
                painter.LineTo(new Vector2(x, centerY + h));
                painter.Stroke();
            }
        }

        private static AudioClip GetAudioClipFromNotify(AnimNotify notify)
        {
            if (notify == null)
                return null;

            FieldInfo field = notify.GetType().GetField("clip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(AudioClip)
                ? field.GetValue(notify) as AudioClip
                : null;
        }

        private float[] GetAudioWaveformPeaks(AudioClip clip)
        {
            if (audioWaveformCache.TryGetValue(clip, out float[] peaks))
                return peaks;

            const int PeakCount = 96;
            peaks = new float[PeakCount];
            audioWaveformCache[clip] = peaks;
            if (clip == null || clip.samples <= 0 || clip.channels <= 0)
                return peaks;

            int sampleCount = Mathf.Min(clip.samples * clip.channels, 44100 * clip.channels * 20);
            float[] samples = new float[sampleCount];
            try
            {
                if (!clip.GetData(samples, 0))
                    return peaks;
            }
            catch
            {
                return peaks;
            }

            int samplesPerPeak = Mathf.Max(1, sampleCount / PeakCount);
            for (int i = 0; i < PeakCount; i++)
            {
                int start = i * samplesPerPeak;
                int end = Mathf.Min(sampleCount, start + samplesPerPeak);
                float max = 0f;
                for (int j = start; j < end; j++)
                    max = Mathf.Max(max, Mathf.Abs(samples[j]));

                peaks[i] = Mathf.Clamp01(max);
            }

            return peaks;
        }

        private float DrawNotifyStateTrack(Painter2D painter, Rect rect, float y, AnimMontageSO montage, string trackId)
        {
            DrawTrackRow(painter, rect, y, new Color(0.28f, 0.72f, 0.42f, 0.18f), TrackKind.NotifyState, trackId);
            trackRows.Add(new TrackRowLayout(TrackKind.NotifyState, trackId, new Rect(rect.xMin, y, rect.width, TrackHeight)));
            Rect contentRect = GetTimelineContentRect(rect);
            for (int i = 0; i < montage.NotifyStates.Count; i++)
            {
                AnimNotifyStatePlacement placement = montage.NotifyStates[i];
                if (placement == null || placement.TrackId != trackId || placement.NotifyState == null)
                    continue;

                float x0 = TimeToX(placement.StartTime);
                float x1 = TimeToX(placement.EndTime);
                var bar = new Rect(x0, y, Mathf.Max(4f, x1 - x0), TrackHeight);
                if (!TryClipRect(bar, contentRect, out Rect clippedBar))
                    continue;

                bool selected = context.IsNotifyStateSelected(i);
                Color stateColor = placement.HasCustomColor
                    ? placement.CustomColor
                    : placement.NotifyState.EditorColor;
                Color bodyColor = selected
                    ? HighlightColor(stateColor) * new Color(1f, 1f, 1f, 0.9f)
                    : stateColor * new Color(1f, 1f, 1f, 0.62f);
                Rect body = new(
                    clippedBar.xMin,
                    clippedBar.yMin + 3f,
                    clippedBar.width,
                    Mathf.Max(1f, clippedBar.height - 6f));
                painter.fillColor = bodyColor;
                FillRoundedRect(painter, body, 4f);

                painter.strokeColor = selected
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.36f);
                painter.lineWidth = selected ? 1.6f : 1f;
                StrokeRoundedRect(painter, body, 4f);

                Rect inner = new(
                    body.xMin + 2f,
                    body.yMin + 2f,
                    Mathf.Max(0f, body.width - 4f),
                    Mathf.Max(0f, body.height * 0.42f));
                if (inner.width > 0f && inner.height > 0f)
                {
                    painter.fillColor = new Color(1f, 1f, 1f, selected ? 0.22f : 0.14f);
                    FillRoundedRect(painter, inner, 3f);
                }

                DrawNotifyStateAudioWaveform(painter, placement, body, selected);
                DrawSegmentEdgeTicks(painter, body);

                float gripWidth = Mathf.Min(5f, Mathf.Max(2f, body.width * 0.25f));
                if (body.width >= 8f)
                {
                    painter.fillColor = new Color(0f, 0f, 0f, selected ? 0.34f : 0.22f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMin + gripWidth, body.yMin));
                    painter.LineTo(new Vector2(body.xMin, body.center.y));
                    painter.LineTo(new Vector2(body.xMin + gripWidth, body.yMax));
                    painter.ClosePath();
                    painter.Fill();

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(body.xMax - gripWidth, body.yMin));
                    painter.LineTo(new Vector2(body.xMax, body.center.y));
                    painter.LineTo(new Vector2(body.xMax - gripWidth, body.yMax));
                    painter.ClosePath();
                    painter.Fill();
                }

                painter.strokeColor = new Color(0f, 0f, 0f, selected ? 0.34f : 0.2f);
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(body.xMin + gripWidth, body.center.y));
                painter.LineTo(new Vector2(body.xMax - gripWidth, body.center.y));
                painter.Stroke();
                notifyStateLayouts.Add(new NotifyStateLayout(i, clippedBar));
            }

            return y + TrackHeight + TrackGap;
        }

        private void DrawNotifyStateAudioWaveform(
            Painter2D painter,
            AnimNotifyStatePlacement placement,
            Rect body,
            bool selected)
        {
            AudioClip clip = GetAudioClipFromNotifyState(placement.NotifyState);
            if (clip == null || clip.length <= 0f || body.width < 8f)
                return;

            float[] peaks = GetAudioWaveformPeaks(clip);
            if (peaks == null || peaks.Length == 0)
                return;

            Rect waveRect = new(
                body.xMin + 6f,
                body.yMin + 6f,
                Mathf.Max(0f, body.width - 12f),
                Mathf.Max(0f, body.height - 12f));
            if (waveRect.width <= 2f || waveRect.height <= 2f)
                return;

            painter.strokeColor = selected
                ? new Color(1f, 0.96f, 0.72f, 0.95f)
                : new Color(1f, 0.93f, 0.62f, 0.76f);
            painter.lineWidth = 1f;

            float centerY = waveRect.center.y;
            float amplitude = waveRect.height * 0.46f;
            int columns = Mathf.Clamp(Mathf.FloorToInt(waveRect.width), 4, 240);
            for (int i = 0; i < columns; i++)
            {
                float normalized = i / Mathf.Max(1f, columns - 1f);
                float timeInState = normalized * Mathf.Max(0.0001f, placement.Duration);
                float clipPhase = Mathf.Repeat(timeInState, clip.length) / clip.length;
                int peakIndex = Mathf.Clamp(Mathf.RoundToInt(clipPhase * (peaks.Length - 1)), 0, peaks.Length - 1);
                float x = Mathf.Lerp(waveRect.xMin, waveRect.xMax, normalized);
                float fallback = 0.16f + 0.1f * Mathf.Sin(i * 0.55f);
                float h = Mathf.Max(1f, Mathf.Max(peaks[peakIndex], fallback) * amplitude);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, centerY - h));
                painter.LineTo(new Vector2(x, centerY + h));
                painter.Stroke();
            }
        }

        private static AudioClip GetAudioClipFromNotifyState(AnimNotifyState notifyState)
        {
            if (notifyState == null)
                return null;

            FieldInfo field = notifyState.GetType().GetField("clip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(AudioClip)
                ? field.GetValue(notifyState) as AudioClip
                : null;
        }

        private void DrawTrackRow(Painter2D painter, Rect rect, float y, Color accentColor, TrackKind kind, string trackId)
        {
            bool hovered = hasHoverTrack && hoverTrackKind == kind && hoverTrackId == trackId;
            bool selected = context.IsTimelineTrackSelected(GetTrackKey(kind, trackId));
            var row = new Rect(rect.xMin + TrackLabelWidth, y, rect.width - TrackLabelWidth, TrackHeight);
            painter.fillColor = hovered ? new Color(0.16f, 0.16f, 0.18f, 1f) : TrackRowColor;
            FillRect(painter, row);

            var labelRect = new Rect(rect.xMin + 2f, y + 1f, TrackLabelWidth - 4f, TrackHeight - 2f);
            painter.fillColor = hovered || selected
                ? accentColor * new Color(1.25f, 1.25f, 1.25f, 1.55f)
                : accentColor;
            FillRoundedRect(painter, labelRect, 3f);
            if (hovered || selected)
            {
                painter.strokeColor = selected ? new Color(1f, 0.86f, 0.28f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
                painter.lineWidth = selected ? 1.8f : 1.4f;
                StrokeRoundedRect(painter, labelRect, 3f);
            }

            DrawTrackGrip(painter, labelRect);
        }

        private static void DrawTrackGrip(Painter2D painter, Rect rect)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.45f);
            painter.lineWidth = 1.5f;
            float centerY = rect.center.y;
            for (int i = -1; i <= 1; i++)
            {
                float y = centerY + i * 6f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin + 14f, y));
                painter.LineTo(new Vector2(rect.xMax - 14f, y));
                painter.Stroke();
            }
        }

        private void DrawSegmentAutoBlendOverlay(
            Painter2D painter,
            Rect body,
            AnimMontageSO montage,
            MontageSegment segment,
            int segmentIndex,
            Rect contentRect)
        {
            for (int i = 0; i < montage.Segments.Count; i++)
            {
                if (i == segmentIndex)
                    continue;

                MontageSegment other = montage.Segments[i];
                if (other?.Clip == null)
                    continue;

                float overlapStart = Mathf.Max(segment.StartTime, other.StartTime);
                float overlapEnd = Mathf.Min(segment.EndTime, other.EndTime);
                if (overlapEnd <= overlapStart)
                    continue;

                float x0 = Mathf.Max(body.xMin, contentRect.xMin, TimeToX(overlapStart));
                float x1 = Mathf.Min(body.xMax, contentRect.xMax, TimeToX(overlapEnd));
                if (x1 <= x0)
                    continue;

                var blendRect = new Rect(x0, body.yMin + 6f, x1 - x0, body.height - 12f);
                painter.fillColor = AutoBlendOverlayColor;
                FillRoundedRect(painter, blendRect, 2f);
                painter.strokeColor = new Color(1f, 0.88f, 0.36f, 0.82f);
                painter.lineWidth = 1.3f;
                StrokeRoundedRect(painter, blendRect, 2f);
                DrawDiagonalHatch(painter, blendRect, new Color(1f, 1f, 1f, 0.22f));
            }
        }

        private static void DrawSegmentEdgeTicks(Painter2D painter, Rect rect)
        {
            painter.strokeColor = new Color(1f, 1f, 1f, 0.32f);
            painter.lineWidth = 1f;

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? rect.xMin + 3f : rect.xMax - 3f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin + 5f));
                painter.LineTo(new Vector2(x, rect.yMax - 5f));
                painter.Stroke();
            }
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

        private static void StrokeRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            if (radius <= 0f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Stroke();
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
            painter.Stroke();
        }

        private void DrawSnapGuide(Painter2D painter, Rect rect)
        {
            if (!hasSnapGuide)
                return;

            float x = TimeToX(snapGuideTime);
            painter.strokeColor = new Color(1f, 0.86f, 0.24f, 0.95f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, RulerHeight));
            painter.LineTo(new Vector2(x, rect.yMax));
            painter.Stroke();
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

        private void DrawBoxSelection(Painter2D painter)
        {
            if (dragMode != DragMode.BoxSelect)
                return;

            Rect rect = GetBoxSelectionRect();
            if (rect.width < 3f && rect.height < 3f)
                return;

            painter.fillColor = new Color(0.35f, 0.62f, 1f, 0.16f);
            FillRect(painter, rect);
            painter.strokeColor = new Color(0.68f, 0.84f, 1f, 0.9f);
            painter.lineWidth = 1.2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
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

        private static void DrawTriangle(Painter2D painter, float cx, float cy, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx - radius, cy - radius));
            painter.LineTo(new Vector2(cx - radius, cy + radius));
            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokeDiamond(Painter2D painter, float cx, float cy, float radius)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(cx, cy - radius));
            painter.LineTo(new Vector2(cx + radius, cy));
            painter.LineTo(new Vector2(cx, cy + radius));
            painter.LineTo(new Vector2(cx - radius, cy));
            painter.ClosePath();
            painter.Stroke();
        }
    }
}