using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal sealed class MontagePreviewBlendSampler
    {
        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private readonly List<int> clipPlayableSegmentIndices = new();

        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private GameObject boundInstance;
        private Animator boundAnimator;
        private bool animatorPrepared;
        private bool preparedApplyRootMotion;
        private RuntimeAnimatorController cachedController;
        private bool cachedApplyRootMotion;
        private AnimatorCullingMode cachedCullingMode;
        private AnimatorUpdateMode cachedUpdateMode;
        private bool hasCachedAnimatorState;
        private int mixerInputCount;

        public void Bind(GameObject instance)
        {
            if (boundInstance == instance)
                return;

            Reset();
            boundInstance = instance;
            boundAnimator = instance != null ? instance.GetComponentInChildren<Animator>() : null;
        }

        public void Dispose()
        {
            Reset();
            boundInstance = null;
            boundAnimator = null;
        }

        public void Reset()
        {
            RestoreAnimatorState();
            animatorPrepared = false;
            preparedApplyRootMotion = false;
            DestroyGraph();
        }

        public bool TrySample(GameObject instance, AnimMontageSO montage, float montageTime, bool applyRootMotion)
        {
            if (instance == null || montage == null)
                return false;

            Bind(instance);
            MontageSegmentBlending.Evaluate(montageTime, montage.Segments, samples);
            if (samples.Count == 0)
                return false;

            bool requiresMixer = samples.Count > 1
                                 || MontageSegmentBlending.MontageHasBlends(montage.Segments)
                                 || applyRootMotion;
            if (!requiresMixer && AnimationMode.InAnimationMode())
            {
                MontageSegmentSample sample = samples[0];
                AnimationMode.SampleAnimationClip(instance, sample.Segment.Clip, sample.ClipTime);
                return true;
            }

            if (boundAnimator == null)
                return SampleFallbackClip(instance, samples[0]);

            PrepareAnimatorForPlayables(applyRootMotion);
            EnsureGraph(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable clipPlayable = clipPlayables[i];
                if (!clipPlayable.IsValid() || clipPlayable.GetAnimationClip() != sample.Segment.Clip || clipPlayableSegmentIndices[i] != sample.SegmentIndex)
                {
                    if (clipPlayable.IsValid())
                        clipPlayable.Destroy();

                    clipPlayable = AnimationClipPlayable.Create(graph, sample.Segment.Clip);
                    clipPlayables[i] = clipPlayable;
                    clipPlayableSegmentIndices[i] = sample.SegmentIndex;
                    graph.Connect(clipPlayable, 0, mixer, i);
                }

                clipPlayable.SetApplyFootIK(true);
                clipPlayable.SetApplyPlayableIK(true);
                clipPlayable.SetTime(sample.ClipTime);
                clipPlayable.SetSpeed(0f);
                mixer.SetInputWeight(i, sample.Weight);
            }

            for (int i = samples.Count; i < mixerInputCount; i++)
                mixer.SetInputWeight(i, 0f);

            if (AnimationMode.InAnimationMode())
                AnimationMode.SamplePlayableGraph(graph, 0, 0f);
            else
                graph.Evaluate(0f);

            return true;
        }

        private static bool SampleFallbackClip(GameObject instance, MontageSegmentSample sample)
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.SampleAnimationClip(instance, sample.Segment.Clip, sample.ClipTime);
                return true;
            }

            sample.Segment.Clip.SampleAnimation(instance, sample.ClipTime);
            return true;
        }

        private void PrepareAnimatorForPlayables(bool applyRootMotion)
        {
            if (boundAnimator == null || animatorPrepared && preparedApplyRootMotion == applyRootMotion)
                return;

            CacheAnimatorState();
            boundAnimator.runtimeAnimatorController = null;
            boundAnimator.applyRootMotion = applyRootMotion;
            boundAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            boundAnimator.updateMode = AnimatorUpdateMode.Normal;
            boundAnimator.Rebind();
            boundAnimator.Update(0f);
            animatorPrepared = true;
            preparedApplyRootMotion = applyRootMotion;
        }

        private void CacheAnimatorState()
        {
            if (boundAnimator == null || hasCachedAnimatorState)
                return;

            cachedController = boundAnimator.runtimeAnimatorController;
            cachedApplyRootMotion = boundAnimator.applyRootMotion;
            cachedCullingMode = boundAnimator.cullingMode;
            cachedUpdateMode = boundAnimator.updateMode;
            hasCachedAnimatorState = true;
        }

        private void RestoreAnimatorState()
        {
            if (boundAnimator == null || !hasCachedAnimatorState)
            {
                hasCachedAnimatorState = false;
                cachedController = null;
                return;
            }

            boundAnimator.runtimeAnimatorController = cachedController;
            boundAnimator.applyRootMotion = cachedApplyRootMotion;
            boundAnimator.cullingMode = cachedCullingMode;
            boundAnimator.updateMode = cachedUpdateMode;
            boundAnimator.Rebind();
            boundAnimator.Update(0f);
            hasCachedAnimatorState = false;
            cachedController = null;
        }

        private void EnsureGraph(int inputCount)
        {
            int required = Mathf.Max(1, inputCount);
            if (graph.IsValid() && mixer.IsValid() && mixerInputCount == required)
                return;

            DestroyGraph();

            graph = PlayableGraph.Create("MontagePreviewBlend");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, required);
            mixerInputCount = required;
            output = AnimationPlayableOutput.Create(graph, "MontagePreviewOutput", boundAnimator);
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
            if (!graph.IsValid())
            {
                clipPlayables.Clear();
                clipPlayableSegmentIndices.Clear();
                mixerInputCount = 0;
                return;
            }

            graph.Destroy();
            graph = default;
            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
            mixer = default;
            output = default;
            mixerInputCount = 0;
        }
    }
}


