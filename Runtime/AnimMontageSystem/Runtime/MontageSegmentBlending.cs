using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageSegmentSample
    {
        public MontageSegmentSample(
            MontageSegment segment,
            int segmentIndex,
            float clipTime,
            float playableClipTime,
            float weight,
            bool isHeldPose = false)
        {
            Segment = segment;
            SegmentIndex = segmentIndex;
            ClipTime = clipTime;
            PlayableClipTime = playableClipTime;
            Weight = weight;
            IsHeldPose = isHeldPose;
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
        public bool IsHeldPose { get; }
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

            bool hasActiveEmptyState = HasActiveEmptyState(montageTime, segments);
            float totalWeight = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment?.Clip == null)
                    continue;

                float weight;
                float playableClipTime;
                if (hasActiveEmptyState
                    && TryGetActiveEmptyStateOnTrack(
                        montageTime,
                        segment.TrackId,
                        segments,
                        out MontageSegment emptyState,
                        out int emptyStateIndex))
                {
                    int transitionIndex = FindEmptyStateTransitionSegment(emptyState, emptyStateIndex, segments);
                    if (transitionIndex != i)
                        continue;

                    weight = GetEmptyStateTransitionWeight(montageTime, emptyState, segment);
                    playableClipTime = segment.ToPlayableClipTime(montageTime);
                }
                else
                {
                    MontageSegment previous = FindPreviousOnTrack(i, segments);
                    MontageSegment next = FindNextOnTrack(i, segments);
                    weight = MontageBlendUtility.Evaluate(
                        ComputeWeight(montageTime, segment, previous, next));
                    playableClipTime = ComputePlayableClipTime(montageTime, segment, previous);
                }

                if (weight <= 0.0001f)
                    continue;

                results.Add(new MontageSegmentSample(
                    segment,
                    i,
                    segment.NormalizeClipTime(playableClipTime),
                    playableClipTime,
                    weight));
                totalWeight += weight;
            }

            if (hasActiveEmptyState)
                totalWeight += AddActiveEmptyStateHoldSamples(montageTime, segments, results);
            if (totalWeight <= 0.0001f)
            {
                results.Clear();
                if (!hasActiveEmptyState)
                    TryAddGapPoseSample(montageTime, segments, results);
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
                    sample.Weight / totalWeight,
                    sample.IsHeldPose);
            }
        }

        private static float AddActiveEmptyStateHoldSamples(
            float montageTime,
            IReadOnlyList<MontageSegment> segments,
            List<MontageSegmentSample> results)
        {
            float addedWeight = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment emptyState = segments[i];
                if (emptyState == null
                    || !emptyState.IsEmptyState
                    || !emptyState.ContainsTime(montageTime)
                    || !IsPrimaryActiveEmptyState(i, montageTime, segments))
                    continue;

                int transitionIndex = FindEmptyStateTransitionSegment(emptyState, i, segments);
                MontageSegment transition = transitionIndex >= 0 ? segments[transitionIndex] : null;
                float transitionWeight = transition != null
                    ? GetEmptyStateTransitionWeight(montageTime, emptyState, transition)
                    : 0f;
                float holdWeight = 1f - transitionWeight;
                if (holdWeight <= 0.0001f)
                    continue;

                int sourceIndex = FindEmptyStateHoldSource(emptyState, segments);
                if (sourceIndex < 0)
                    continue;

                MontageSegment source = segments[sourceIndex];
                float poseTime = Mathf.Max(
                    source.StartTime,
                    Mathf.Min(emptyState.StartTime - 0.0001f, source.EndTime - 0.0001f));
                float playableClipTime = source.ToPlayableClipTime(poseTime);
                results.Add(new MontageSegmentSample(
                    source,
                    sourceIndex,
                    source.NormalizeClipTime(playableClipTime),
                    playableClipTime,
                    holdWeight,
                    true));
                addedWeight += holdWeight;
            }

            return addedWeight;
        }

        private static bool TryGetActiveEmptyStateOnTrack(
            float montageTime,
            string trackId,
            IReadOnlyList<MontageSegment> segments,
            out MontageSegment emptyState,
            out int emptyStateIndex)
        {
            emptyState = null;
            emptyStateIndex = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment candidate = segments[i];
                if (candidate == null
                    || !candidate.IsEmptyState
                    || candidate.TrackId != trackId
                    || !candidate.ContainsTime(montageTime))
                    continue;

                if (emptyState != null && candidate.StartTime < emptyState.StartTime)
                    continue;

                emptyState = candidate;
                emptyStateIndex = i;
            }

            return emptyState != null;
        }

        private static bool IsPrimaryActiveEmptyState(
            int emptyStateIndex,
            float montageTime,
            IReadOnlyList<MontageSegment> segments)
        {
            MontageSegment current = segments[emptyStateIndex];
            for (int i = 0; i < segments.Count; i++)
            {
                if (i == emptyStateIndex)
                    continue;

                MontageSegment candidate = segments[i];
                if (candidate == null
                    || !candidate.IsEmptyState
                    || candidate.TrackId != current.TrackId
                    || !candidate.ContainsTime(montageTime))
                    continue;

                if (candidate.StartTime > current.StartTime
                    || Mathf.Approximately(candidate.StartTime, current.StartTime) && i > emptyStateIndex)
                    return false;
            }

            return true;
        }

        private static int FindEmptyStateTransitionSegment(
            MontageSegment emptyState,
            int emptyStateIndex,
            IReadOnlyList<MontageSegment> segments)
        {
            int result = -1;
            float earliestStartTime = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                if (i == emptyStateIndex)
                    continue;

                MontageSegment candidate = segments[i];
                if (candidate?.Clip == null
                    || candidate.TrackId != emptyState.TrackId
                    || candidate.StartTime < emptyState.StartTime
                    || candidate.StartTime >= emptyState.EndTime
                    || candidate.StartTime >= earliestStartTime)
                    continue;

                earliestStartTime = candidate.StartTime;
                result = i;
            }

            return result;
        }

        private static int FindEmptyStateHoldSource(
            MontageSegment emptyState,
            IReadOnlyList<MontageSegment> segments)
        {
            int result = -1;
            float latestStartTime = float.MinValue;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment candidate = segments[i];
                if (candidate?.Clip == null
                    || candidate.TrackId != emptyState.TrackId
                    || candidate.StartTime >= emptyState.StartTime
                    || candidate.StartTime <= latestStartTime)
                    continue;

                latestStartTime = candidate.StartTime;
                result = i;
            }

            return result;
        }

        private static float GetEmptyStateTransitionWeight(
            float montageTime,
            MontageSegment emptyState,
            MontageSegment transition)
        {
            float blendEndTime = Mathf.Min(emptyState.EndTime, transition.EndTime);
            if (blendEndTime <= transition.StartTime + 0.0001f)
                return montageTime >= transition.StartTime ? 1f : 0f;

            float normalizedTime = Mathf.InverseLerp(transition.StartTime, blendEndTime, montageTime);
            return MontageBlendUtility.Evaluate(normalizedTime);
        }
        public static bool HasActiveEmptyState(
            float montageTime,
            IReadOnlyList<MontageSegment> segments)
        {
            if (segments == null)
                return false;

            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment != null && segment.IsEmptyState && segment.ContainsTime(montageTime))
                    return true;
            }

            return false;
        }

        private static bool TryAddGapPoseSample(
            float montageTime,
            IReadOnlyList<MontageSegment> segments,
            List<MontageSegmentSample> results)
        {
            int previousIndex = -1;
            int nextIndex = -1;
            float latestEndTime = float.MinValue;
            float earliestStartTime = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment?.Clip == null || segment.Duration <= 0f)
                    continue;
                if (segment.EndTime <= montageTime + 0.0001f && segment.EndTime > latestEndTime)
                {
                    previousIndex = i;
                    latestEndTime = segment.EndTime;
                }
                if (segment.StartTime >= montageTime - 0.0001f && segment.StartTime < earliestStartTime)
                {
                    nextIndex = i;
                    earliestStartTime = segment.StartTime;
                }
            }
            int segmentIndex = previousIndex >= 0 ? previousIndex : nextIndex;
            if (segmentIndex < 0)
                return false;
            MontageSegment fallback = segments[segmentIndex];
            float playableClipTime;
            if (previousIndex >= 0)
            {
                float sampleTime = fallback.IsLoopingClip
                    ? Mathf.Max(fallback.StartTime, fallback.EndTime - 0.0001f / fallback.PlayRate)
                    : fallback.EndTime;
                playableClipTime = fallback.ToPlayableClipTime(sampleTime);
            }
            else
            {
                playableClipTime = fallback.ClipStartTime;
            }
            results.Add(new MontageSegmentSample(
                fallback,
                segmentIndex,
                fallback.NormalizeClipTime(playableClipTime),
                playableClipTime,
                1f,
                true));
            return true;
        }
        private static float ComputeWeight(
            float montageTime,
            MontageSegment segment,
            MontageSegment previous,
            MontageSegment next)
        {
            float duration = segment.Duration;
            if (duration <= 0f)
                return 0f;

            float start = segment.StartTime;
            float end = segment.EndTime;
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
            MontageSegment previous)
        {
            float start = segment.StartTime;
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