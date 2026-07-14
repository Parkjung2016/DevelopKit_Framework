using System.Collections.Generic;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.Editors.AnimMontageSystem
{
    internal static class MontageRootMotionPreviewUtility
    {
        private const float EvaluationStep = 1f / 60f;

        private static readonly Dictionary<AnimationClip, RootMotionCurves> CurveCache = new();
        private static readonly List<MontageSegmentSample> CurrentSamples = new();
        private static readonly List<MontageSegmentSample> PreviousSamples = new();

        public static bool TryEvaluate(AnimMontageSO montage, float montageTime, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (montage == null || !montage.ApplyRootMotion)
                return false;

            bool hasRootMotion = false;
            float targetTime = Mathf.Max(0f, montageTime);
            if (targetTime <= 0f)
                return HasAnyRootMotionCurve(montage.Segments);

            float previousTime = 0f;
            while (previousTime < targetTime)
            {
                float currentTime = Mathf.Min(targetTime, previousTime + EvaluationStep);
                MontageSegmentBlending.Evaluate(previousTime, montage.Segments, PreviousSamples);
                MontageSegmentBlending.Evaluate(currentTime, montage.Segments, CurrentSamples);

                EvaluateBlendedStepDelta(PreviousSamples, CurrentSamples, out Vector3 deltaPosition, out Quaternion deltaRotation, ref hasRootMotion);
                position += rotation * deltaPosition;
                rotation = rotation * deltaRotation;
                previousTime = currentTime;
            }

            return hasRootMotion;
        }

        private static bool HasAnyRootMotionCurve(IReadOnlyList<MontageSegment> segments)
        {
            if (segments == null)
                return false;

            for (int i = 0; i < segments.Count; i++)
            {
                MontageSegment segment = segments[i];
                if (segment?.Clip != null && GetCurves(segment.Clip).HasAnyCurve)
                    return true;
            }

            return false;
        }

        private static void EvaluateBlendedStepDelta(
            List<MontageSegmentSample> previousSamples,
            List<MontageSegmentSample> currentSamples,
            out Vector3 position,
            out Quaternion rotation,
            ref bool hasRootMotion)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Quaternion blendedRotationDelta = Quaternion.identity;
            float rotationWeight = 0f;

            for (int i = 0; i < currentSamples.Count; i++)
            {
                MontageSegmentSample current = currentSamples[i];
                MontageSegmentSample previous = FindPreviousSample(previousSamples, current);
                if (previous.Segment == null)
                    continue;

                RootMotionCurves curves = GetCurves(current.Segment.Clip);
                if (!curves.HasAnyCurve)
                    continue;

                EvaluateSegmentDelta(
                    current.Segment,
                    curves,
                    previous.ClipTime,
                    current.ClipTime,
                    out Vector3 segmentPosition,
                    out Quaternion segmentRotation);

                float weight = Mathf.Clamp01((previous.Weight + current.Weight) * 0.5f);
                position += segmentPosition * weight;
                blendedRotationDelta = Quaternion.Slerp(blendedRotationDelta, segmentRotation, weight / Mathf.Max(0.0001f, rotationWeight + weight));
                rotationWeight += weight;
                hasRootMotion = true;
            }

            rotation = rotationWeight > 0f
                ? MontageRootMotionUtility.ExtractYaw(blendedRotationDelta)
                : Quaternion.identity;
        }

        private static MontageSegmentSample FindPreviousSample(List<MontageSegmentSample> samples, MontageSegmentSample current)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                MontageSegmentSample sample = samples[i];
                if (sample.SegmentIndex == current.SegmentIndex)
                    return sample;
            }

            return new MontageSegmentSample(current.Segment, current.SegmentIndex, current.ClipTime, 0f);
        }

        private static RootMotionCurves GetCurves(AnimationClip clip)
        {
            if (clip == null)
                return RootMotionCurves.Empty;

            if (CurveCache.TryGetValue(clip, out RootMotionCurves curves))
                return curves;

            curves = new RootMotionCurves { ImportSettings = GetRootMotionImportSettings(clip) };
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (!string.IsNullOrEmpty(binding.path))
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                switch (binding.propertyName)
                {
                    case "RootT.x": curves.PositionX = curve; break;
                    case "RootT.y": curves.PositionY = curve; break;
                    case "RootT.z": curves.PositionZ = curve; break;
                    case "RootQ.x": curves.RotationX = curve; break;
                    case "RootQ.y": curves.RotationY = curve; break;
                    case "RootQ.z": curves.RotationZ = curve; break;
                    case "RootQ.w": curves.RotationW = curve; break;
                }
            }

            CurveCache[clip] = curves;
            return curves;
        }

        private static void EvaluateSegmentDelta(
            MontageSegment segment,
            RootMotionCurves curves,
            float fromClipTime,
            float toClipTime,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (segment.IsLoopingClip && toClipTime < fromClipTime)
                toClipTime += Mathf.Max(0.0001f, segment.LoopEndTime - segment.ClipStartTime);

            if (toClipTime <= fromClipTime)
                return;

            if (!segment.IsLoopingClip)
            {
                EvaluateDelta(curves, fromClipTime, Mathf.Min(toClipTime, segment.ClipEndTime), out position, out rotation);
                ApplyImportSettings(curves.ImportSettings, ref position, ref rotation);
                return;
            }

            float loopStart = segment.ClipStartTime;
            float loopEnd = Mathf.Max(loopStart + 0.0001f, segment.LoopEndTime);
            float cursor = fromClipTime;
            while (cursor < toClipTime)
            {
                float normalizedCursor = NormalizeLoopTime(cursor, loopStart, loopEnd);
                float remainingInLoop = loopEnd - normalizedCursor;
                float stepEndRaw = Mathf.Min(toClipTime, cursor + remainingInLoop);
                float normalizedStepEnd = Mathf.Approximately(stepEndRaw, cursor + remainingInLoop)
                    ? loopEnd
                    : NormalizeLoopTime(stepEndRaw, loopStart, loopEnd);

                EvaluateDelta(curves, normalizedCursor, normalizedStepEnd, out Vector3 stepPosition, out Quaternion stepRotation);
                ApplyImportSettings(curves.ImportSettings, ref stepPosition, ref stepRotation);
                position += rotation * stepPosition;
                rotation = rotation * stepRotation;
                cursor = stepEndRaw;
            }
        }

        private static float NormalizeLoopTime(float time, float loopStart, float loopEnd)
        {
            float length = Mathf.Max(0.0001f, loopEnd - loopStart);
            return loopStart + Mathf.Repeat(time - loopStart, length);
        }

        private static RootMotionImportSettings GetRootMotionImportSettings(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path))
                return RootMotionImportSettings.Default;

            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                return RootMotionImportSettings.Default;

            ModelImporterClipAnimation clipAnimation = FindClipAnimation(importer, clip.name);
            if (clipAnimation == null)
                return RootMotionImportSettings.Default;

            return new RootMotionImportSettings(
                clipAnimation.lockRootRotation,
                clipAnimation.keepOriginalOrientation,
                clipAnimation.rotationOffset,
                clipAnimation.lockRootHeightY,
                clipAnimation.keepOriginalPositionY,
                clipAnimation.heightFromFeet,
                clipAnimation.lockRootPositionXZ,
                clipAnimation.keepOriginalPositionXZ);
        }

        private static ModelImporterClipAnimation FindClipAnimation(ModelImporter importer, string clipName)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null)
                return null;

            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                if (clip != null && clip.name == clipName)
                    return clip;
            }

            return null;
        }

        private static void ApplyImportSettings(RootMotionImportSettings settings, ref Vector3 position, ref Quaternion rotation)
        {
            if (settings.LockRootRotation)
                rotation = Quaternion.identity;
            else if (!Mathf.Approximately(settings.RotationOffset, 0f))
            {
                Quaternion offset = Quaternion.Euler(0f, settings.RotationOffset, 0f);
                position = offset * position;
                rotation = offset * rotation * Quaternion.Inverse(offset);
            }

            if (settings.LockRootHeightY)
                position.y = 0f;

            if (settings.LockRootPositionXZ)
            {
                position.x = 0f;
                position.z = 0f;
            }
        }
        private static void EvaluateDelta(RootMotionCurves curves, float from, float to, out Vector3 position, out Quaternion rotation)
        {
            Vector3 startPosition = curves.EvaluatePosition(from);
            Vector3 endPosition = curves.EvaluatePosition(to);
            Quaternion startRotation = curves.EvaluateRotation(from);
            Quaternion endRotation = curves.EvaluateRotation(to);
            position = endPosition - startPosition;
            rotation = Quaternion.Inverse(startRotation) * endRotation;
        }


        private readonly struct RootMotionImportSettings
        {
            public static readonly RootMotionImportSettings Default = new(false, true, 0f, false, true, false, false, true);

            public RootMotionImportSettings(
                bool lockRootRotation,
                bool keepOriginalOrientation,
                float rotationOffset,
                bool lockRootHeightY,
                bool keepOriginalPositionY,
                bool heightFromFeet,
                bool lockRootPositionXZ,
                bool keepOriginalPositionXZ)
            {
                LockRootRotation = lockRootRotation;
                KeepOriginalOrientation = keepOriginalOrientation;
                RotationOffset = rotationOffset;
                LockRootHeightY = lockRootHeightY;
                KeepOriginalPositionY = keepOriginalPositionY;
                HeightFromFeet = heightFromFeet;
                LockRootPositionXZ = lockRootPositionXZ;
                KeepOriginalPositionXZ = keepOriginalPositionXZ;
            }

            public bool LockRootRotation { get; }
            public bool KeepOriginalOrientation { get; }
            public float RotationOffset { get; }
            public bool LockRootHeightY { get; }
            public bool KeepOriginalPositionY { get; }
            public bool HeightFromFeet { get; }
            public bool LockRootPositionXZ { get; }
            public bool KeepOriginalPositionXZ { get; }
        }
        private sealed class RootMotionCurves
        {
            public static readonly RootMotionCurves Empty = new();

            public AnimationCurve PositionX;
            public AnimationCurve PositionY;
            public AnimationCurve PositionZ;
            public AnimationCurve RotationX;
            public AnimationCurve RotationY;
            public AnimationCurve RotationZ;
            public AnimationCurve RotationW;
            public RootMotionImportSettings ImportSettings;

            public bool HasAnyCurve =>
                PositionX != null || PositionY != null || PositionZ != null ||
                RotationX != null || RotationY != null || RotationZ != null || RotationW != null;

            public Vector3 EvaluatePosition(float time)
            {
                return new Vector3(
                    PositionX?.Evaluate(time) ?? 0f,
                    PositionY?.Evaluate(time) ?? 0f,
                    PositionZ?.Evaluate(time) ?? 0f);
            }

            public Quaternion EvaluateRotation(float time)
            {
                if (RotationX == null && RotationY == null && RotationZ == null && RotationW == null)
                    return Quaternion.identity;

                var rotation = new Quaternion(
                    RotationX?.Evaluate(time) ?? 0f,
                    RotationY?.Evaluate(time) ?? 0f,
                    RotationZ?.Evaluate(time) ?? 0f,
                    RotationW?.Evaluate(time) ?? 1f);
                return Normalize(rotation);
            }

            private static Quaternion Normalize(Quaternion rotation)
            {
                float magnitude = Mathf.Sqrt(
                    rotation.x * rotation.x +
                    rotation.y * rotation.y +
                    rotation.z * rotation.z +
                    rotation.w * rotation.w);
                if (magnitude <= 0.00001f)
                    return Quaternion.identity;

                float inv = 1f / magnitude;
                return new Quaternion(rotation.x * inv, rotation.y * inv, rotation.z * inv, rotation.w * inv);
            }
        }
    }
}