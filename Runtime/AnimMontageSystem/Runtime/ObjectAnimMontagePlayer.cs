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


    /// <summary>
    /// 몽타주 재생 상태 변경 타입입니다.
    /// </summary>
    public enum MontagePlaybackEventType
    {
        Play,
        Complete,
        Stop,
        Interrupted
    }


    /// <summary>
    /// 이벤트에서 사용할 몽타주 런타임 정보입니다.
    /// </summary>
    public sealed class MontageRuntimeInfo
    {
        public MontageRuntimeInfo(string name, float length, float rateScale, bool applyRootMotion)
        {
            Name = name;
            Length = length;
            RateScale = rateScale;
            ApplyRootMotion = applyRootMotion;
        }

        /// <summary>
        /// 몽타주 이름입니다.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// 몽타주 전체 길이입니다.
        /// </summary>
        public float Length { get; }
        /// <summary>
        /// 몽타주 재생 배율입니다.
        /// </summary>
        public float RateScale { get; }
        /// <summary>
        /// 루트 모션 적용 여부입니다.
        /// </summary>
        public bool ApplyRootMotion { get; }

        internal static MontageRuntimeInfo FromMontage(AnimMontageSO montage)
        {
            return montage != null
                ? new MontageRuntimeInfo(montage.name, montage.Length, montage.RateScale, montage.ApplyRootMotion)
                : null;
        }
    }

    /// <summary>
    /// 몽타주 재생 이벤트에 함께 전달되는 정보입니다.
    /// </summary>
    public readonly struct MontagePlaybackEventContext
    {
        public MontagePlaybackEventContext(
            ObjectAnimMontagePlayer player,
            MontageRuntimeInfo runtimeInfo,
            MontagePlaybackEventType eventType,
            float previousTime,
            float currentTime)
        {
            Player = player;
            RuntimeInfo = runtimeInfo;
            EventType = eventType;
            PreviousTime = previousTime;
            CurrentTime = currentTime;
        }

        /// <summary>
        /// 이벤트를 보낸 플레이어입니다.
        /// </summary>
        public ObjectAnimMontagePlayer Player { get; }
        /// <summary>
        /// 이벤트가 발생한 몽타주의 런타임 정보입니다.
        /// </summary>
        public MontageRuntimeInfo RuntimeInfo { get; }
        /// <summary>
        /// 발생한 재생 이벤트 타입입니다.
        /// </summary>
        public MontagePlaybackEventType EventType { get; }
        /// <summary>
        /// 이벤트 직전의 재생 시간입니다.
        /// </summary>
        public float PreviousTime { get; }
        /// <summary>
        /// 이벤트가 발생한 재생 시간입니다.
        /// </summary>
        public float CurrentTime { get; }
        /// <summary>
        /// 몽타주 전체 길이입니다. 몽타주가 없으면 0입니다.
        /// </summary>
        public float Length => RuntimeInfo != null ? RuntimeInfo.Length : 0f;
        /// <summary>
        /// 현재 재생 위치를 0~1 범위로 나타낸 값입니다.
        /// </summary>
        public float NormalizedTime => Length > 0f ? Mathf.Clamp01(CurrentTime / Length) : 0f;
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

        [SerializeField] private Rigidbody rootMotionRigidbody;
        [SerializeField] private CharacterController rootMotionCharacterController;
        [SerializeField] private MontageRootMotionController customRootMotionController;
        private readonly MontagePlaybackState playback = new();
        private readonly MontageNotifyDispatcher dispatcher = new();
        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private readonly List<int> clipPlayableSegmentIndices = new();
        private readonly HashSet<int> activeTimelineElementIndices = new();
        private readonly List<int> timelineElementExitBuffer = new();
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

        public Animator Animator => animator;
        public AnimMontageSO CurrentMontage => playback.Montage;
        public float CurrentTime => playback.CurrentTime;
        public float CurrentLength => playback.Montage != null ? playback.Montage.Length : 0f;
        public float NormalizedTime => CurrentLength > 0f ? Mathf.Clamp01(CurrentTime / CurrentLength) : 0f;
        public float AnimationSampleTime => animationSampleTime;
        public float AnimationSampleNormalizedTime => CurrentLength > 0f ? Mathf.Clamp01(animationSampleTime / CurrentLength) : 0f;
        public bool IsPlaying => playback.IsPlaying;
        public bool IsPaused => playback.IsPaused;
        public MontageRootMotionMode RootMotionMode => rootMotionMode;
        public Rigidbody RootMotionRigidbody => rootMotionRigidbody;
        public CharacterController RootMotionCharacterController => rootMotionCharacterController;

        public IMontageRootMotionController CustomRootMotionController =>
            runtimeRootMotionController ?? customRootMotionController;
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

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheRootMotionComponents();
            dispatcher.NotifyFired += (notify, ctx) => OnNotify?.Invoke(notify, ctx);
        }

        private void Update()
        {
            rootMotionActiveThisFrame = false;
            if (!playback.IsPlaying || playback.IsPaused)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            bool wasPlayingBeforeAdvance = playback.IsPlaying;
            MontageTimelineElementEvaluation timelineEvaluation =
                MontageTimelineElementEvaluator.Evaluate(montage, currentTime);
            float timelineSpeed = timelineEvaluation.SpeedMultiplier;
            float timeScale = timelineEvaluation.TimeScaleMultiplier;
            UpdateTimelineElementBehaviours(montage, currentTime, deltaTime);
            float animationDeltaTime = deltaTime * timelineSpeed * timeScale;
            rootMotionActiveThisFrame = montage != null && montage.ApplyRootMotion;

            if (rootMotionActiveThisFrame)
            {
                UpdateAnimationSample(false, animationSampleTime);
                if (mixerRebuiltThisSample)
                    SyncRootMotionGraphAfterRebuild();

                EvaluateGraph(animationDeltaTime);
                animationSampleTime = Mathf.Min(animationSampleTime + animationDeltaTime * montage.RateScale, montage.Length);
                playback.Advance(deltaTime * timelineSpeed);
            }
            else
            {
                animationSampleTime = Mathf.Min(animationSampleTime + animationDeltaTime * montage.RateScale, montage.Length);
                playback.Advance(deltaTime * timelineSpeed);
                UpdateAnimationSample(false, animationSampleTime);
                EvaluateGraph(0f);
            }

            ApplyTimelineElementTransform(timelineEvaluation);
            if (!playback.IsPlaying)
                EndActiveTimelineElements(montage, playback.CurrentTime);
            dispatcher.Dispatch(playback, gameObject, animator, this);

            if (wasPlayingBeforeAdvance && !playback.IsPlaying)
                EmitPlaybackEvent(MontagePlaybackEventType.Complete, montage, currentTime, playback.CurrentTime);
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

        private void OnDestroy()
        {
            EndActiveTimelineElements(playback.Montage, playback.CurrentTime);
            DestroyGraph();
        }


        public void SetRootMotionRigidbody(Rigidbody target) => rootMotionRigidbody = target;

        public void SetRootMotionCharacterController(CharacterController target) =>
            rootMotionCharacterController = target;

        public void SetCustomRootMotionController(IMontageRootMotionController controller) =>
            runtimeRootMotionController = controller;

        public void Play(AnimMontageSO montage, float startTime = 0f)
        {
            if (montage == null || animator == null)
                return;

            AnimMontageSO previousMontage = playback.Montage;
            float previousTime = playback.CurrentTime;
            bool wasPlaying = playback.IsPlaying;
            if (wasPlaying)
            {
                dispatcher.EndActiveStates(gameObject, animator, previousMontage, previousTime);
                EmitPlaybackEvent(MontagePlaybackEventType.Interrupted, previousMontage, previousTime, previousTime);
            }

            EndActiveTimelineElements(previousMontage, previousTime);
            EnsureGraph();
            ApplyAnimatorRootMotion(montage.ApplyRootMotion);
            playback.Begin(montage, Mathf.Clamp(startTime, 0f, montage.Length));
            animationSampleTime = playback.CurrentTime;
            suppressNextRootMotion = montage.ApplyRootMotion;
            dispatcher.Reset();
            UpdateTimelineElementBehaviours(montage, playback.CurrentTime, 0f);
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            EmitPlaybackEvent(MontagePlaybackEventType.Play, montage, previousTime, playback.CurrentTime);
            dispatcher.Dispatch(playback, gameObject, animator, this);
        }
        public void Stop()
        {
            AnimMontageSO montage = playback.Montage;
            float currentTime = playback.CurrentTime;
            bool hadPlayback = montage != null && (playback.IsPlaying || playback.IsPaused);

            dispatcher.EndActiveStates(gameObject, animator, montage, currentTime);
            EndActiveTimelineElements(montage, currentTime);
            playback.Stop();
            DestroyGraph();
            RestoreAnimatorRootMotion();

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
                EndActiveTimelineElements(montage, currentTime);
            }

            playback.Pause(paused);
        }
        public void SetTime(float montageTime)
        {
            AnimMontageSO montage = playback.Montage;
            float previousTime = playback.CurrentTime;
            EndActiveTimelineElements(montage, previousTime);
            playback.SetTime(montageTime);
            UpdateTimelineElementBehaviours(playback.Montage, playback.CurrentTime, 0f);
            suppressNextRootMotion = playback.Montage != null && playback.Montage.ApplyRootMotion;
            UpdateAnimationSample(force: true);
            EvaluateGraph(0f);
            dispatcher.ScrubTo(playback, gameObject, animator);
        }
        public bool TryHandle(AnimNotify notify, AnimNotifyContext context) => false;
        private void EmitPlaybackEvent(
            MontagePlaybackEventType eventType,
            AnimMontageSO montage,
            float previousTime,
            float currentTime)
        {
            var context = new MontagePlaybackEventContext(this, MontageRuntimeInfo.FromMontage(montage), eventType, previousTime, currentTime);
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

        private void UpdateTimelineElementBehaviours(AnimMontageSO montage, float montageTime, float deltaTime)
        {
            if (montage == null)
            {
                EndActiveTimelineElements(null, montageTime);
                return;
            }

            timelineElementExitBuffer.Clear();
            foreach (int activeIndex in activeTimelineElementIndices)
            {
                if (!IsTimelineElementActive(montage, activeIndex, montageTime))
                    timelineElementExitBuffer.Add(activeIndex);
            }

            for (int i = 0; i < timelineElementExitBuffer.Count; i++)
                ExitTimelineElement(montage, timelineElementExitBuffer[i], montageTime, deltaTime);

            var elements = montage.CustomElements;
            for (int i = 0; i < elements.Count; i++)
            {
                CustomMontageElementPlacement placement = elements[i];
                MontageTimelineElement element = placement?.Element;
                if (element is not IMontageTimelineElementBehaviour behaviour
                    || !IsTimelineElementActive(placement, montageTime))
                {
                    continue;
                }

                var context = new MontageTimelineElementContext(this, montage, placement, i, montageTime, deltaTime);
                if (activeTimelineElementIndices.Add(i))
                    behaviour.OnElementEnter(context);

                behaviour.OnElementUpdate(context);
            }
        }

        private void EndActiveTimelineElements(AnimMontageSO montage, float montageTime)
        {
            if (activeTimelineElementIndices.Count == 0)
                return;

            timelineElementExitBuffer.Clear();
            foreach (int activeIndex in activeTimelineElementIndices)
                timelineElementExitBuffer.Add(activeIndex);

            for (int i = 0; i < timelineElementExitBuffer.Count; i++)
                ExitTimelineElement(montage, timelineElementExitBuffer[i], montageTime, 0f);
        }

        private void ExitTimelineElement(AnimMontageSO montage, int index, float montageTime, float deltaTime)
        {
            if (!activeTimelineElementIndices.Remove(index))
                return;

            if (montage == null || index < 0 || index >= montage.CustomElements.Count)
                return;

            CustomMontageElementPlacement placement = montage.CustomElements[index];
            if (placement?.Element is not IMontageTimelineElementBehaviour behaviour)
                return;

            behaviour.OnElementExit(new MontageTimelineElementContext(this, montage, placement, index, montageTime, deltaTime));
        }

        private static bool IsTimelineElementActive(AnimMontageSO montage, int index, float montageTime)
        {
            return montage != null
                && index >= 0
                && index < montage.CustomElements.Count
                && IsTimelineElementActive(montage.CustomElements[index], montageTime);
        }

        private static bool IsTimelineElementActive(CustomMontageElementPlacement placement, float montageTime)
        {
            return placement != null
                && placement.Element != null
                && montageTime >= placement.StartTime
                && montageTime <= placement.EndTime;
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
                if (!playable.IsValid() || playable.GetAnimationClip() != sample.Segment.Clip || clipPlayableSegmentIndices[i] != sample.SegmentIndex)
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
                    clipPlayableSegmentIndices[i] = sample.SegmentIndex;
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
            clipPlayableSegmentIndices.Clear();
            for (int i = 0; i < required; i++)
            {
                clipPlayables.Add(default);
                clipPlayableSegmentIndices.Add(-1);
            }
        }
    }
}
