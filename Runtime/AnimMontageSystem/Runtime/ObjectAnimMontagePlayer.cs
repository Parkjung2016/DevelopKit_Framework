using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Anim Montage Player")]
    public sealed class ObjectAnimMontagePlayer : MonoBehaviour, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool playOnAwake;
        [SerializeField] private AnimMontageSO defaultMontage;

        private readonly MontagePlaybackState playback = new();
        private readonly MontageNotifyDispatcher dispatcher = new();
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable clipPlayable;
        private int activeSegmentIndex = -1;

        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;

        public event Action<AnimNotifySO, AnimNotifyContext> NotifyFired;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            dispatcher.NotifyFired += (notify, ctx) => NotifyFired?.Invoke(notify, ctx);

            if (playOnAwake && defaultMontage != null)
                Play(defaultMontage);
        }

        private void Update()
        {
            if (!playback.IsPlaying || playback.IsPaused)
                return;

            playback.Advance(Time.deltaTime);
            UpdateAnimationSample();
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        private void OnDestroy() => DestroyGraph();

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || animator == null)
                return;

            EnsureGraph();
            playback.Begin(montage, Mathf.Clamp(startTime, 0f, montage.Length));
            dispatcher.Reset();
            UpdateAnimationSample(force: true);
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        public void Stop()
        {
            playback.Stop();
            activeSegmentIndex = -1;
            DestroyGraph();
        }

        public void Pause(bool paused = true) => playback.Pause(paused);

        public void SetTime(float montageTime)
        {
            playback.SetTime(montageTime);
            UpdateAnimationSample(force: true);
            dispatcher.ScrubTo(playback, gameObject, animator);
        }

        public bool TryHandle(AnimNotifySO notify, AnimNotifyContext context) => false;

        private void EnsureGraph()
        {
            if (graph.IsValid())
                return;

            graph = PlayableGraph.Create($"{name}.AnimMontage");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            graph.Play();
        }

        private void DestroyGraph()
        {
            if (!graph.IsValid())
                return;

            graph.Destroy();
            activeSegmentIndex = -1;
        }

        private void UpdateAnimationSample(bool force = false)
        {
            if (!graph.IsValid() || playback.Montage == null)
                return;

            if (!playback.Montage.TryGetSegmentAtTime(playback.CurrentTime, out MontageSegment segment, out int segmentIndex))
                return;

            AnimationClip clip = segment.Clip;
            if (clip == null)
                return;

            if (force || segmentIndex != activeSegmentIndex)
            {
                activeSegmentIndex = segmentIndex;
                clipPlayable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(clipPlayable);
                clipPlayable.SetSpeed(segment.PlayRate * playback.Montage.RateScale);
            }

            clipPlayable.SetTime(segment.ToClipTime(playback.CurrentTime));
        }
    }
}
