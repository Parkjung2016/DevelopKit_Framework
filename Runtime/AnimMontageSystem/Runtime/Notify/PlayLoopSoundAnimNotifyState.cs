using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class PlayLoopSoundAnimNotifyState : AnimNotifyState
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float pitch = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private bool parentToOwner = true;

        [NonSerialized] private Dictionary<GameObject, AudioSource> activeSources;

        public override string DisplayName => "Loop Sound State";
        public override Color EditorColor => new(0.95f, 0.8f, 0.35f, 1f);

        public override void OnBegin(AnimNotifyContext context)
        {
            if (clip == null)
            {
                return;
            }

            GameObject key = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            if (key == null)
            {
                return;
            }

            activeSources ??= new Dictionary<GameObject, AudioSource>();
            StopSource(key);

            Transform ownerTransform = AnimNotifyRuntimeUtility.GetOwnerTransform(context);
            Vector3 worldPosition = ownerTransform != null
                ? ownerTransform.TransformPoint(localPosition)
                : localPosition;

            var audioObject = new GameObject($"AnimNotify Loop Audio - {clip.name}");
            audioObject.transform.position = worldPosition;
            AnimNotifyRuntimeUtility.MoveToOwnerScene(audioObject, context);
            if (parentToOwner && ownerTransform != null)
            {
                audioObject.transform.SetParent(ownerTransform, true);
            }

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Mathf.Approximately(pitch, 0f) ? 1f : pitch;
            source.spatialBlend = spatialBlend;
            source.loop = true;
            source.Play();

            activeSources[key] = source;
        }

        public override void OnEnd(AnimNotifyContext context)
        {
            GameObject key = AnimNotifyRuntimeUtility.GetOwnerKey(context);
            if (key == null)
            {
                return;
            }

            StopSource(key);
        }

        private void StopSource(GameObject key)
        {
            if (activeSources == null || !activeSources.TryGetValue(key, out AudioSource source))
            {
                return;
            }

            activeSources.Remove(key);
            if (source == null)
            {
                return;
            }

            source.Stop();
            AnimNotifyRuntimeUtility.DestroyObject(source.gameObject);
        }
    }
}