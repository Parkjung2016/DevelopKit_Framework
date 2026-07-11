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

    public interface IMontageRootMotionController
    {
        void ApplyMontageRootMotion(ObjectAnimMontagePlayer player, Animator animator, Vector3 deltaPosition, Quaternion deltaRotation);
    }

    public abstract class MontageRootMotionController : MonoBehaviour, IMontageRootMotionController
    {
        public abstract void ApplyMontageRootMotion(ObjectAnimMontagePlayer player, Animator animator, Vector3 deltaPosition, Quaternion deltaRotation);
    }

    [AddComponentMenu("PJDev/Framework/Object Anim Montage Player")]
    public sealed class ObjectAnimMontagePlayer : MonoBehaviour, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MontageRootMotionMode rootMotionMode = MontageRootMotionMode.Transform;
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
        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;
        public MontageRootMotionMode RootMotionMode => rootMotionMode;
        public Rigidbody RootMotionRigidbody => rootMotionRigidbody;
        public CharacterController RootMotionCharacterController => rootMotionCharacterController;
        public IMontageRootMotionController CustomRootMotionController => runtimeRootMotionController ?? customRootMotionController;
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
            if (!playback.IsPlaying || playback.IsPaused)
                return;

            playback.Advance(Time.deltaTime);
            UpdateAnimationSample();
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        private void OnAnimatorMove()
        {
            if (animator == null
                || playback.Montage == null
                || !playback.Montage.ApplyRootMotion
                || !playback.IsPlaying
                || playback.IsPaused)
            {
                return;
            }

            ApplyRootMotionDelta(animator.deltaPosition, animator.deltaRotation);
        }

        private void OnDestroy() => DestroyGraph();


        public void SetRootMotionRigidbody(Rigidbody target) => rootMotionRigidbody = target;

        public void SetRootMotionCharacterController(CharacterController target) => rootMotionCharacterController = target;

        public void SetCustomRootMotionController(IMontageRootMotionController controller) => runtimeRootMotionController = controller;

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || animator == null)
                return;

            EnsureGraph();
            ApplyAnimatorRootMotion(montage.ApplyRootMotion);
            playback.Begin(montage, Mathf.Clamp(startTime, 0f, montage.Length));
            dispatcher.Reset();
            UpdateAnimationSample(force: true);
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
            UpdateAnimationSample(force: true);
            dispatcher.ScrubTo(playback, gameObject, animator);
        }

        public bool TryHandle(AnimNotify notify, AnimNotifyContext context) => false;

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

        private void ApplyTransformRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            Transform animatorTransform = animator.transform;
            animatorTransform.position += deltaPosition;
            animatorTransform.rotation *= deltaRotation;
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

            rootMotionRigidbody.MovePosition(rootMotionRigidbody.position + deltaPosition);
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

            rootMotionCharacterController.Move(deltaPosition);
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

        private void UpdateAnimationSample(bool force = false)
        {
            if (!graph.IsValid() || playback.Montage == null)
                return;

            MontageSegmentBlending.Evaluate(playback.CurrentTime, playback.Montage.Segments, samples);
            if (samples.Count == 0)
                return;

            EnsureMixer(samples.Count, force);

            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable playable = clipPlayables[i];
                if (!playable.IsValid() || playable.GetAnimationClip() != sample.Segment.Clip)
                {
                    if (playable.IsValid())
                        playable.Destroy();

                    playable = AnimationClipPlayable.Create(graph, sample.Segment.Clip);
                    playable.SetApplyFootIK(true);
                    playable.SetApplyPlayableIK(true);
                    playable.SetSpeed(0f);
                    clipPlayables[i] = playable;
                    graph.Connect(playable, 0, mixer, i);
                }

                playable.SetTime(sample.ClipTime);
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
