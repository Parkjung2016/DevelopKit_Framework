using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct AnimNotifyContext
    {
        public AnimNotifyContext(
            GameObject owner,
            Animator animator,
            IAnimationNotifyAsset animationAsset,
            float animationTime,
            float deltaTime)
        {
            Owner = owner;
            Animator = animator;
            AnimationAsset = animationAsset;
            AnimationTime = animationTime;
            DeltaTime = deltaTime;
        }

        public GameObject Owner { get; }
        public Animator Animator { get; }
        public IAnimationNotifyAsset AnimationAsset { get; }
        public Object Asset => AnimationAsset?.Asset;
        public AnimMontageSO Montage => AnimationAsset?.AssetType == AnimationAssetType.Montage
            ? AnimationAsset as AnimMontageSO
            : null;
        public AnimSequenceSO Sequence => AnimationAsset as AnimSequenceSO;
        public float AnimationTime { get; }

        public float DeltaTime { get; }
    }
}