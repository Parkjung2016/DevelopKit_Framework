using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>
    /// AnimatorController와 몽타주 포즈를 하나의 PlayableGraph에서 섞습니다.
    /// </summary>
    internal sealed class MontagePlayableGraph : IDisposable
    {
        private struct ControllerLayerState
        {
            public int StateHash;
            public float NormalizedTime;
            public float Weight;
        }

        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private readonly List<int> clipPlayableSegmentIndices = new();
        private readonly List<ControllerLayerState> controllerStateBuffer = new();

        private Animator animator;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationLayerMixerPlayable layerMixer;
        private AnimatorControllerPlayable controllerPlayable;
        private AnimationMixerPlayable montageMixer;
        private RuntimeAnimatorController boundController;
        private AnimatorControllerParameter[] controllerParameters = Array.Empty<AnimatorControllerParameter>();
        private int montageMixerInputCount;
        private float montageWeight;
        private bool hasMontagePose;

        public MontagePlayableGraph(Animator animator)
        {
            this.animator = animator;
        }

        public bool IsValid => graph.IsValid();

        public void Bind(Animator target)
        {
            if (animator == target)
                return;

            Dispose();
            animator = target;
        }

        public void Ensure()
        {
            if (graph.IsValid())
            {
                EnsureControllerPlayable();
                return;
            }

            if (animator == null)
                return;

            graph = PlayableGraph.Create($"{animator.name}.AnimMontage");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            layerMixer = AnimationLayerMixerPlayable.Create(graph, 2);
            layerMixer.SetLayerAdditive(1, false);
            output.SetSourcePlayable(layerMixer);
            EnsureControllerPlayable();
            ApplyLayerWeights();
            graph.Play();
        }

        /// <returns>몽타주 Mixer를 새로 만들었으면 true입니다.</returns>
        public bool Sample(AnimMontageSO montage, float sampleTime, bool force)
        {
            if (!graph.IsValid() || montage == null)
                return false;

            MontageSegmentBlending.Evaluate(sampleTime, montage.Segments, samples);
            if (samples.Count == 0)
            {
                hasMontagePose = false;
                ClearMontageWeights();
                ApplyLayerWeights();
                return false;
            }

            hasMontagePose = true;
            bool mixerRebuilt = EnsureMontageMixer(Mathf.Max(samples.Count, montage.Segments.Count), false);

            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable playable = clipPlayables[i];
                bool advancePlayableTime = MontageRootMotionUtility.IsEnabled(montage)
                                           && !sample.IsHeldPose;
                bool resetPlayableTime = force;
                if (!playable.IsValid()
                    || playable.GetAnimationClip() != sample.Segment.Clip
                    || clipPlayableSegmentIndices[i] != sample.SegmentIndex)
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
                    graph.Connect(playable, 0, montageMixer, i);
                    resetPlayableTime = true;
                }

                if (!advancePlayableTime)
                    playable.SetTime(sample.ClipTime);
                else if (resetPlayableTime)
                    playable.SetTime(Mathf.Max(sample.Segment.ClipStartTime, sample.PlayableClipTime));

                playable.SetSpeed(advancePlayableTime ? sample.Segment.PlayRate * montage.RateScale : 0f);
                montageMixer.SetInputWeight(i, sample.Weight);
            }

            for (int i = samples.Count; i < montageMixerInputCount; i++)
                montageMixer.SetInputWeight(i, 0f);

            return mixerRebuilt;
        }

        public void SetMontageWeight(float weight)
        {
            montageWeight = Mathf.Clamp01(weight);
            if (!graph.IsValid())
                return;

            EnsureControllerPlayable();
            ApplyLayerWeights();
        }

        public void Evaluate(float deltaTime)
        {
            if (!graph.IsValid())
                return;

            EnsureControllerPlayable();
            SyncControllerParameters();
            graph.Evaluate(Mathf.Max(0f, deltaTime));
        }

        public void ResampleAfterMixerRebuild()
        {
            if (graph.IsValid())
                graph.Evaluate(0f);
        }

        public void Dispose()
        {
            if (graph.IsValid())
            {
                CaptureControllerState();
                graph.Destroy();
                RestoreControllerState();
            }

            graph = default;
            output = default;
            layerMixer = default;
            controllerPlayable = default;
            montageMixer = default;
            boundController = null;
            controllerParameters = Array.Empty<AnimatorControllerParameter>();
            montageMixerInputCount = 0;
            montageWeight = 0f;
            hasMontagePose = false;
            samples.Clear();
            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
            controllerStateBuffer.Clear();
        }

        private bool EnsureMontageMixer(int inputCount, bool force)
        {
            int required = Mathf.Max(1, inputCount);
            if (!force && montageMixer.IsValid() && montageMixerInputCount == required)
                return false;

            for (int i = 0; i < clipPlayables.Count; i++)
            {
                if (clipPlayables[i].IsValid())
                    clipPlayables[i].Destroy();
            }

            if (montageMixer.IsValid())
                montageMixer.Destroy();

            montageMixer = AnimationMixerPlayable.Create(graph, required);
            montageMixerInputCount = required;
            graph.Connect(montageMixer, 0, layerMixer, 1);

            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
            for (int i = 0; i < required; i++)
            {
                clipPlayables.Add(default);
                clipPlayableSegmentIndices.Add(-1);
            }

            ApplyLayerWeights();
            return true;
        }

        private void EnsureControllerPlayable()
        {
            if (!graph.IsValid() || animator == null)
                return;

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == boundController && controllerPlayable.IsValid())
                return;

            if (controllerPlayable.IsValid())
            {
                graph.Disconnect(layerMixer, 0);
                controllerPlayable.Destroy();
            }

            boundController = controller;
            controllerParameters = controller != null
                ? animator.parameters
                : Array.Empty<AnimatorControllerParameter>();

            if (controller == null)
            {
                ApplyLayerWeights();
                return;
            }

            controllerPlayable = AnimatorControllerPlayable.Create(graph, controller);
            SyncControllerPlayableStateFromAnimator();
            graph.Connect(controllerPlayable, 0, layerMixer, 0);
            ApplyLayerWeights();
        }

        private void ApplyLayerWeights()
        {
            if (!layerMixer.IsValid())
                return;

            float effectiveMontageWeight = montageMixer.IsValid() && hasMontagePose
                ? montageWeight
                : 0f;
            layerMixer.SetInputWeight(0, controllerPlayable.IsValid() ? 1f : 0f);
            layerMixer.SetInputWeight(1, effectiveMontageWeight);
        }

        private void ClearMontageWeights()
        {
            if (!montageMixer.IsValid())
                return;

            for (int i = 0; i < montageMixerInputCount; i++)
                montageMixer.SetInputWeight(i, 0f);
        }

        private void SyncControllerPlayableStateFromAnimator()
        {
            if (!controllerPlayable.IsValid() || animator == null || animator.layerCount <= 0)
                return;

            int layerCount = Mathf.Min(animator.layerCount, controllerPlayable.GetLayerCount());
            for (int i = 0; i < layerCount; i++)
            {
                controllerPlayable.SetLayerWeight(i, animator.GetLayerWeight(i));
                AnimatorStateInfo stateInfo = animator.IsInTransition(i)
                    ? animator.GetNextAnimatorStateInfo(i)
                    : animator.GetCurrentAnimatorStateInfo(i);

                if (stateInfo.fullPathHash != 0)
                    controllerPlayable.Play(stateInfo.fullPathHash, i, stateInfo.normalizedTime);
            }
        }

        private void SyncControllerParameters()
        {
            if (!controllerPlayable.IsValid() || animator == null)
                return;

            for (int i = 0; i < controllerParameters.Length; i++)
            {
                AnimatorControllerParameter parameter = controllerParameters[i];
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        controllerPlayable.SetFloat(hash, animator.GetFloat(hash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        controllerPlayable.SetInteger(hash, animator.GetInteger(hash));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        controllerPlayable.SetBool(hash, animator.GetBool(hash));
                        break;
                }
            }
        }

        private void CaptureControllerState()
        {
            controllerStateBuffer.Clear();
            if (!controllerPlayable.IsValid() || animator == null)
                return;

            int layerCount = Mathf.Min(animator.layerCount, controllerPlayable.GetLayerCount());
            for (int i = 0; i < layerCount; i++)
            {
                AnimatorStateInfo stateInfo = controllerPlayable.IsInTransition(i)
                    ? controllerPlayable.GetNextAnimatorStateInfo(i)
                    : controllerPlayable.GetCurrentAnimatorStateInfo(i);
                controllerStateBuffer.Add(new ControllerLayerState
                {
                    StateHash = stateInfo.fullPathHash,
                    NormalizedTime = stateInfo.normalizedTime,
                    Weight = controllerPlayable.GetLayerWeight(i)
                });
            }
        }

        private void RestoreControllerState()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            int layerCount = Mathf.Min(animator.layerCount, controllerStateBuffer.Count);
            for (int i = 0; i < layerCount; i++)
            {
                ControllerLayerState state = controllerStateBuffer[i];
                animator.SetLayerWeight(i, state.Weight);
                if (state.StateHash != 0)
                    animator.Play(state.StateHash, i, state.NormalizedTime);
            }

            if (layerCount > 0 && animator.isActiveAndEnabled)
                animator.Update(0f);
        }


    }
}
