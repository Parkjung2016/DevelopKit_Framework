using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// 몽타주 재생 상태가 바뀐 이유입니다.
    /// </summary>
    public enum MontagePlaybackEventType
    {
        Play,
        Complete,
        Stop,
        Interrupted
    }

    /// <summary>
    /// 런타임에서 외부로 넘겨도 되는 몽타주 정보입니다.
    /// </summary>
    public sealed class MontageRuntimeInfo
    {
        public MontageRuntimeInfo(
            string name,
            float length,
            float rateScale,
            float blendIn,
            float blendOut,
            bool applyRootMotion,
            bool applyHorizontalRootMotion,
            bool applyVerticalRootMotion,
            bool applyRotationRootMotion)
        {
            Name = name;
            Length = length;
            RateScale = rateScale;
            BlendIn = blendIn;
            BlendOut = blendOut;
            ApplyRootMotion = applyRootMotion;
            ApplyHorizontalRootMotion = applyHorizontalRootMotion;
            ApplyVerticalRootMotion = applyVerticalRootMotion;
            ApplyRotationRootMotion = applyRotationRootMotion;
        }

        /// <summary>몽타주 이름입니다.</summary>
        public string Name { get; }

        /// <summary>몽타주의 전체 길이입니다.</summary>
        public float Length { get; }

        /// <summary>몽타주 재생 배속입니다.</summary>
        public float RateScale { get; }

        /// <summary>AnimatorController에서 몽타주로 넘어오는 시간입니다.</summary>
        public float BlendIn { get; }

        /// <summary>몽타주에서 AnimatorController로 돌아가는 시간입니다.</summary>
        public float BlendOut { get; }

        /// <summary>루트 모션을 사용하는지 나타냅니다.</summary>
        public bool ApplyRootMotion { get; }

        /// <summary>XZ 평면 이동 루트 모션을 적용하는지 나타냅니다.</summary>
        public bool ApplyHorizontalRootMotion { get; }

        /// <summary>Y축 이동 루트 모션을 적용하는지 나타냅니다.</summary>
        public bool ApplyVerticalRootMotion { get; }

        /// <summary>회전 루트 모션을 적용하는지 나타냅니다.</summary>
        public bool ApplyRotationRootMotion { get; }

        internal static MontageRuntimeInfo FromMontage(AnimMontageSO montage)
        {
            return montage != null
                ? new MontageRuntimeInfo(
                    montage.name,
                    montage.Length,
                    montage.RateScale,
                    montage.BlendIn,
                    montage.BlendOut,
                    montage.ApplyRootMotion,
                    montage.ApplyHorizontalRootMotion,
                    montage.ApplyVerticalRootMotion,
                    montage.ApplyRotationRootMotion)
                : null;
        }
    }

    /// <summary>
    /// 몽타주 재생 이벤트와 함께 전달되는 정보입니다.
    /// </summary>
    public readonly struct MontagePlaybackEventContext
    {
        public MontagePlaybackEventContext(
            ObjectAnimMontagePlayer player,
            MontageRuntimeInfo runtimeInfo,
            MontagePlaybackEventType eventType,
            float previousTime,
            float currentTime)
        {
            Player = player;
            RuntimeInfo = runtimeInfo;
            EventType = eventType;
            PreviousTime = previousTime;
            CurrentTime = currentTime;
        }

        /// <summary>이벤트를 보낸 플레이어입니다.</summary>
        public ObjectAnimMontagePlayer Player { get; }

        /// <summary>이벤트가 발생한 몽타주의 런타임 정보입니다.</summary>
        public MontageRuntimeInfo RuntimeInfo { get; }

        /// <summary>발생한 재생 이벤트 타입입니다.</summary>
        public MontagePlaybackEventType EventType { get; }

        /// <summary>이벤트 직전의 재생 시간입니다.</summary>
        public float PreviousTime { get; }

        /// <summary>이벤트가 발생한 시점의 재생 시간입니다.</summary>
        public float CurrentTime { get; }

        /// <summary>몽타주의 전체 길이입니다. 몽타주 정보가 없으면 0입니다.</summary>
        public float Length => RuntimeInfo != null ? RuntimeInfo.Length : 0f;

        /// <summary>현재 재생 위치를 0~1 범위로 나타냅니다.</summary>
        public float NormalizedTime => Length > 0f ? Mathf.Clamp01(CurrentTime / Length) : 0f;
    }
}
