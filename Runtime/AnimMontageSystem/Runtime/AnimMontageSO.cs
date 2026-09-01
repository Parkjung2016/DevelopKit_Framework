using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>애니메이션 구간, Notify, 트랙과 재생 설정을 담는 Montage 에셋입니다.</summary>
    [CreateAssetMenu(fileName = "Montage_", menuName = "PJDev/Animation/Montage")]
    public class AnimMontageSO : ScriptableObject, IAnimationNotifyAsset
    {
        [Min(0.01f)] [SerializeField] private float rateScale = 1f;
        [Min(0f)] [SerializeField] private float blendIn;
        [Min(0f)] [SerializeField] private float blendOut;
        [SerializeField] private bool applyHorizontalRootMotion = true;
        [SerializeField] private bool applyVerticalRootMotion = true;
        [SerializeField] private bool applyRotationRootMotion = true;
        [SerializeField] private MontageSegment[] segments = Array.Empty<MontageSegment>();
        [SerializeField] private AnimNotifyPlacement[] notifies = Array.Empty<AnimNotifyPlacement>();
        [SerializeField] private AnimNotifyStatePlacement[] notifyStates = Array.Empty<AnimNotifyStatePlacement>();
        [SerializeField] private MontageSlotDefinition[] slots = Array.Empty<MontageSlotDefinition>();
        [SerializeField] private string[] animationTracks = { "Default" };
        [SerializeField] private string[] notifyTracks = { "Default" };
        [SerializeField] private string[] notifyStateTracks = { "Default" };
        [SerializeField] private string[] timelineTrackOrder = Array.Empty<string>();

        [NonSerialized] private float cachedLength = -1f;

        public UnityEngine.Object Asset => this;
        public virtual AnimationAssetType AssetType => AnimationAssetType.Montage;

        /// <summary>Montage 전체에 적용되는 재생 속도 배율입니다.</summary>
        public virtual float RateScale => Mathf.Max(0.01f, rateScale);
        /// <summary>Animator 상태에서 Montage로 전환하는 시간입니다.</summary>
        public float BlendIn => Mathf.Max(0f, blendIn);
        /// <summary>Montage에서 Animator 상태로 돌아가는 시간입니다.</summary>
        public float BlendOut => Mathf.Max(0f, blendOut);
        /// <summary>위치 또는 회전 Root Motion이 하나라도 활성화되어 있는지 나타냅니다.</summary>
        public bool ApplyRootMotion => applyHorizontalRootMotion || applyVerticalRootMotion || applyRotationRootMotion;
        public virtual bool ApplyHorizontalRootMotion => applyHorizontalRootMotion;
        public virtual bool ApplyVerticalRootMotion => applyVerticalRootMotion;
        public virtual bool ApplyRotationRootMotion => applyRotationRootMotion;
        public IReadOnlyList<MontageSegment> Segments => segments ?? Array.Empty<MontageSegment>();
        public IReadOnlyList<AnimNotifyPlacement> Notifies => notifies ?? Array.Empty<AnimNotifyPlacement>();
        public IReadOnlyList<AnimNotifyStatePlacement> NotifyStates => notifyStates ?? Array.Empty<AnimNotifyStatePlacement>();
        public IReadOnlyList<MontageSlotDefinition> Slots => slots ?? Array.Empty<MontageSlotDefinition>();
        public IReadOnlyList<string> AnimationTracks => animationTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> NotifyTracks => notifyTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> NotifyStateTracks => notifyStateTracks ?? Array.Empty<string>();
        public IReadOnlyList<string> TimelineTrackOrder => timelineTrackOrder ?? Array.Empty<string>();

        /// <summary>Segment, Notify, NotifyState 중 가장 늦게 끝나는 시각입니다.</summary>
        public virtual float Length
        {
            get
            {
                if (cachedLength < 0f)
                    cachedLength = CalculateLength();
                return cachedLength;
            }
        }

        /// <summary>지정한 Montage 시간에 재생되는 애니메이션 구간을 찾습니다.</summary>
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

        /// <summary>배열 순서대로 Segment를 이어 붙이고 시작 시간을 다시 계산합니다.</summary>
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

            InvalidateLength();
        }

        private float CalculateLength()
        {
            // Length는 타임라인에서 자주 조회되므로 데이터가 바뀔 때만 캐시를 무효화합니다.
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
                if (notify == null)
                    continue;

                float endTime = notify.Time;
                if (notify.Notify is IMontageDurationNotify durationNotify)
                    endTime += Mathf.Max(0f, durationNotify.Duration);

                max = Mathf.Max(max, endTime);
            }

            for (int i = 0; notifyStates != null && i < notifyStates.Length; i++)
            {
                AnimNotifyStatePlacement state = notifyStates[i];
                if (state != null)
                    max = Mathf.Max(max, state.EndTime);
            }

            return max;
        }

        protected virtual void OnEnable() => InvalidateLength();
        protected void InvalidateLength() => cachedLength = -1f;

        protected void SetSegments(MontageSegment[] value)
        {
            segments = value ?? Array.Empty<MontageSegment>();
            InvalidateLength();
        }

        protected void ClampNotifyTimeline(float maxTime)
        {
            maxTime = Mathf.Max(0f, maxTime);
            for (int i = 0; notifies != null && i < notifies.Length; i++)
            {
                if (notifies[i] != null)
                    notifies[i].Time = Mathf.Clamp(notifies[i].Time, 0f, maxTime);
            }

            for (int i = 0; notifyStates != null && i < notifyStates.Length; i++)
            {
                AnimNotifyStatePlacement state = notifyStates[i];
                if (state == null)
                    continue;

                state.StartTime = Mathf.Clamp(state.StartTime, 0f, maxTime);
                state.EndTime = Mathf.Clamp(state.EndTime, state.StartTime, maxTime);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            rateScale = Mathf.Max(0.01f, rateScale);
            blendIn = Mathf.Max(0f, blendIn);
            blendOut = Mathf.Max(0f, blendOut);
            animationTracks = SanitizeTracks(animationTracks);
            notifyTracks = SanitizeTracks(notifyTracks);
            notifyStateTracks = SanitizeTracks(notifyStateTracks);
            timelineTrackOrder = SanitizeTrackOrder(timelineTrackOrder);

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

            InvalidateLength();
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
                if (!string.IsNullOrEmpty(tracks[i]))
                    tracks[write++] = tracks[i];
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
