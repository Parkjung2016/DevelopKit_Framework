using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [CreateAssetMenu(fileName = "Montage_", menuName = "PJDev/Animation/Montage")]
    public sealed class AnimMontageSO : ScriptableObject
    {
        [Min(0.01f)]
        [SerializeField] private float rateScale = 1f;
                [Min(0f)]
        [SerializeField] private float blendIn;
        [Min(0f)]
        [SerializeField] private float blendOut;
        [SerializeField] private bool applyRootMotion;
        [SerializeField] private MontageSegment[] segments = Array.Empty<MontageSegment>();
        [SerializeField] private AnimNotifyPlacement[] notifies = Array.Empty<AnimNotifyPlacement>();
        [SerializeField] private AnimNotifyStatePlacement[] notifyStates = Array.Empty<AnimNotifyStatePlacement>();
        [SerializeField] private CustomMontageTrack[] customTracks = Array.Empty<CustomMontageTrack>();
        [SerializeField] private CustomMontageElementPlacement[] customElements = Array.Empty<CustomMontageElementPlacement>();
        [SerializeField] private MontageSlotDefinition[] slots = Array.Empty<MontageSlotDefinition>();
        [SerializeField] private string[] animationTracks = { "Default" };
        [SerializeField] private string[] notifyTracks = { "Default" };
        [SerializeField] private string[] notifyStateTracks = { "Default" };
        [SerializeField] private string[] timelineTrackOrder = Array.Empty<string>();

        public float RateScale => Mathf.Max(0.01f, rateScale);
        public float BlendIn => Mathf.Max(0f, blendIn);
        public float BlendOut => Mathf.Max(0f, blendOut);
        public bool ApplyRootMotion => applyRootMotion;
        public IReadOnlyList<MontageSegment> Segments => segments ?? Array.Empty<MontageSegment>();
        public IReadOnlyList<AnimNotifyPlacement> Notifies => notifies ?? Array.Empty<AnimNotifyPlacement>();
        public IReadOnlyList<AnimNotifyStatePlacement> NotifyStates => notifyStates ?? Array.Empty<AnimNotifyStatePlacement>();
        public IReadOnlyList<CustomMontageTrack> CustomTracks => customTracks ?? Array.Empty<CustomMontageTrack>();
        public IReadOnlyList<CustomMontageElementPlacement> CustomElements => customElements ?? Array.Empty<CustomMontageElementPlacement>();
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
                for (int i = 0; segments != null && i < segments.Length; i++)
                {
                    MontageSegment segment = segments[i];
                    if (segment != null)
                        max = Mathf.Max(max, segment.EndTime);
                }

                for (int i = 0; notifies != null && i < notifies.Length; i++)
                {
                    AnimNotifyPlacement notify = notifies[i];
                    if (notify != null)
                        max = Mathf.Max(max, notify.Time);
                }

                for (int i = 0; notifyStates != null && i < notifyStates.Length; i++)
                {
                    AnimNotifyStatePlacement state = notifyStates[i];
                    if (state != null)
                        max = Mathf.Max(max, state.EndTime);
                }

                for (int i = 0; customElements != null && i < customElements.Length; i++)
                {
                    CustomMontageElementPlacement element = customElements[i];
                    if (element != null)
                        max = Mathf.Max(max, element.EndTime);
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
            rateScale = Mathf.Max(0.01f, rateScale);
            blendIn = Mathf.Max(0f, blendIn);
            blendOut = Mathf.Max(0f, blendOut);

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
                    notify.Time = Mathf.Max(0f, notify.Time);
            }

            for (int i = 0; i < notifyStates?.Length; i++)
            {
                AnimNotifyStatePlacement state = notifyStates[i];
                if (state == null)
                    continue;

                state.StartTime = Mathf.Max(0f, state.StartTime);
                state.EndTime = Mathf.Max(state.StartTime, state.EndTime);
            }

            for (int i = 0; i < customTracks?.Length; i++)
            {
                CustomMontageTrack track = customTracks[i];
                if (track != null)
                    track.TrackId = track.TrackId;
            }

            for (int i = 0; i < customElements?.Length; i++)
            {
                CustomMontageElementPlacement element = customElements[i];
                if (element == null)
                    continue;

                element.TrackId = element.TrackId;
                element.StartTime = Mathf.Max(0f, element.StartTime);
                element.EndTime = Mathf.Max(element.StartTime, element.EndTime);
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
