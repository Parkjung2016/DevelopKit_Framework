using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class PlaybackSpeedAnimNotifyState : AnimNotifyState, IMontagePlaybackSpeedNotifyState
    {
        public override bool CanEditTriggerOnManualPreview() => false;

        [SerializeField, Min(0f)] private float speedMultiplier = 0.5f;

        public override string DisplayName => "Playback Speed";
        public override Color EditorColor => new(0.74f, 0.95f, 0.46f, 0.95f);
        public override float DefaultDuration => 0.5f;
        public float PlaybackSpeedMultiplier => Mathf.Max(0f, speedMultiplier);
    }
}