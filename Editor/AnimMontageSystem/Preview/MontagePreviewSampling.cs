using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    [InitializeOnLoad]
    internal static class MontagePreviewSampling
    {
        private static readonly MontagePreviewBlendSampler BlendSampler = new();

        static MontagePreviewSampling()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
        }

        public static void BindInstance(GameObject instance) => BlendSampler.Bind(instance);

        public static void Reset() => BlendSampler.Reset();

        public static void Dispose() => BlendSampler.Dispose();

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingEditMode
                or PlayModeStateChange.EnteredPlayMode
                or PlayModeStateChange.ExitingPlayMode)
            {
                Dispose();
            }
        }

        public static bool TrySample(GameObject instance, MontageEditorContext context) =>
            TrySample(instance, context, context?.PlayheadTime ?? 0f);

        public static bool TrySample(GameObject instance, MontageEditorContext context, float sampleTime)
        {
            if (instance == null || context?.Montage == null)
                return false;

            if (BlendSampler.TrySample(instance, context.Montage, sampleTime, context.Montage.ApplyRootMotion))
                return true;

            if (!context.Montage.TryGetSegmentAtTime(sampleTime, out MontageSegment segment, out _))
                return false;

            AnimationClip clip = segment?.Clip;
            if (clip == null)
                return false;

            float clipTime = segment.ToClipTime(sampleTime);
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.SampleAnimationClip(instance, clip, clipTime);
                return true;
            }

            clip.SampleAnimation(instance, clipTime);
            return true;
        }
    }
}