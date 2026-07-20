using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    internal sealed class MontageRootMotionRuntimeSampler : System.IDisposable
    {
        private readonly List<MontageSegmentSample> samples = new();
        private readonly List<AnimationClipPlayable> clipPlayables = new();
        private readonly List<int> clipPlayableSegmentIndices = new();

        private GameObject samplerObject;
        private Animator samplerAnimator;
        private MontageRootMotionDeltaReceiver deltaReceiver;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;
        private AnimationMixerPlayable mixer;
        private int mixerInputCount;
        private Avatar boundAvatar;
        private bool requiresTimeSync;

        public void Dispose()
        {
            Stop();
            DestroySamplerObject();
        }

        public void Stop()
        {
            DestroyGraph();
            ResetSamplerTransform();
            deltaReceiver?.Clear();
        }

        public void Reset(Animator sourceAnimator)
        {
            EnsureSamplerObject(sourceAnimator);
            Stop();
            boundAvatar = samplerAnimator != null ? samplerAnimator.avatar : null;
        }

        public bool TryEvaluateStep(
            Animator sourceAnimator,
            AnimMontageSO montage,
            float montageTime,
            float deltaTime,
            Quaternion worldRotation,
            out Vector3 deltaPosition,
            out Quaternion deltaRotation)
        {
            deltaPosition = Vector3.zero;
            deltaRotation = Quaternion.identity;

            if (sourceAnimator == null || montage == null || !MontageRootMotionUtility.IsEnabled(montage) || deltaTime <= 0f)
                return false;

            EnsureSamplerObject(sourceAnimator);
            if (samplerAnimator == null || deltaReceiver == null)
                return false;

            MontageSegmentBlending.Evaluate(montageTime, montage.Segments, samples);
            if (samples.Count == 0)
            {
                requiresTimeSync = true;
                deltaReceiver.Clear();
                return false;
            }

            bool graphRebuilt = EnsureGraph(Mathf.Max(samples.Count, montage.Segments.Count));
            if (!graph.IsValid() || !mixer.IsValid())
                return false;

            bool sampleRebuilt = UpdateAnimationSample(montage, forceTime: graphRebuilt || requiresTimeSync);
            requiresTimeSync = false;
            if (graphRebuilt || sampleRebuilt)
            {
                deltaReceiver.Clear();
                graph.Evaluate(0f);
            }

            deltaReceiver.Clear();
            graph.Evaluate(deltaTime);

            if (!deltaReceiver.HasDelta)
                return false;

            deltaPosition = worldRotation * deltaReceiver.DeltaPosition;
            deltaRotation = deltaReceiver.DeltaRotation;
            return deltaPosition.sqrMagnitude > 0.0000001f
                   || Quaternion.Angle(Quaternion.identity, deltaRotation) > 0.0001f;
        }

        private void EnsureSamplerObject(Animator sourceAnimator)
        {
            if (sourceAnimator == null)
                return;

            if (samplerAnimator != null && samplerAnimator.avatar == sourceAnimator.avatar)
                return;

            Dispose();
            samplerObject = new GameObject($"{sourceAnimator.name}.MontageRootMotionSampler")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            samplerObject.SetActive(false);
            samplerAnimator = samplerObject.AddComponent<Animator>();
            samplerAnimator.avatar = sourceAnimator.avatar;
            samplerAnimator.runtimeAnimatorController = null;
            samplerAnimator.applyRootMotion = true;
            samplerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            samplerAnimator.updateMode = AnimatorUpdateMode.Normal;
            deltaReceiver = samplerObject.AddComponent<MontageRootMotionDeltaReceiver>();
            deltaReceiver.Bind(samplerAnimator);
            samplerObject.SetActive(true);
            ResetSamplerTransform();
            boundAvatar = samplerAnimator.avatar;
        }

        private void ResetSamplerTransform()
        {
            if (samplerObject == null)
                return;

            samplerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            samplerObject.transform.localScale = Vector3.one;
        }

        private bool EnsureGraph(int inputCount)
        {
            int required = Mathf.Max(1, inputCount);
            if (graph.IsValid() && mixer.IsValid() && mixerInputCount == required && boundAvatar == samplerAnimator.avatar)
                return false;

            DestroyGraph();
            if (samplerAnimator == null)
                return false;

            graph = PlayableGraph.Create("MontageRootMotionRuntimeSampler");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, required);
            mixerInputCount = required;
            output = AnimationPlayableOutput.Create(graph, "RootMotion", samplerAnimator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
            for (int i = 0; i < required; i++)
            {
                clipPlayables.Add(default);
                clipPlayableSegmentIndices.Add(-1);
            }

            boundAvatar = samplerAnimator.avatar;
            return true;
        }

        private bool UpdateAnimationSample(AnimMontageSO montage, bool forceTime)
        {
            bool rebuilt = false;
            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                AnimationClipPlayable playable = clipPlayables[i];
                bool resetPlayableTime = forceTime;
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
                    graph.Connect(playable, 0, mixer, i);
                    resetPlayableTime = true;
                    rebuilt = true;
                }

                if (sample.IsHeldPose)
                    playable.SetTime(sample.ClipTime);
                else if (resetPlayableTime)
                    playable.SetTime(Mathf.Max(sample.Segment.ClipStartTime, sample.PlayableClipTime));

                playable.SetSpeed(sample.IsHeldPose ? 0f : sample.Segment.PlayRate * montage.RateScale);
                mixer.SetInputWeight(i, sample.Weight);
            }

            for (int i = samples.Count; i < mixerInputCount; i++)
                mixer.SetInputWeight(i, 0f);

            return rebuilt;
        }

        private void DestroyGraph()
        {
            if (graph.IsValid())
                graph.Destroy();

            graph = default;
            output = default;
            mixer = default;
            mixerInputCount = 0;
            clipPlayables.Clear();
            clipPlayableSegmentIndices.Clear();
        }

        private void DestroySamplerObject()
        {
            if (samplerObject == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(samplerObject);
            else
                Object.DestroyImmediate(samplerObject);

            samplerObject = null;
            samplerAnimator = null;
            deltaReceiver = null;
            boundAvatar = null;
        }
    }

    internal sealed class MontageRootMotionDeltaReceiver : MonoBehaviour
    {
        private Animator animator;
        private Vector3 previousRootPosition;
        private Quaternion previousRootRotation = Quaternion.identity;
        private bool hasPreviousRootPosition;
        private bool hasPreviousRootRotation;

        public Vector3 DeltaPosition { get; private set; }
        public Quaternion DeltaRotation { get; private set; } = Quaternion.identity;
        public bool HasDelta { get; private set; }

        public void Bind(Animator target)
        {
            animator = target;
            CaptureRootTransform();
        }

        public void Clear()
        {
            DeltaPosition = Vector3.zero;
            DeltaRotation = Quaternion.identity;
            HasDelta = false;
            CaptureRootTransform();
        }

        private void CaptureRootTransform()
        {
            if (animator == null)
            {
                previousRootPosition = Vector3.zero;
                previousRootRotation = Quaternion.identity;
                hasPreviousRootPosition = false;
                hasPreviousRootRotation = false;
                return;
            }

            previousRootPosition = animator.rootPosition;
            previousRootRotation = animator.rootRotation;
            hasPreviousRootPosition = true;
            hasPreviousRootRotation = true;
        }

        private void OnAnimatorMove()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (animator == null)
                return;

            Vector3 frameDeltaPosition = animator.deltaPosition;
            if (hasPreviousRootPosition)
            {
                Vector3 rootPositionDelta = animator.rootPosition - previousRootPosition;
                frameDeltaPosition = RestoreMissingAxes(
                    frameDeltaPosition,
                    rootPositionDelta);
            }

            Quaternion frameDeltaRotation = animator.deltaRotation;
            if (Quaternion.Angle(Quaternion.identity, frameDeltaRotation) <= 0.0001f
                && hasPreviousRootRotation)
            {
                frameDeltaRotation =
                    Quaternion.Inverse(previousRootRotation) * animator.rootRotation;
            }

            DeltaPosition += frameDeltaPosition;
            DeltaRotation *= frameDeltaRotation;
            previousRootPosition = animator.rootPosition;
            previousRootRotation = animator.rootRotation;
            hasPreviousRootPosition = true;
            hasPreviousRootRotation = true;
            HasDelta = true;
        }

        private static Vector3 RestoreMissingAxes(Vector3 delta, Vector3 rootDelta)
        {
            const float epsilon = 0.000001f;
            if (Mathf.Abs(delta.x) <= epsilon)
                delta.x = rootDelta.x;
            if (Mathf.Abs(delta.y) <= epsilon)
                delta.y = rootDelta.y;
            if (Mathf.Abs(delta.z) <= epsilon)
                delta.z = rootDelta.z;

            return delta;
        }
    }
}
