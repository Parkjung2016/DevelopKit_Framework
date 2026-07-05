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

        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private GameObject boundInstance;
        private Animator boundAnimator;
        private bool animatorPrepared;
        private int mixerInputCount;

        public void Bind(GameObject instance)
        {
            if (boundInstance == instance)
                return;

            boundInstance = instance;
            boundAnimator = instance != null ? instance.GetComponentInChildren<Animator>() : null;
            animatorPrepared = false;
            DestroyGraph();
        }

        public void Dispose() => DestroyGraph();

        public bool TrySample(GameObject instance, AnimMontageSO montage, float montageTime)
        {
            if (instance == null || montage == null)
                return false;

            Bind(instance);
            MontageSegmentBlending.Evaluate(montageTime, montage.Segments, samples);
            if (samples.Count == 0)
                return false;

            bool requiresMixer = samples.Count > 1 || MontageSegmentBlending.MontageHasBlends(montage.Segments);
            if (!requiresMixer && AnimationMode.InAnimationMode())
            {
                MontageSegmentSample sample = samples[0];
                AnimationMode.SampleAnimationClip(instance, sample.Segment.Clip, sample.ClipTime);
                return true;
            }

            if (boundAnimator == null)
                return SampleFallbackClip(instance, samples[0]);

            PrepareAnimatorForPlayables();
            EnsureGraph(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable clipPlayable = clipPlayables[i];
                if (!clipPlayable.IsValid() || clipPlayable.GetAnimationClip() != sample.Segment.Clip)
                {
                    if (clipPlayable.IsValid())
                        clipPlayable.Destroy();

                    clipPlayable = AnimationClipPlayable.Create(graph, sample.Segment.Clip);
                    clipPlayables[i] = clipPlayable;
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

        private void PrepareAnimatorForPlayables()
        {
            if (boundAnimator == null || animatorPrepared)
                return;

            boundAnimator.runtimeAnimatorController = null;
            boundAnimator.applyRootMotion = false;
            boundAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            boundAnimator.updateMode = AnimatorUpdateMode.Normal;
            boundAnimator.Rebind();
            animatorPrepared = true;
        }

        private void EnsureGraph(int inputCount)
        {
            int required = Mathf.Max(1, inputCount);
            if (graph.IsValid() && mixer.IsValid() && mixerInputCount == required)
                return;

            DestroyGraph();

            graph = PlayableGraph.Create("MontagePreviewBlend");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, required, true);
            mixerInputCount = required;
            output = AnimationPlayableOutput.Create(graph, "MontagePreviewOutput", boundAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            clipPlayables.Clear();
            for (int i = 0; i < required; i++)
                clipPlayables.Add(default);
        }

        private void DestroyGraph()
        {
            if (!graph.IsValid())
            {
                clipPlayables.Clear();
                mixerInputCount = 0;
                return;
            }

            graph.Destroy();
            clipPlayables.Clear();
            mixer = default;
            output = default;
            mixerInputCount = 0;
        }
    }
}
