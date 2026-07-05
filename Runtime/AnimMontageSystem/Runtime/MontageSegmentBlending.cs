using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageSegmentSample
    {
        public MontageSegmentSample(MontageSegment segment, int segmentIndex, float clipTime, float weight)
        {
            Segment = segment;
            SegmentIndex = segmentIndex;
            ClipTime = clipTime;
            Weight = weight;
        }

        public MontageSegment Segment { get; }
        public int SegmentIndex { get; }
        public float ClipTime { get; }
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
            }

            return false;
        }

        public static float GetCrossfadeDuration(MontageSegment from, MontageSegment to)
        {
            if (from?.Clip == null || to?.Clip == null)
                return 0f;

            return Mathf.Min(from.BlendOut, to.BlendIn);
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

                results.Add(new MontageSegmentSample(
                    segment,
                    i,
                    segment.ToClipTime(montageTime),
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
            MontageSegment previous = segmentIndex > 0 ? segments[segmentIndex - 1] : null;
            MontageSegment next = segmentIndex + 1 < segments.Count ? segments[segmentIndex + 1] : null;

            float crossfadeIn = previous != null && previous.Clip != null
                ? GetCrossfadeDuration(previous, segment)
                : 0f;
            float crossfadeOut = next != null && next.Clip != null
                ? GetCrossfadeDuration(segment, next)
                : 0f;

            float activeStart = start - crossfadeIn;
            float activeEnd = end + crossfadeOut;
            if (montageTime < activeStart || montageTime >= activeEnd)
                return 0f;

            if (montageTime < start)
                return crossfadeIn > 0f ? (montageTime - activeStart) / crossfadeIn : 0f;

            if (montageTime >= end)
                return crossfadeOut > 0f ? (activeEnd - montageTime) / crossfadeOut : 0f;

            float weight = 1f;

            if (crossfadeIn > 0f && montageTime - start < crossfadeIn)
                weight = (montageTime - (start - crossfadeIn)) / crossfadeIn;
            else if (segment.BlendIn > 0f && previous == null && montageTime - start < segment.BlendIn)
                weight = (montageTime - start) / segment.BlendIn;

            if (crossfadeOut > 0f && end - montageTime < crossfadeOut)
                weight *= (end - montageTime) / crossfadeOut;
            else if (segment.BlendOut > 0f && next == null && end - montageTime < segment.BlendOut)
                weight *= (end - montageTime) / segment.BlendOut;

            return Mathf.Clamp01(weight);
        }
    }
}
