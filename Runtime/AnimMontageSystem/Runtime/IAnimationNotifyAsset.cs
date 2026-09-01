using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum AnimationAssetType
    {
        Sequence,
        Montage
    }

    /// <summary>Animation Sequence와 Montage가 공유하는 Notify 타임라인 계약입니다.</summary>
    public interface IAnimationNotifyAsset
    {
        Object Asset { get; }
        AnimationAssetType AssetType { get; }
        float Length { get; }
        float RateScale { get; }
        IReadOnlyList<AnimNotifyPlacement> Notifies { get; }
        IReadOnlyList<AnimNotifyStatePlacement> NotifyStates { get; }
    }
}
