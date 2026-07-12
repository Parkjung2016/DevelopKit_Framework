using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public abstract class MontageTimelineElement
    {
        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(0.86f, 0.62f, 1f, 0.95f);
        public virtual float DefaultDuration => 0.25f;
        public virtual float PlaybackSpeedMultiplier => 1f;
        public virtual Vector3 PositionOffset => Vector3.zero;
        public virtual Vector3 RotationOffsetEuler => Vector3.zero;
        public virtual Vector3 ScaleOffset => Vector3.zero;
    }
}