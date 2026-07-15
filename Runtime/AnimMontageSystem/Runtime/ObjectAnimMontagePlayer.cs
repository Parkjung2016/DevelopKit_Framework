using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Anim Montage Player")]
    public sealed class ObjectAnimMontagePlayer : MonoBehaviour, IAnimMontagePlayer, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MontageRootMotionMode rootMotionMode = MontageRootMotionMode.Transform;

        [SerializeField] private Rigidbody rootMotionRigidbody;
        [SerializeField] private CharacterController rootMotionCharacterController;
        [SerializeField] private InterfaceReference<IMontageRootMotionController> customRootMotionController;
        private readonly MontagePlaybackState playback = new();
        private readonly MontageNotifyDispatcher dispatcher = new();
        private readonly MontageRootMotionRuntimeSampler rootMotionSampler = new();
        private readonly MontageBlendController blendController = new();

        private MontagePlayableGraph playableGraph;
        private MontageRootMotionDriver rootMotionDriver;
        private MontageTransformDriver transformDriver;
        private bool notifyForwardingBound;
        private bool rootMotionActiveThisFrame;
        private float animationSampleTime;


        public Animator Animator => animator;
        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public float Duration => playback.Duration;
        public float CurrentLength => Duration;
        public float NormalizedTime => playback.NormalizedTime;
        public float AnimationSampleTime => animationSampleTime;

        public float AnimationSampleNormalizedTime =>
            CurrentLength > 0f ? Mathf.Clamp01(animationSampleTime / CurrentLength) : 0f;

        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;
        public MontageRootMotionMode RootMotionMode => rootMotionMode;
        public Rigidbody RootMotionRigidbody => rootMotionRigidbody;
        public CharacterController RootMotionCharacterController => rootMotionCharacterController;

        public IMontageRootMotionController CustomRootMotionController => customRootMotionController.Value;

        /// <summary>Notify가 실행될 때 호출됩니다.</summary>
        public event Action<AnimNotify, AnimNotifyContext> OnNotify;

        /// <summary>재생 시작, 완료, 중지, 교체 이벤트를 한 곳에서 받을 때 사용합니다.</summary>
        public event Action<MontagePlaybackEventContext> OnPlaybackEvent;

        /// <summary>Montage 재생이 시작될 때 호출됩니다.</summary>
        public event Action<MontagePlaybackEventContext> OnPlay;

        /// <summary>Montage가 끝까지 재생되면 호출됩니다.</summary>
        public event Action<MontagePlaybackEventContext> OnComplete;

        /// <summary>Stop으로 재생을 중지하면 호출됩니다.</summary>
        public event Action<MontagePlaybackEventContext> OnStop;

        /// <summary>재생 중인 Montage를 다른 Montage로 교체하면 호출됩니다.</summary>
        public event Action<MontagePlaybackEventContext> OnInterrupted;
        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            if (!EnsureInitialized())
                return;

            EnsureGraph();
            EvaluateGraph(0f);
        }

        private bool EnsureInitialized()
        {
            transformDriver ??= new MontageTransformDriver(transform);

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
                return false;

            playableGraph ??= new MontagePlayableGraph(animator);
            playableGraph.Bind(animator);
            SyncRootMotionDriver();
            if (!notifyForwardingBound)
            {
                dispatcher.OnNotify += ForwardNotify;
                notifyForwardingBound = true;
            }

            return true;
        }

        private void ForwardNotify(AnimNotify notify, AnimNotifyContext context) =>
            OnNotify?.Invoke(notify, context);

        private void Update()
        {
            rootMotionActiveThisFrame = false;
            if (!playback.IsPlaying || playback.IsPaused)
            {
                bool wasBlendingOut = blendController.IsFadingOut;
                UpdateBlendOut(Time.unscaledDeltaTime);
                if (!wasBlendingOut && !playback.IsPaused && playableGraph is { IsValid: true })
                    EvaluateGraph(Time.unscaledDeltaTime);
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            bool wasPlayingBeforeAdvance = playback.IsPlaying;
            MontageNotifyEvaluation timelineEvaluation =
                MontageNotifyEvaluator.Evaluate(montage, currentTime);
            float timelineSpeed = timelineEvaluation.SpeedMultiplier;
            float timeScale = timelineEvaluation.TimeScaleMultiplier;
            blendController.AdvancePlayback(deltaTime);
            float montageLayerWeight = ComputeMontageLayerWeight(montage, currentTime);
            SetMontageLayerWeight(montageLayerWeight);
            float animationDeltaTime = deltaTime * timelineSpeed * timeScale;
            rootMotionActiveThisFrame = UsesRootMotion(montage);

            if (rootMotionActiveThisFrame)
            {
                float previousAnimationSampleTime = animationSampleTime;
                if (UpdateAnimationSample(false, previousAnimationSampleTime))
                    playableGraph.ResampleAfterMixerRebuild();
                EvaluateGraph(animationDeltaTime);
                if (rootMotionSampler.TryEvaluateStep(animator, montage, previousAnimationSampleTime, animationDeltaTime,
                        transform.rotation, out Vector3 rootDeltaPosition, out Quaternion rootDeltaRotation))
                {
                    ApplyRootMotionDelta(rootDeltaPosition, rootDeltaRotation, montageLayerWeight);
                }
                animationSampleTime = Mathf.Min(animationSampleTime + animationDeltaTime * montage.RateScale,
                    playback.Duration);
                playback.Advance(deltaTime * timelineSpeed);
            }
            else
            {
                animationSampleTime = Mathf.Min(animationSampleTime + animationDeltaTime * montage.RateScale,
                    playback.Duration);
                playback.Advance(deltaTime * timelineSpeed);
                UpdateAnimationSample(false, animationSampleTime);
                EvaluateGraph(deltaTime);
            }

            if (!playback.IsPlaying)
                timelineEvaluation = MontageNotifyEvaluator.Evaluate(montage, playback.CurrentTime);

            ApplyNotifyTransform(timelineEvaluation);
            if (!playback.IsPlaying)
                transformDriver?.Reset();

            dispatcher.Dispatch(playback, gameObject, animator, this);

            if (wasPlayingBeforeAdvance && !playback.IsPlaying)
                BeginBlendOut(montage, currentTime, playback.CurrentTime,
                    ComputeMontageLayerWeight(montage, playback.CurrentTime),
                    MontagePlaybackEventType.Complete);
        }


        private void BeginBlendOut(
            AnimMontageSO montage,
            float previousTime,
            float currentTime,
            float startWeight,
            MontagePlaybackEventType completionEventType)
        {
            dispatcher.Dispatch(playback, gameObject, animator, this);
            if (montage == null || montage.BlendOut <= 0f || playableGraph is not { IsValid: true })
            {
                FinishMontageLayer();
                EmitPlaybackEvent(completionEventType, montage, previousTime, currentTime);
                return;
            }

            blendController.BeginFadeOut(montage, currentTime, startWeight);
            SetMontageLayerWeight(startWeight);
        }

        private void UpdateBlendOut(float deltaTime)
        {
            if (!blendController.IsFadingOut || playback.IsPaused)
                return;

            if (playableGraph is not { IsValid: true })
            {
                blendController.ClearFadeOut();
                return;
            }

            AnimMontageSO fadingMontage = blendController.FadingOutMontage;
            float fadingMontageTime = blendController.FadeOutMontageTime;
            float weight = blendController.AdvanceFadeOut(deltaTime, out bool completed);
            SetMontageLayerWeight(weight);

            rootMotionActiveThisFrame = UsesRootMotion(fadingMontage);
            EvaluateGraph(deltaTime);
            if (!completed)
                return;

            FinishMontageLayer();
            EmitPlaybackEvent(
                MontagePlaybackEventType.Complete,
                fadingMontage,
                fadingMontageTime,
                fadingMontageTime);
        }
        private void OnAnimatorMove()
        {
            if (animator == null || !animator.applyRootMotion)
                return;

            bool montageOwnsRootMotion = rootMotionActiveThisFrame
                || ((playback.IsPlaying || playback.IsPaused) && UsesRootMotion(playback.Montage))
                || (blendController.IsFadingOut && UsesRootMotion(blendController.FadingOutMontage));
            if (montageOwnsRootMotion)
                return;

            rootMotionDriver?.ApplyAnimatorDelta();
        }

        private void LateUpdate()
        {
            if (playableGraph is not { IsValid: true } || playback.Montage == null || (!playback.IsPlaying && !playback.IsPaused))
                return;
            EvaluateGraph(0f);
        }

        private void OnDestroy()
        {
            DestroyGraph();
            rootMotionSampler.Dispose();
        }

        public void SetRootMotionRigidbody(Rigidbody target)
        {
            rootMotionRigidbody = target;
            SyncRootMotionDriver();
        }

        public void SetRootMotionCharacterController(CharacterController target)
        {
            rootMotionCharacterController = target;
            SyncRootMotionDriver();
        }

        public void SetCustomRootMotionController(IMontageRootMotionController controller)
        {
            customRootMotionController.Value = controller;
            SyncRootMotionDriver();
        }

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || !EnsureInitialized())
                return;

            AnimMontageSO previousMontage = blendController.IsFadingOut ? blendController.FadingOutMontage : playback.Montage;
            float previousTime = blendController.IsFadingOut ? blendController.FadeOutMontageTime : playback.CurrentTime;
            bool wasPlaying = playback.IsPlaying || blendController.IsFadingOut;
            if (wasPlaying && previousMontage != null)
            {
                dispatcher.EndActiveStates(gameObject, animator, previousMontage, previousTime);
                EmitPlaybackEvent(MontagePlaybackEventType.Interrupted, previousMontage, previousTime, previousTime);
            }

            blendController.ClearFadeOut();
            transformDriver?.Reset();
            EnsureGraph();
            playback.Begin(montage, startTime);
            if (UsesRootMotion(montage))
                rootMotionSampler.Reset(animator);
            blendController.BeginPlayback();
            SetMontageLayerWeight(ComputeMontageLayerWeight(montage, playback.CurrentTime));
            animationSampleTime = playback.CurrentTime;
            dispatcher.Reset();
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            ApplyNotifyTransform(MontageNotifyEvaluator.Evaluate(montage, playback.CurrentTime));
            EmitPlaybackEvent(MontagePlaybackEventType.Play, montage, previousTime, playback.CurrentTime);
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }

        public void Stop()
        {
            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            bool hadPlayback = montage != null && (playback.IsPlaying || playback.IsPaused || blendController.IsFadingOut);

            dispatcher.EndActiveStates(gameObject, animator, montage, currentTime);
            playback.Stop();
            transformDriver?.Reset();
            FinishMontageLayer();

            if (hadPlayback)
                EmitPlaybackEvent(MontagePlaybackEventType.Stop, montage, currentTime, currentTime);
        }

        public void Pause(bool paused = true)
        {
            if (playback.Montage == null || playback.IsPaused == paused)
                return;

            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            if (paused)
            {
                dispatcher.EndActiveStates(gameObject, animator, montage, currentTime);
            }

            playback.Pause(paused);
        }

        public void SetTime(float montageTime)
        {
            playback.SetTime(montageTime);
            if (UsesRootMotion(playback.Montage))
                rootMotionSampler.Reset(animator);
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            ApplyNotifyTransform(
                MontageNotifyEvaluator.Evaluate(playback.Montage, playback.CurrentTime));
            dispatcher.ScrubTo(playback, gameObject, animator);
        }

        public bool TryHandle(AnimNotify notify, AnimNotifyContext context) => false;

        private void EmitPlaybackEvent(
            MontagePlaybackEventType eventType,
            AnimMontageSO montage,
            float previousTime,
            float currentTime)
        {
            var context = new MontagePlaybackEventContext(this, MontageRuntimeInfo.FromMontage(montage), eventType,
                previousTime, currentTime);
            OnPlaybackEvent?.Invoke(context);

            switch (eventType)
            {
                case MontagePlaybackEventType.Play:
                    OnPlay?.Invoke(context);
                    break;
                case MontagePlaybackEventType.Complete:
                    OnComplete?.Invoke(context);
                    break;
                case MontagePlaybackEventType.Stop:
                    OnStop?.Invoke(context);
                    break;
                case MontagePlaybackEventType.Interrupted:
                    OnInterrupted?.Invoke(context);
                    break;
            }
        }

        private void EnsureGraph()
        {
            playableGraph ??= new MontagePlayableGraph(animator);
            playableGraph.Bind(animator);
            playableGraph.Ensure();
        }
        private void EvaluateGraph(float deltaTime)
        {
            playableGraph?.Evaluate(deltaTime);
        }

        private void FinishMontageLayer()
        {
            SetMontageLayerWeight(0f);
            if (playableGraph is { IsValid: true })
                EvaluateGraph(0f);

            blendController.ClearFadeOut();
            rootMotionSampler.Stop();
        }

        private void DestroyGraph()
        {
            playableGraph?.Dispose();
            blendController.ClearFadeOut();
            rootMotionSampler.Stop();
        }
        private void SyncRootMotionDriver()
        {
            if (animator == null)
                return;

            rootMotionDriver ??= new MontageRootMotionDriver(this, animator);
            rootMotionDriver.BindAnimator(animator);
            rootMotionDriver.Configure(
                rootMotionMode,
                rootMotionRigidbody,
                rootMotionCharacterController,
                CustomRootMotionController);
            rootMotionDriver.FindMissingTargets();
            rootMotionRigidbody = rootMotionDriver.Rigidbody;
            rootMotionCharacterController = rootMotionDriver.CharacterController;
        }

        private void ApplyNotifyTransform(MontageNotifyEvaluation evaluation) =>
            transformDriver?.Apply(evaluation);

        private static bool UsesRootMotion(AnimMontageSO montage) =>
            MontageRootMotionUtility.IsEnabled(montage);
        private void ApplyRootMotionDelta(
            Vector3 deltaPosition,
            Quaternion deltaRotation,
            float montageLayerWeight)
        {
            rootMotionDriver?.Apply(
                playback.Montage ?? blendController.FadingOutMontage,
                deltaPosition,
                deltaRotation,
                montageLayerWeight);
        }

        private bool UpdateAnimationSample(bool force = false) =>
            UpdateAnimationSample(force, playback.CurrentTime);

        private bool UpdateAnimationSample(bool force, float sampleTime)
        {
            return playableGraph != null
                   && playableGraph.Sample(playback.Montage, sampleTime, force);
        }

        private float ComputeMontageLayerWeight(AnimMontageSO montage, float montageTime) =>
            blendController.GetPlaybackWeight(montage, montageTime, playback.Duration);
        private void SetMontageLayerWeight(float weight)
        {
            playableGraph?.SetMontageWeight(weight);
        }
    }
}