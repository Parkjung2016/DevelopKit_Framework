using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [CreateAssetMenu(fileName = "Montage_", menuName = "PJDev/Animation/Montage")]
    public sealed class AnimMontageSO : ScriptableObject
    {
        [SerializeField] private float rateScale = 1f;
        [SerializeField] private MontageSegment[] segments = Array.Empty<MontageSegment>();
        [SerializeField] private AnimNotifyPlacement[] notifies = Array.Empty<AnimNotifyPlacement>();
        [SerializeField] private AnimNotifyStatePlacement[] notifyStates = Array.Empty<AnimNotifyStatePlacement>();
        [SerializeField] private MontageSlotDefinition[] slots = Array.Empty<MontageSlotDefinition>();
        [SerializeField] private string[] animationTracks = { "Default" };
        [SerializeField] private string[] notifyTracks = { "Default" };
        [SerializeField] private string[] notifyStateTracks = { "Default" };
        [SerializeField] private string[] timelineTrackOrder = Array.Empty<string>();

        public float RateScale => rateScale <= 0f ? 1f : rateScale;
        public IReadOnlyList<MontageSegment> Segments => segments ?? Array.Empty<MontageSegment>();
        public IReadOnlyList<AnimNotifyPlacement> Notifies => notifies ?? Array.Empty<AnimNotifyPlacement>();
        public IReadOnlyList<AnimNotifyStatePlacement> NotifyStates => notifyStates ?? Array.Empty<AnimNotifyStatePlacement>();
        public IReadOnlyList<MontageSlotDefinition> Slots => slots ?? Array.Empty<MontageSlotDefinition>();
        public IReadOnlyList<string> AnimationTracks => animationTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> NotifyTracks => notifyTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> NotifyStateTracks => notifyStateTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> TimelineTrackOrder => timelineTrackOrder ?? Array.Empty<string>();

        public float Length
        {
            get
            {
                float max = 0f;
                if (segments == null)
                    return 0f;

                for (int i = 0; i < segments.Length; i++)
                {
                    MontageSegment segment = segments[i];
                    if (segment != null)
                        max = Mathf.Max(max, segment.EndTime);
                }

                return max;
            }
        }

        public bool TryGetSegmentAtTime(float montageTime, out MontageSegment segment, out int segmentIndex)
        {
            segment = null;
            segmentIndex = -1;
            if (segments == null)
                return false;

            for (int i = 0; i < segments.Length; i++)
            {
                MontageSegment candidate = segments[i];
                if (candidate == null || !candidate.ContainsTime(montageTime))
                    continue;

                segment = candidate;
                segmentIndex = i;
                return true;
            }

            return false;
        }

        public void RebuildSegmentStartTimes()
        {
            if (segments == null || segments.Length == 0)
                return;

            float cursor = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                MontageSegment segment = segments[i];
                if (segment == null)
                    continue;

                segment.StartTime = cursor;
                cursor += segment.Duration;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            animationTracks = SanitizeTracks(animationTracks);
            notifyTracks = SanitizeTracks(notifyTracks);
            notifyStateTracks = SanitizeTracks(notifyStateTracks);
            timelineTrackOrder = SanitizeTrackOrder(timelineTrackOrder);

            if (segments == null)
                return;

            for (int i = 0; i < notifies?.Length; i++)
            {
                AnimNotifyPlacement notify = notifies[i];
                if (notify != null)
                    notify.Time = Mathf.Clamp(notify.Time, 0f, Length);
            }

            for (int i = 0; i < notifyStates?.Length; i++)
            {
                AnimNotifyStatePlacement state = notifyStates[i];
                if (state == null)
                    continue;

                state.StartTime = Mathf.Clamp(state.StartTime, 0f, Length);
                state.EndTime = Mathf.Clamp(state.EndTime, state.StartTime, Length);
            }
        }

        private static string[] SanitizeTracks(string[] tracks)
        {
            if (tracks == null || tracks.Length == 0)
                return new[] { "Default" };

            for (int i = 0; i < tracks.Length; i++)
            {
                if (string.IsNullOrEmpty(tracks[i]))
                    tracks[i] = "Default";
            }

            return tracks;
        }

        private static string[] SanitizeTrackOrder(string[] tracks)
        {
            if (tracks == null)
                return Array.Empty<string>();

            int write = 0;
            for (int i = 0; i < tracks.Length; i++)
            {
                if (string.IsNullOrEmpty(tracks[i]))
                    continue;

                tracks[write] = tracks[i];
                write++;
            }

            if (write == tracks.Length)
                return tracks;

            string[] sanitized = new string[write];
            Array.Copy(tracks, sanitized, write);
            return sanitized;
        }
#endif
    }
}
