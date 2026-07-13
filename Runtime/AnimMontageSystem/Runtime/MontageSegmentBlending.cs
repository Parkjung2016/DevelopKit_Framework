using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageSegmentSample
    {
        public MontageSegmentSample(MontageSegment segment, int segmentIndex, float clipTime, float playableClipTime, float weight)
        {
            Segment = segment;
            SegmentIndex = segmentIndex;
            ClipTime = clipTime;
            PlayableClipTime = playableClipTime;
            Weight = weight;
        }

        public MontageSegmentSample(MontageSegment segment, int segmentIndex, float clipTime, float weight)
            : this(segment, segmentIndex, clipTime, clipTime, weight)
        {
        }

        public MontageSegment Segment { get; }
        public int SegmentIndex { get; }
        public float ClipTime { get; }
        public float PlayableClipTime { get; }
        public float Weight { get; }
    }

    public static class MontageSegmentBlending
    {
        public static bool MontageHasBlends(IReadOnlyList<MontageSegment> segments)
        {
            if (segments == null)
                return false;

            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment == null)
                    continue;

                if (segment.BlendIn > 0f || segment.BlendOut > 0f)
                    return true;

                if (FindNextOnTrack(i, segments) is MontageSegment next
                    && GetOverlapDuration(segment, next) > 0f)
                    return true;
            }

            return false;
        }

        public static float GetCrossfadeDuration(MontageSegment from, MontageSegment to)
        {
            if (from?.Clip == null || to?.Clip == null)
                return 0f;

            float manual = Mathf.Min(from.BlendOut, to.BlendIn);
            float overlap = GetOverlapDuration(from, to);
            return Mathf.Max(manual, overlap);
        }

        public static void Evaluate(float montageTime, IReadOnlyList<MontageSegment> segments, List<MontageSegmentSample> results)
        {
            results.Clear();
            if (segments == null || segments.Count == 0)
                return;

            float totalWeight = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment?.Clip == null)
                    continue;

                float weight = ComputeWeight(montageTime, segment, i, segments);
                if (weight <= 0.0001f)
                    continue;

                float playableClipTime = ComputePlayableClipTime(montageTime, segment, i, segments);
                results.Add(new MontageSegmentSample(
                    segment,
                    i,
                    segment.NormalizeClipTime(playableClipTime),
                    playableClipTime,
                    weight));
                totalWeight += weight;
            }

            if (totalWeight <= 0.0001f)
            {
                results.Clear();
                return;
            }

            if (Mathf.Approximately(totalWeight, 1f))
                return;

            for (int i = 0; i < results.Count; i++)
            {
                MontageSegmentSample sample = results[i];
                results[i] = new MontageSegmentSample(
                    sample.Segment,
                    sample.SegmentIndex,
                    sample.ClipTime,
                    sample.PlayableClipTime,
                    sample.Weight / totalWeight);
            }
        }

        private static float ComputeWeight(
            float montageTime,
            MontageSegment segment,
            int segmentIndex,
            IReadOnlyList<MontageSegment> segments)
        {
            float duration = segment.Duration;
            if (duration <= 0f)
                return 0f;

            float start = segment.StartTime;
            float end = segment.EndTime;
            MontageSegment previous = FindPreviousOnTrack(segmentIndex, segments);
            MontageSegment next = FindNextOnTrack(segmentIndex, segments);

            float crossfadeIn = previous != null && previous.Clip != null
                ? GetCrossfadeDuration(previous, segment)
                : 0f;
            float crossfadeOut = next != null && next.Clip != null
                ? GetCrossfadeDuration(segment, next)
                : 0f;

            bool overlapsPrevious = previous != null && previous.EndTime > start;
            bool overlapsNext = next != null && next.StartTime < end;
            float fadeInStart = crossfadeIn > 0f && !overlapsPrevious ? start - crossfadeIn : start;
            float fadeInEnd = crossfadeIn > 0f && overlapsPrevious ? Mathf.Min(previous.EndTime, end) : start;
            float fadeOutStart = crossfadeOut > 0f && overlapsNext ? Mathf.Max(start, next.StartTime) : end - crossfadeOut;
            float fadeOutEnd = end;

            float activeStart = fadeInStart;
            float activeEnd = end;
            if (montageTime < activeStart || montageTime >= activeEnd)
                return 0f;

            if (crossfadeIn > 0f && montageTime < fadeInEnd)
                return (montageTime - fadeInStart) / Mathf.Max(0.0001f, fadeInEnd - fadeInStart);

            float weight = 1f;

            if (crossfadeIn > 0f && !overlapsPrevious && montageTime < start)
                weight = (montageTime - fadeInStart) / crossfadeIn;
            else if (segment.BlendIn > 0f && previous == null && montageTime - start < segment.BlendIn)
                weight = (montageTime - start) / segment.BlendIn;

            if (crossfadeOut > 0f && next != null)
            {
                if (montageTime >= fadeOutStart)
                    weight *= (fadeOutEnd - montageTime) / Mathf.Max(0.0001f, fadeOutEnd - fadeOutStart);
            }
            else if (segment.BlendOut > 0f && next == null && end - montageTime < segment.BlendOut)
                weight *= (end - montageTime) / segment.BlendOut;

            return Mathf.Clamp01(weight);
        }

        private static float ComputePlayableClipTime(
            float montageTime,
            MontageSegment segment,
            int segmentIndex,
            IReadOnlyList<MontageSegment> segments)
        {
            float start = segment.StartTime;
            MontageSegment previous = FindPreviousOnTrack(segmentIndex, segments);
            float crossfadeIn = previous != null && previous.Clip != null
                ? GetCrossfadeDuration(previous, segment)
                : 0f;

            bool overlapsPrevious = previous != null && previous.EndTime > start;
            float clipClockStart = crossfadeIn > 0f && !overlapsPrevious ? start - crossfadeIn : start;
            float local = segment.ClipStartTime + (montageTime - clipClockStart) * segment.PlayRate;
            return segment.IsLoopingClip ? local : segment.NormalizeClipTime(local);
        }

        private static MontageSegment FindPreviousOnTrack(int segmentIndex, IReadOnlyList<MontageSegment> segments)
        {
            MontageSegment current = GetSegment(segmentIndex, segments);
            if (current == null)
                return null;

            MontageSegment previous = null;
            float bestStartTime = float.MinValue;
            for (int i = 0; i < segments.Count; i++)
            {
                if (i == segmentIndex)
                    continue;

                MontageSegment candidate = segments[i];
                if (candidate?.Clip == null)
                    continue;

                if (candidate.StartTime > current.StartTime || candidate.StartTime <= bestStartTime)
                    continue;

                bestStartTime = candidate.StartTime;
                previous = candidate;
            }

            return previous;
        }

        private static MontageSegment FindNextOnTrack(int segmentIndex, IReadOnlyList<MontageSegment> segments)
        {
            MontageSegment current = GetSegment(segmentIndex, segments);
            if (current == null)
                return null;

            MontageSegment next = null;
            float bestStartTime = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                if (i == segmentIndex)
                    continue;

                MontageSegment candidate = segments[i];
                if (candidate?.Clip == null)
                    continue;

                if (candidate.StartTime <= current.StartTime || candidate.StartTime >= bestStartTime)
                    continue;

                bestStartTime = candidate.StartTime;
                next = candidate;
            }

            return next;
        }

        private static MontageSegment GetSegment(int segmentIndex, IReadOnlyList<MontageSegment> segments)
        {
            if (segments == null || segmentIndex < 0 || segmentIndex >= segments.Count)
                return null;

            return segments[segmentIndex];
        }

        private static float GetOverlapDuration(MontageSegment from, MontageSegment to)
        {
            float start = Mathf.Max(from.StartTime, to.StartTime);
            float end = Mathf.Min(from.EndTime, to.EndTime);
            return Mathf.Max(0f, end - start);
        }
    }
}