using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public abstract class MontageTimelineTrack
    {
        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(0.72f, 0.46f, 1f, 0.95f);
    }
}
