using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum MontageRootMotionMode
    {
        Transform,
        Rigidbody,
        CharacterController,
        Custom
    }

    public enum MontageRootMotionPositionSpace
    {
        Local,
        World
    }

    public interface IMontageRootMotionController
    {
        void ApplyMontageRootMotion(ObjectAnimMontagePlayer player, Animator animator, Vector3 deltaPosition,
            Quaternion deltaRotation);
    }

    public abstract class MontageRootMotionController : MonoBehaviour, IMontageRootMotionController
    {
        public abstract void ApplyMontageRootMotion(ObjectAnimMontagePlayer player, Animator animator,
            Vector3 deltaPosition, Quaternion deltaRotation);
    }

    [AddComponentMenu("PJDev/Framework/Object Anim Montage Player")]
    public sealed class ObjectAnimMontagePlayer : MonoBehaviour, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MontageRootMotionMode rootMotionMode = MontageRootMotionMode.Transform;

        [SerializeField]
        private MontageRootMotionPositionSpace rootMotionPositionSpace = MontageRootMotionPositionSpace.World;

        [SerializeField] private Rigidbody rootMotionRigidbody;
        [SerializeField] private CharacterController rootMotionCharacterController;
        [SerializeField] private MontageRootMotionController customRootMotionController;
        private readonly MontagePlaybackState playback = new();
        private readonly MontageNotifyDispatcher dispatcher = new();
        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private int mixerInputCount;
        private bool cachedAnimatorRootMotion;
        private bool hasCachedAnimatorRootMotion;
        private IMontageRootMotionController runtimeRootMotionController;
        private bool rootMotionActiveThisFrame;
        private bool suppressNextRootMotion;
        private bool mixerRebuiltThisSample;
        private float animationSampleTime;

        private MontageTimelineElementEvaluation previousTimelineElementEvaluation =
            MontageTimelineElementEvaluation.Default;

        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;
        public MontageRootMotionMode RootMotionMode => rootMotionMode;
        public MontageRootMotionPositionSpace RootMotionPositionSpace => rootMotionPositionSpace;
        public Rigidbody RootMotionRigidbody => rootMotionRigidbody;
        public CharacterController RootMotionCharacterController => rootMotionCharacterController;

        public IMontageRootMotionController CustomRootMotionController =>
            runtimeRootMotionController ?? customRootMotionController;

        public event Action<AnimNotify, AnimNotifyContext> NotifyFired;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheRootMotionComponents();
            dispatcher.NotifyFired += (notify, ctx) => NotifyFired?.Invoke(notify, ctx);
        }

        private void Update()
        {
            rootMotionActiveThisFrame = false;
            if (!playback.IsPlaying || playback.IsPaused)
                return;

            float deltaTime = Time.deltaTime;
            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            MontageTimelineElementEvaluation timelineEvaluation =
                MontageTimelineElementEvaluator.Evaluate(montage, currentTime);
            float timelineSpeed = timelineEvaluation.SpeedMultiplier;
            float animationDeltaTime = deltaTime * timelineSpeed;
            rootMotionActiveThisFrame = montage != null && montage.ApplyRootMotion;

            if (rootMotionActiveThisFrame)
            {
                animationSampleTime = currentTime;
                UpdateAnimationSample(false, animationSampleTime);
                if (mixerRebuiltThisSample)
                    SyncRootMotionGraphAfterRebuild();

                EvaluateGraph(animationDeltaTime);
                playback.Advance(deltaTime * timelineSpeed);
                animationSampleTime = playback.CurrentTime;
            }
            else
            {
                playback.Advance(deltaTime * timelineSpeed);
                animationSampleTime = playback.CurrentTime;
                UpdateAnimationSample(false, animationSampleTime);
                EvaluateGraph(0f);
            }

            ApplyTimelineElementTransform(timelineEvaluation);
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        private void OnAnimatorMove()
        {
            if (animator == null
                || playback.Montage == null
                || !playback.Montage.ApplyRootMotion
                || (!playback.IsPlaying && !rootMotionActiveThisFrame)
                || playback.IsPaused)
            {
                return;
            }

            if (suppressNextRootMotion)
            {
                suppressNextRootMotion = false;
                return;
            }

            ApplyRootMotionDelta(animator.deltaPosition, animator.deltaRotation);
        }

        private void OnDestroy() => DestroyGraph();


        public void SetRootMotionRigidbody(Rigidbody target) => rootMotionRigidbody = target;

        public void SetRootMotionCharacterController(CharacterController target) =>
            rootMotionCharacterController = target;

        public void SetCustomRootMotionController(IMontageRootMotionController controller) =>
            runtimeRootMotionController = controller;

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || animator == null)
                return;

            EnsureGraph();
            ApplyAnimatorRootMotion(montage.ApplyRootMotion);
            playback.Begin(montage, Mathf.Clamp(startTime, 0f, montage.Length));
            animationSampleTime = playback.CurrentTime;
            suppressNextRootMotion = montage.ApplyRootMotion;
            dispatcher.Reset();
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        public void Stop()
        {
            dispatcher.EndActiveStates(gameObject, animator, playback.Montage, playback.CurrentTime);
            playback.Stop();
            DestroyGraph();
            RestoreAnimatorRootMotion();
        }

        public void Pause(bool paused = true)
        {
            if (paused)
                dispatcher.EndActiveStates(gameObject, animator, playback.Montage, playback.CurrentTime);

            playback.Pause(paused);
        }

        public void SetTime(float montageTime)
        {
            playback.SetTime(montageTime);
            suppressNextRootMotion = playback.Montage != null && playback.Montage.ApplyRootMotion;
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            dispatcher.ScrubTo(playback, gameObject, animator);
        }

        public bool TryHandle(AnimNotify notify, AnimNotifyContext context) => false;

        private void EnsureGraph()
        {
            if (graph.IsValid())
                return;

            graph = PlayableGraph.Create($"{name}.AnimMontage");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            graph.Play();
        }

        private void EvaluateGraph(float deltaTime)
        {
            if (!graph.IsValid())
                return;

            graph.Evaluate(playback.Montage != null && playback.Montage.ApplyRootMotion ? deltaTime : 0f);
        }

        private void SyncRootMotionGraphAfterRebuild()
        {
            if (!graph.IsValid())
                return;

            suppressNextRootMotion = true;
            graph.Evaluate(0f);
            suppressNextRootMotion = false;
        }

        private void DestroyGraph()
        {
            if (!graph.IsValid())
                return;

            graph.Destroy();
            samples.Clear();
            clipPlayables.Clear();
            mixer = default;
            mixerInputCount = 0;
            RestoreAnimatorRootMotion();
        }

        private void CacheRootMotionComponents()
        {
            if (rootMotionRigidbody == null)
                rootMotionRigidbody = GetComponentInParent<Rigidbody>();

            if (rootMotionCharacterController == null)
                rootMotionCharacterController = GetComponentInParent<CharacterController>();
        }

        private void ApplyTimelineElementTransform(MontageTimelineElementEvaluation evaluation)
        {
            Vector3 positionDelta = evaluation.PositionOffset - previousTimelineElementEvaluation.PositionOffset;
            Quaternion rotationDelta = Quaternion.Inverse(previousTimelineElementEvaluation.RotationOffset) *
                                       evaluation.RotationOffset;
            Vector3 scaleDelta = evaluation.ScaleOffset - previousTimelineElementEvaluation.ScaleOffset;

            if (positionDelta.sqrMagnitude > 0.0000001f)
                transform.position += transform.rotation * positionDelta;

            if (Quaternion.Angle(Quaternion.identity, rotationDelta) > 0.0001f)
                transform.rotation *= rotationDelta;

            if (scaleDelta.sqrMagnitude > 0.0000001f)
                transform.localScale += scaleDelta;

            previousTimelineElementEvaluation = evaluation;
        }

        private void ApplyRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            switch (rootMotionMode)
            {
                case MontageRootMotionMode.Rigidbody:
                    ApplyRigidbodyRootMotion(deltaPosition, deltaRotation);
                    break;
                case MontageRootMotionMode.CharacterController:
                    ApplyCharacterControllerRootMotion(deltaPosition, deltaRotation);
                    break;
                case MontageRootMotionMode.Custom:
                    ApplyCustomRootMotion(deltaPosition, deltaRotation);
                    break;
                default:
                    ApplyTransformRootMotion(deltaPosition, deltaRotation);
                    break;
            }
        }


        private Vector3 ConvertRootMotionPosition(Vector3 deltaPosition, Quaternion targetRotation) => deltaPosition;

        private void ApplyTransformRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            transform.position += ConvertRootMotionPosition(deltaPosition, transform.rotation);
            transform.rotation *= deltaRotation;
        }

        private void ApplyRigidbodyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (rootMotionRigidbody == null)
                CacheRootMotionComponents();

            if (rootMotionRigidbody == null)
            {
                ApplyTransformRootMotion(deltaPosition, deltaRotation);
                return;
            }

            rootMotionRigidbody.MovePosition(rootMotionRigidbody.position +
                                             ConvertRootMotionPosition(deltaPosition, rootMotionRigidbody.rotation));
            rootMotionRigidbody.MoveRotation(rootMotionRigidbody.rotation * deltaRotation);
        }

        private void ApplyCharacterControllerRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (rootMotionCharacterController == null)
                CacheRootMotionComponents();

            if (rootMotionCharacterController == null)
            {
                ApplyTransformRootMotion(deltaPosition, deltaRotation);
                return;
            }

            rootMotionCharacterController.Move(ConvertRootMotionPosition(deltaPosition,
                rootMotionCharacterController.transform.rotation));
            rootMotionCharacterController.transform.rotation *= deltaRotation;
        }

        private void ApplyCustomRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            IMontageRootMotionController controller = CustomRootMotionController;
            if (controller == null)
            {
                ApplyTransformRootMotion(deltaPosition, deltaRotation);
                return;
            }

            controller.ApplyMontageRootMotion(this, animator, deltaPosition, deltaRotation);
        }

        private void ApplyAnimatorRootMotion(bool applyRootMotion)
        {
            if (animator == null)
                return;

            if (!hasCachedAnimatorRootMotion)
            {
                cachedAnimatorRootMotion = animator.applyRootMotion;
                hasCachedAnimatorRootMotion = true;
            }

            animator.applyRootMotion = applyRootMotion;
        }

        private void RestoreAnimatorRootMotion()
        {
            if (animator != null && hasCachedAnimatorRootMotion)
                animator.applyRootMotion = cachedAnimatorRootMotion;

            hasCachedAnimatorRootMotion = false;
        }

        private void UpdateAnimationSample(bool force = false) =>
            UpdateAnimationSample(force, playback.CurrentTime);

        private void UpdateAnimationSample(bool force, float sampleTime)
        {
            mixerRebuiltThisSample = false;
            if (!graph.IsValid() || playback.Montage == null)
                return;

            MontageSegmentBlending.Evaluate(sampleTime, playback.Montage.Segments, samples);
            if (samples.Count == 0)
                return;

            EnsureMixer(samples.Count, force);

            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable playable = clipPlayables[i];
                bool resetPlayableTime = force;
                if (!playable.IsValid() || playable.GetAnimationClip() != sample.Segment.Clip)
                {
                    if (playable.IsValid())
                        playable.Destroy();

                    playable = AnimationClipPlayable.Create(graph, sample.Segment.Clip);
                    playable.SetApplyFootIK(true);
                    playable.SetApplyPlayableIK(true);
                    playable.SetDuration(sample.Segment.IsLoopingClip
                        ? Mathf.Max(sample.Segment.ClipEndTime, sample.Segment.Clip.length)
                        : sample.Segment.Clip.length);
                    clipPlayables[i] = playable;
                    graph.Connect(playable, 0, mixer, i);
                    resetPlayableTime = true;
                }

                bool applyRootMotion = playback.Montage.ApplyRootMotion;
                float playableSpeed = applyRootMotion ? sample.Segment.PlayRate * playback.Montage.RateScale : 0f;
                if (!applyRootMotion)
                {
                    playable.SetTime(sample.ClipTime);
                }
                else if (resetPlayableTime)
                {
                    playable.SetTime(Mathf.Max(sample.Segment.ClipStartTime, sample.PlayableClipTime));
                }

                playable.SetSpeed(playableSpeed);
                mixer.SetInputWeight(i, sample.Weight);
            }

            for (int i = samples.Count; i < mixerInputCount; i++)
                mixer.SetInputWeight(i, 0f);
        }


        private void EnsureMixer(int inputCount, bool force)
        {
            int required = Mathf.Max(1, inputCount);
            if (!force && mixer.IsValid() && mixerInputCount == required)
                return;

            mixerRebuiltThisSample = true;

            for (int i = 0; i < clipPlayables.Count; i++)
            {
                AnimationClipPlayable playable = clipPlayables[i];
                if (playable.IsValid())
                    playable.Destroy();
            }

            if (mixer.IsValid())
                mixer.Destroy();

            mixer = AnimationMixerPlayable.Create(graph, required);
            mixerInputCount = required;
            output.SetSourcePlayable(mixer);
            clipPlayables.Clear();
            for (int i = 0; i < required; i++)
                clipPlayables.Add(default);
        }
    }
}



