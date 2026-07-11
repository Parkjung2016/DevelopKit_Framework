using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class TimelineControlMontageTrack : MontageTimelineTrack
    {
        public override string DisplayName => "Timeline Control";
        public override Color EditorColor => new(0.62f, 0.72f, 1f, 0.95f);
    }
}