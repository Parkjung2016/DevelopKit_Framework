using System;
using PJDev.DevelopKit.BasicTemplate.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Anim Montage Player")]
    public sealed class ObjectAnimMontagePlayer : MonoBehaviour, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MontageRootMotionMode rootMotionMode = MontageRootMotionMode.Transform;

        [SerializeField] private Rigidbody rootMotionRigidbody;
        [SerializeField] private CharacterController rootMotionCharacterController;
        [SerializeField] private InterfaceReference<IMontageRootMotionController> customRootMotionController;
        private readonly MontagePlaybackState playback = new();
        private readonly MontageNotifyDispatcher dispatcher = new();
        private readonly MontageRootMotionRuntimeSampler rootMotionSampler = new();

        private MontagePlayableGraph playableGraph;
        private bool notifyForwardingBound;
        private bool rootMotionActiveThisFrame;
        private bool isBlendingOut;
        private AnimMontageSO blendOutMontage;
        private float blendOutElapsedTime;
        private float blendOutDuration;
        private float blendOutStartWeight;
        private float blendOutStartTime;
        private float animationSampleTime;
        private float montageBlendElapsedTime;

        private MontageNotifyEvaluation previousNotifyEvaluation =
            MontageNotifyEvaluation.Default;

        public Animator Animator => animator;
        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public float CurrentLength => playback.Montage != null ? playback.Montage.Length : 0f;
        public float NormalizedTime => CurrentLength > 0f ? Mathf.Clamp01(CurrentTime / CurrentLength) : 0f;
        public float AnimationSampleTime => animationSampleTime;

        public float AnimationSampleNormalizedTime =>
            CurrentLength > 0f ? Mathf.Clamp01(animationSampleTime / CurrentLength) : 0f;

        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;
        public MontageRootMotionMode RootMotionMode => rootMotionMode;
        public Rigidbody RootMotionRigidbody => rootMotionRigidbody;
        public CharacterController RootMotionCharacterController => rootMotionCharacterController;

        public IMontageRootMotionController CustomRootMotionController => customRootMotionController.Value;

        /// <summary>
        /// AnimNotify가 실행될 때 호출됩니다.
        /// </summary>
        public event Action<AnimNotify, AnimNotifyContext> OnNotify;

        /// <summary>
        /// 재생 시작, 완료, 정지, 교체 이벤트를 한 번에 받고 싶을 때 사용합니다.
        /// </summary>
        public event Action<MontagePlaybackEventContext> OnPlaybackEvent;

        /// <summary>
        /// 몽타주 재생이 시작될 때 호출됩니다.
        /// </summary>
        public event Action<MontagePlaybackEventContext> OnPlay;

        /// <summary>
        /// 몽타주가 끝까지 재생되면 호출됩니다.
        /// </summary>
        public event Action<MontagePlaybackEventContext> OnComplete;

        /// <summary>
        /// Stop으로 재생을 멈췄을 때 호출됩니다.
        /// </summary>
        public event Action<MontagePlaybackEventContext> OnStop;

        /// <summary>
        /// 재생 중인 몽타주가 새 몽타주로 교체될 때 호출됩니다.
        /// </summary>
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
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (animator == null)
                return false;

            playableGraph ??= new MontagePlayableGraph(animator);
            playableGraph.Bind(animator);
            CacheRootMotionComponents();
            if (!notifyForwardingBound)
            {
                dispatcher.NotifyFired += ForwardNotify;
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
                bool wasBlendingOut = isBlendingOut;
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
            montageBlendElapsedTime += Mathf.Max(0f, deltaTime);
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
                    montage.Length);
                playback.Advance(deltaTime * timelineSpeed);
            }
            else
            {
                animationSampleTime = Mathf.Min(animationSampleTime + animationDeltaTime * montage.RateScale,
                    montage.Length);
                playback.Advance(deltaTime * timelineSpeed);
                UpdateAnimationSample(false, animationSampleTime);
                EvaluateGraph(deltaTime);
            }

            if (!playback.IsPlaying)
                timelineEvaluation = MontageNotifyEvaluator.Evaluate(montage, playback.CurrentTime);

            ApplyNotifyTransform(timelineEvaluation);
            if (!playback.IsPlaying)
                previousNotifyEvaluation = MontageNotifyEvaluation.Default;

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

            isBlendingOut = true;
            blendOutMontage = montage;
            blendOutElapsedTime = 0f;
            blendOutDuration = montage.BlendOut;
            blendOutStartWeight = Mathf.Clamp01(startWeight);
            blendOutStartTime = currentTime;
            SetMontageLayerWeight(blendOutStartWeight);
        }

        private void UpdateBlendOut(float deltaTime)
        {
            if (!isBlendingOut || blendOutMontage == null || playback.IsPaused)
                return;

            if (playableGraph is not { IsValid: true })
            {
                isBlendingOut = false;
                blendOutMontage = null;
                return;
            }

            blendOutElapsedTime += Mathf.Max(0f, deltaTime);

            float t = blendOutDuration > 0f ? Mathf.Clamp01(blendOutElapsedTime / blendOutDuration) : 1f;
            SetMontageLayerWeight(Mathf.Lerp(blendOutStartWeight, 0f, MontageBlendUtility.Evaluate(t)));

            if (UsesRootMotion(blendOutMontage))
            {
                rootMotionActiveThisFrame = true;
                EvaluateGraph(deltaTime);
            }
            else
            {
                EvaluateGraph(deltaTime);
            }

            if (t < 1f)
                return;

            AnimMontageSO completedMontage = blendOutMontage;
            float completedTime = blendOutStartTime;
            FinishMontageLayer();
            EmitPlaybackEvent(MontagePlaybackEventType.Complete, completedMontage, completedTime, completedTime);
        }
        private void OnAnimatorMove()
        {
            if (animator == null || !animator.applyRootMotion)
                return;

            bool montageOwnsRootMotion = rootMotionActiveThisFrame
                || ((playback.IsPlaying || playback.IsPaused) && UsesRootMotion(playback.Montage))
                || (isBlendingOut && UsesRootMotion(blendOutMontage));
            if (montageOwnsRootMotion)
                return;

            Transform animatedTransform = animator.transform;
            animatedTransform.position += animator.deltaPosition;
            animatedTransform.rotation *= animator.deltaRotation;
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

        public void SetRootMotionRigidbody(Rigidbody target) => rootMotionRigidbody = target;

        public void SetRootMotionCharacterController(CharacterController target) =>
            rootMotionCharacterController = target;

        public void SetCustomRootMotionController(IMontageRootMotionController controller) =>
            customRootMotionController.Value = controller;

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || !EnsureInitialized())
                return;

            AnimMontageSO previousMontage = isBlendingOut ? blendOutMontage : playback.Montage;
            float previousTime = isBlendingOut ? blendOutStartTime : playback.CurrentTime;
            bool wasPlaying = playback.IsPlaying || isBlendingOut;
            if (wasPlaying && previousMontage != null)
            {
                dispatcher.EndActiveStates(gameObject, animator, previousMontage, previousTime);
                EmitPlaybackEvent(MontagePlaybackEventType.Interrupted, previousMontage, previousTime, previousTime);
            }

            isBlendingOut = false;
            blendOutMontage = null;
            previousNotifyEvaluation = MontageNotifyEvaluation.Default;
            EnsureGraph();
            playback.Begin(montage, Mathf.Clamp(startTime, 0f, montage.Length));
            if (UsesRootMotion(montage))
                rootMotionSampler.Reset(animator);
            montageBlendElapsedTime = 0f;
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
            bool hadPlayback = montage != null && (playback.IsPlaying || playback.IsPaused || isBlendingOut);

            dispatcher.EndActiveStates(gameObject, animator, montage, currentTime);
            playback.Stop();
            previousNotifyEvaluation = MontageNotifyEvaluation.Default;
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

            isBlendingOut = false;
            blendOutMontage = null;
            blendOutElapsedTime = 0f;
            blendOutDuration = 0f;
            blendOutStartWeight = 0f;
            blendOutStartTime = 0f;
            rootMotionSampler.Stop();
        }

        private void DestroyGraph()
        {
            playableGraph?.Dispose();
            isBlendingOut = false;
            blendOutMontage = null;
            blendOutElapsedTime = 0f;
            blendOutDuration = 0f;
            blendOutStartWeight = 0f;
            blendOutStartTime = 0f;
            rootMotionSampler.Stop();
        }
        private void CacheRootMotionComponents()
        {
            if (rootMotionRigidbody == null)
                rootMotionRigidbody = GetComponentInParent<Rigidbody>();

            if (rootMotionCharacterController == null)
                rootMotionCharacterController = GetComponentInParent<CharacterController>();
        }

        private void ApplyNotifyTransform(MontageNotifyEvaluation evaluation)
        {
            Vector3 positionDelta = evaluation.PositionOffset - previousNotifyEvaluation.PositionOffset;
            Quaternion rotationDelta = Quaternion.Inverse(previousNotifyEvaluation.RotationOffset) *
                                       evaluation.RotationOffset;
            Vector3 scaleDelta = evaluation.ScaleOffset - previousNotifyEvaluation.ScaleOffset;

            if (positionDelta.sqrMagnitude > 0.0000001f)
                transform.position += transform.rotation * positionDelta;

            if (Quaternion.Angle(Quaternion.identity, rotationDelta) > 0.0001f)
                transform.rotation *= rotationDelta;

            if (scaleDelta.sqrMagnitude > 0.0000001f)
                transform.localScale += scaleDelta;

            previousNotifyEvaluation = evaluation;
        }

        private static bool UsesRootMotion(AnimMontageSO montage) =>
            MontageRootMotionUtility.IsEnabled(montage);
        private void ApplyRootMotionDelta(Vector3 deltaPosition, Quaternion deltaRotation, float montageLayerWeight)
        {
            MontageRootMotionUtility.Filter(playback.Montage ?? blendOutMontage, ref deltaPosition, ref deltaRotation);
            deltaRotation = Quaternion.SlerpUnclamped(Quaternion.identity, deltaRotation,
                Mathf.Clamp01(montageLayerWeight));
            if (!MontageRootMotionUtility.HasDelta(deltaPosition, deltaRotation))
                return;

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
            transform.position += deltaPosition;
            transform.rotation *= deltaRotation;
        }

        private void ApplyRigidbodyRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (rootMotionRigidbody == null)
                CacheRootMotionComponents();

            if (rootMotionRigidbody == null)
            {
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
                return;
            }

            controller.ApplyMontageRootMotion(this, animator, deltaPosition, deltaRotation);
        }

        private bool UpdateAnimationSample(bool force = false) =>
            UpdateAnimationSample(force, playback.CurrentTime);

        private bool UpdateAnimationSample(bool force, float sampleTime)
        {
            return playableGraph != null
                   && playableGraph.Sample(playback.Montage, sampleTime, force);
        }

        private float ComputeMontageLayerWeight(AnimMontageSO montage, float montageTime)
        {
            if (montage == null)
                return 0f;

            float weight = 1f;
            float blendIn = montage.BlendIn;
            if (blendIn > 0f)
                weight = Mathf.Min(weight,
                    MontageBlendUtility.Evaluate(montageBlendElapsedTime / blendIn));

            float blendOut = montage.BlendOut;
            float length = montage.Length;
            if (blendOut > 0f && length > 0f)
                weight = Mathf.Min(weight,
                    MontageBlendUtility.Evaluate((length - montageTime) / blendOut));

            return Mathf.Clamp01(weight);
        }

        private void SetMontageLayerWeight(float weight)
        {
            playableGraph?.SetMontageWeight(weight);
        }    }
}