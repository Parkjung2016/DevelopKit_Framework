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

        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private int mixerInputCount;

        public void Dispose() => DestroyGraph();

        public bool TryEvaluate(
            GameObject instance,
            AnimMontageSO montage,
            float montageTime,
            Vector3 initialPosition,
            Quaternion initialRotation,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (instance == null || montage == null || !montage.ApplyRootMotion || montageTime <= 0f)
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

                EnsureGraph(1, animator);
                UpdateAnimationSample(montage, 0f, true);
                graph.Evaluate(0f);

                Transform rootTransform = animator.transform;
                Vector3 previousRootPosition = rootTransform.position;
                Quaternion previousRootRotation = rootTransform.rotation;
                Quaternion inverseInitialRotation = Quaternion.Inverse(initialRotation);

                float targetTime = Mathf.Max(0f, montageTime);
                float previousTime = 0f;
                while (previousTime < targetTime)
                {
                    float deltaTime = Mathf.Min(EvaluationStep, targetTime - previousTime);
                    float currentTime = previousTime + deltaTime;
                    UpdateAnimationSample(montage, currentTime, false);
                    graph.Evaluate(deltaTime);

                    Vector3 currentRootPosition = rootTransform.position;
                    Quaternion currentRootRotation = rootTransform.rotation;
                    Vector3 worldDeltaPosition = currentRootPosition - previousRootPosition;
                    Quaternion worldDeltaRotation = Quaternion.Inverse(previousRootRotation) * currentRootRotation;

                    if (worldDeltaPosition.sqrMagnitude <= 0.0000001f && animator.deltaPosition.sqrMagnitude > 0.0000001f)
                        worldDeltaPosition = initialRotation * (rotation * animator.deltaPosition);

                    if (IsIdentity(worldDeltaRotation) && !IsIdentity(animator.deltaRotation))
                        worldDeltaRotation = animator.deltaRotation;

                    position += inverseInitialRotation * worldDeltaPosition;
                    rotation *= worldDeltaRotation;
                    previousRootPosition = currentRootPosition;
                    previousRootRotation = currentRootRotation;
                    previousTime = currentTime;
                }

                return position.sqrMagnitude > 0.0000001f || !IsIdentity(rotation);
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

            EnsureGraph(samples.Count, null);
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

                if (resetPlayableTime)
                    playable.SetTime(sample.PlayableClipTime);

                playable.SetSpeed(sample.Segment.PlayRate);
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
            for (int i = 0; i < required; i++)
                clipPlayables.Add(default);
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
        }
    }
}