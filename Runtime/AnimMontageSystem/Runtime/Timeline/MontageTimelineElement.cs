using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct MontageTimelineElementContext
    {
        public MontageTimelineElementContext(
            ObjectAnimMontagePlayer player,
            AnimMontageSO montage,
            CustomMontageElementPlacement placement,
            int placementIndex,
            float montageTime,
            float deltaTime)
        {
            Player = player;
            Montage = montage;
            Placement = placement;
            PlacementIndex = placementIndex;
            MontageTime = montageTime;
            DeltaTime = deltaTime;
        }

        public ObjectAnimMontagePlayer Player { get; }
        public AnimMontageSO Montage { get; }
        public CustomMontageElementPlacement Placement { get; }
        public int PlacementIndex { get; }
        public float MontageTime { get; }
        public float DeltaTime { get; }
        public GameObject GameObject => Player != null ? Player.gameObject : null;
        public Animator Animator => Player != null ? Player.Animator : null;

        public float NormalizedTime
        {
            get
            {
                if (Placement == null || Placement.Duration <= 0f)
                    return 0f;

                return Mathf.Clamp01((MontageTime - Placement.StartTime) / Placement.Duration);
            }
        }
    }

    public interface IMontageTimelineElementBehaviour
    {
        void OnElementEnter(MontageTimelineElementContext context);
        void OnElementUpdate(MontageTimelineElementContext context);
        void OnElementExit(MontageTimelineElementContext context);
    }

    public interface IMontageTimeScaleElement
    {
        float TimeScaleMultiplier { get; }
    }
    public interface IMontagePlaybackSpeedElement
    {
        float PlaybackSpeedMultiplier { get; }
    }

    public interface IMontageTransformOffsetElement
    {
        Vector3 PositionOffset { get; }
        Vector3 RotationOffsetEuler { get; }
        Vector3 ScaleOffset { get; }
    }

    [Serializable]
    public abstract class MontageTimelineElement
    {
        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(0.86f, 0.62f, 1f, 0.95f);
        public virtual float DefaultDuration => 0.25f;
        public virtual Type RequiredTrackType => null;

        public virtual bool CanAttachToTrack(MontageTimelineTrack track)
        {
            Type requiredTrackType = RequiredTrackType;
            return requiredTrackType == null || track != null && requiredTrackType.IsInstanceOfType(track);
        }
    }

    [Serializable]
    public abstract class MontageTimelineElement<TTrack> : MontageTimelineElement where TTrack : MontageTimelineTrack
    {
        public sealed override Type RequiredTrackType => typeof(TTrack);
    }
}