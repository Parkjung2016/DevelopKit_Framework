using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>Animator State에서 재생할 단일 AnimationClip과 Notify 타임라인을 담습니다.</summary>
    [CreateAssetMenu(fileName = "Sequence_", menuName = "PJDev/Animation/Animation Sequence")]
    public sealed class AnimSequenceSO : AnimMontageSO
    {
        [SerializeField] private AnimationClip clip;

        public override AnimationAssetType AssetType => AnimationAssetType.Sequence;
        public AnimationClip Clip => clip;
        public override float RateScale => 1f;
        public override bool ApplyHorizontalRootMotion => true;
        public override bool ApplyVerticalRootMotion => true;
        public override bool ApplyRotationRootMotion => true;
        public override float Length => clip != null ? clip.length : 0f;

        /// <summary>Sequence가 참조할 Clip을 바꾸고 프리뷰용 단일 Segment를 갱신합니다.</summary>
        public void SetClip(AnimationClip value)
        {
            if (clip == value)
                return;

            clip = value;
            SyncSequenceSegment();
            ClampNotifyTimeline(Length);
            InvalidateLength();
        }

        protected override void OnEnable()
        {
            SyncSequenceSegment();
            base.OnEnable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            SyncSequenceSegment();
            ClampNotifyTimeline(Length);
            base.OnValidate();
        }
#endif

        private void SyncSequenceSegment()
        {
            SetSegments(clip != null
                ? new[] { MontageSegment.CreateSequenceSegment(clip) }
                : System.Array.Empty<MontageSegment>());
        }
    }
}
