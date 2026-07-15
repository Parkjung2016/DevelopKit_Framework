using System;
using System.Collections.Generic;
using System.Reflection;
using PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Tests
{
    internal sealed class MontageTestFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        public AnimMontageSO CreateMontage(
            MontageSegment[] segments = null,
            AnimNotifyPlacement[] notifies = null,
            AnimNotifyStatePlacement[] notifyStates = null,
            float rateScale = 1f)
        {
            AnimMontageSO montage = ScriptableObject.CreateInstance<AnimMontageSO>();
            createdObjects.Add(montage);
            SetField(montage, "segments", segments ?? Array.Empty<MontageSegment>());
            SetField(montage, "notifies", notifies ?? Array.Empty<AnimNotifyPlacement>());
            SetField(montage, "notifyStates", notifyStates ?? Array.Empty<AnimNotifyStatePlacement>());
            SetField(montage, "rateScale", rateScale);
            return montage;
        }

        public GameObject CreateGameObject(string name = "Montage Test Owner")
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        public AnimationClip CreateClip(float length, bool looping = false)
        {
            var clip = new AnimationClip { wrapMode = looping ? WrapMode.Loop : WrapMode.ClampForever };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, length, 1f));
            createdObjects.Add(clip);
            return clip;
        }

        public MontageSegment CreateAnimationSegment(
            AnimationClip clip,
            float startTime = 0f,
            float clipStartTime = 0f,
            float clipEndTime = 0f,
            float playRate = 1f,
            float blendIn = 0f,
            float blendOut = 0f,
            string trackId = "Default")
        {
            var segment = new MontageSegment { StartTime = startTime, TrackId = trackId };
            SetField(segment, "clip", clip);
            SetField(segment, "clipStartTime", clipStartTime);
            SetField(segment, "clipEndTime", clipEndTime);
            SetField(segment, "playRate", playRate);
            SetField(segment, "blendIn", blendIn);
            SetField(segment, "blendOut", blendOut);
            return segment;
        }

        public MontageSegment CreateEmptySegment(float startTime, float duration, string trackId = "Default")
        {
            var segment = new MontageSegment { StartTime = startTime, TrackId = trackId };
            SetField(segment, "segmentType", MontageSegmentType.EmptyState);
            SetField(segment, "emptyStateDuration", duration);
            return segment;
        }

        public static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);

            field.SetValue(target, value);
        }

        public void Dispose()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }
    }
}
