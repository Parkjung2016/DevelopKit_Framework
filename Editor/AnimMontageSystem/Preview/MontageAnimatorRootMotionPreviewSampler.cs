using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontageAnimatorRootMotionPreviewSampler : System.IDisposable
    {
        private const float EvaluationStep = 1f / 60f;

        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private readonly List<int> clipPlayableSegmentIndices = new();

        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private int mixerInputCount;

        public void Dispose() => DestroyGraph();

        public bool TryEvaluate(
            GameObject instance,
            AnimMontageSO montage,
            float animationTime,
            float timelineTime,
            Vector3 initialPosition,
            Quaternion initialRotation,
            out Vector3 position,
            out Quaternion rotation,
            out bool includesTimelineTransform,
            out bool sampledRootMotion)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            includesTimelineTransform = false;
            sampledRootMotion = false;

            if (instance == null || montage == null || !montage.ApplyRootMotion)
                return false;

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
                return false;

            RuntimeAnimatorController cachedController = animator.runtimeAnimatorController;
            bool cachedRootMotion = animator.applyRootMotion;
            AnimatorCullingMode cachedCullingMode = animator.cullingMode;
            AnimatorUpdateMode cachedUpdateMode = animator.updateMode;
            Vector3 cachedPosition = instance.transform.position;
            Quaternion cachedRotation = instance.transform.rotation;
            Vector3 cachedScale = instance.transform.localScale;
            Vector3 cachedAnimatorLocalPosition = animator.transform.localPosition;
            Quaternion cachedAnimatorLocalRotation = animator.transform.localRotation;
            Vector3 cachedAnimatorLocalScale = animator.transform.localScale;

            try
            {
                DestroyGraph();

                instance.transform.SetPositionAndRotation(initialPosition, initialRotation);
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.Rebind();
                animator.Update(0f);

                EnsureGraph(Mathf.Max(1, montage.Segments.Count), animator);
                UpdateAnimationSample(montage, 0f, true);
                graph.Evaluate(0f);
                Transform rootTransform = animator.transform;
                Vector3 previousRootPosition = rootTransform.position;
                Quaternion previousRootRotation = rootTransform.rotation;
                Quaternion inverseInitialRotation = Quaternion.Inverse(initialRotation);
                Quaternion baseAccumulatedRotation = Quaternion.identity;
                bool hasTimelineTransformNotify = HasTimelineTransformNotify(montage);

                MontageNotifyEvaluation previousTimelineEvaluation = hasTimelineTransformNotify
                    ? MontageNotifyEvaluator.Evaluate(montage, 0f)
                    : MontageNotifyEvaluation.Default;
                if (hasTimelineTransformNotify)
                {
                    ApplyTimelineTransformDelta(
                        previousTimelineEvaluation.PositionOffset,
                        previousTimelineEvaluation.RotationOffset,
                        ref position,
                        ref rotation,
                        ref includesTimelineTransform);
                }


                float targetAnimationTime = Mathf.Max(0f, animationTime);
                float targetTimelineTime = Mathf.Max(0f, timelineTime);
                float traversalLength = Mathf.Max(targetAnimationTime, targetTimelineTime);
                float previousTraversalTime = 0f;
                float previousAnimationTime = 0f;
                while (previousTraversalTime < traversalLength)
                {
                    float currentTraversalTime = Mathf.Min(
                        traversalLength,
                        previousTraversalTime + EvaluationStep);
                    float traversalRatio = traversalLength > 0f
                        ? currentTraversalTime / traversalLength
                        : 1f;
                    float currentAnimationTime = targetAnimationTime * traversalRatio;
                    float currentTimelineTime = targetTimelineTime * traversalRatio;
                    float animationDeltaTime = currentAnimationTime - previousAnimationTime;

                    UpdateAnimationSample(montage, previousAnimationTime, false);
                    graph.Evaluate(animationDeltaTime);

                    Vector3 currentRootPosition = rootTransform.position;
                    Quaternion currentRootRotation = rootTransform.rotation;
                    Vector3 worldDeltaPosition = animator.deltaPosition;
                    Quaternion rawDeltaRotation = animator.deltaRotation;

                    if (worldDeltaPosition.sqrMagnitude <= 0.0000001f)
                        worldDeltaPosition = currentRootPosition - previousRootPosition;

                    if (IsIdentity(rawDeltaRotation))
                        rawDeltaRotation = Quaternion.Inverse(previousRootRotation) * currentRootRotation;

                    Quaternion baseDeltaRotation = MontageRootMotionUtility.ExtractYaw(rawDeltaRotation);
                    Vector3 initialSpaceDelta = inverseInitialRotation * worldDeltaPosition;
                    Vector3 localRootDelta = Quaternion.Inverse(baseAccumulatedRotation) * initialSpaceDelta;
                    Quaternion filteredRootRotation = baseDeltaRotation;
                    MontageRootMotionUtility.Filter(montage, ref localRootDelta, ref filteredRootRotation);

                    if (MontageRootMotionUtility.HasDelta(localRootDelta, filteredRootRotation))
                    {
                        position += rotation * localRootDelta;
                        rotation *= filteredRootRotation;
                        sampledRootMotion = true;
                    }

                    baseAccumulatedRotation *= baseDeltaRotation;

                    if (hasTimelineTransformNotify)
                    {
                        MontageNotifyEvaluation currentTimelineEvaluation =
                            MontageNotifyEvaluator.Evaluate(montage, currentTimelineTime);
                        Vector3 timelinePositionDelta = currentTimelineEvaluation.PositionOffset -
                                                        previousTimelineEvaluation.PositionOffset;
                        Quaternion timelineRotationDelta =
                            Quaternion.Inverse(previousTimelineEvaluation.RotationOffset) *
                            currentTimelineEvaluation.RotationOffset;
                        ApplyTimelineTransformDelta(
                            timelinePositionDelta,
                            timelineRotationDelta,
                            ref position,
                            ref rotation,
                            ref includesTimelineTransform);

                        previousTimelineEvaluation = currentTimelineEvaluation;
                    }
                    previousRootPosition = currentRootPosition;
                    previousRootRotation = currentRootRotation;
                    previousAnimationTime = currentAnimationTime;
                    previousTraversalTime = currentTraversalTime;
                }

                return sampledRootMotion || includesTimelineTransform;
            }
            finally
            {
                DestroyGraph();
                animator.runtimeAnimatorController = cachedController;
                animator.applyRootMotion = cachedRootMotion;
                animator.cullingMode = cachedCullingMode;
                animator.updateMode = cachedUpdateMode;
                animator.transform.localPosition = cachedAnimatorLocalPosition;
                animator.transform.localRotation = cachedAnimatorLocalRotation;
                animator.transform.localScale = cachedAnimatorLocalScale;
                instance.transform.localScale = cachedScale;
                instance.transform.SetPositionAndRotation(cachedPosition, cachedRotation);
            }
        }

        private static bool HasTimelineTransformNotify(AnimMontageSO montage)
        {
            var notifies = montage.Notifies;
            for (int i = 0; i < notifies.Count; i++)
            {
                if (notifies[i]?.Notify is IMontageTransformOffsetNotify)
                    return true;
            }

            return false;
        }

        private static void ApplyTimelineTransformDelta(
            Vector3 positionDelta,
            Quaternion rotationDelta,
            ref Vector3 position,
            ref Quaternion rotation,
            ref bool includesTimelineTransform)
        {
            if (positionDelta.sqrMagnitude > 0.0000001f)
            {
                position += rotation * positionDelta;
                includesTimelineTransform = true;
            }

            if (Quaternion.Angle(Quaternion.identity, rotationDelta) <= 0.0001f)
                return;

            rotation *= rotationDelta;
            includesTimelineTransform = true;
        }

        private static bool IsIdentity(Quaternion rotation) =>
            Mathf.Abs(rotation.x) < 0.00001f
            && Mathf.Abs(rotation.y) < 0.00001f
            && Mathf.Abs(rotation.z) < 0.00001f
            && Mathf.Abs(rotation.w - 1f) < 0.00001f;
        private void UpdateAnimationSample(AnimMontageSO montage, float montageTime, bool force)
        {
            MontageSegmentBlending.Evaluate(montageTime, montage.Segments, samples);
            if (samples.Count == 0)
                return;

            EnsureGraph(Mathf.Max(samples.Count, montage.Segments.Count), null);
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
                    playable.SetApplyFootIK(false);
                    playable.SetApplyPlayableIK(true);
                    playable.SetDuration(sample.Segment.IsLoopingClip
                        ? Mathf.Max(sample.Segment.ClipEndTime, sample.Segment.Clip.length)
                        : sample.Segment.Clip.length);
                    clipPlayables[i] = playable;
                    clipPlayableSegmentIndices[i] = sample.SegmentIndex;
                    graph.Connect(playable, 0, mixer, i);
                    resetPlayableTime = true;
                }

                float playableSpeed = sample.IsHeldPose ? 0f : sample.Segment.PlayRate;
                if (sample.IsHeldPose)
                    playable.SetTime(sample.ClipTime);
                else if (resetPlayableTime)
                    playable.SetTime(Mathf.Max(sample.Segment.ClipStartTime, sample.PlayableClipTime));

                playable.SetSpeed(playableSpeed);
                mixer.SetInputWeight(i, sample.Weight);
            }

            for (int i = samples.Count; i < mixerInputCount; i++)
                mixer.SetInputWeight(i, 0f);
        }

        private void EnsureGraph(int inputCount, Animator animator)
        {
            int required = Mathf.Max(1, inputCount);
            if (graph.IsValid() && mixer.IsValid() && mixerInputCount == required)
                return;

            Animator outputAnimator = animator;
            if (outputAnimator == null && output.IsOutputValid())
                outputAnimator = (Animator)output.GetTarget();

            DestroyGraph();
            if (outputAnimator == null)
                return;

            graph = PlayableGraph.Create("MontageRootMotionPreview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, required);
            mixerInputCount = required;
            output = AnimationPlayableOutput.Create(graph, "RootMotionPreview", outputAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
            for (int i = 0; i < required; i++)
            {
                clipPlayables.Add(default);
                clipPlayableSegmentIndices.Add(-1);
            }
        }

        private void DestroyGraph()
        {
            if (graph.IsValid())
                graph.Destroy();

            graph = default;
            mixer = default;
            output = default;
            mixerInputCount = 0;
            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
        }
    }
}