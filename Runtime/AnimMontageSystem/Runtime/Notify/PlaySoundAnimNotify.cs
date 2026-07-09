using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public sealed class PlaySoundAnimNotify : AnimNotify
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float pitch = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private Vector3 localPosition;

        public override string DisplayName => "Play Sound";
        public override Color EditorColor => new(0.45f, 0.75f, 1f, 1f);

        public override void OnNotify(AnimNotifyContext context)
        {
            if (clip == null)
            {
                return;
            }

            Transform ownerTransform = AnimNotifyRuntimeUtility.GetOwnerTransform(context);
            Vector3 worldPosition = ownerTransform != null
                ? ownerTransform.TransformPoint(localPosition)
                : localPosition;

            var audioObject = new GameObject($"AnimNotify Audio - {clip.name}");
            audioObject.transform.position = worldPosition;
            AnimNotifyRuntimeUtility.MoveToOwnerScene(audioObject, context);

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Mathf.Approximately(pitch, 0f) ? 1f : pitch;
            source.spatialBlend = spatialBlend;
            source.Play();

            float lifetime = clip.length / Mathf.Abs(source.pitch);
            AnimNotifyRuntimeUtility.DestroyObject(audioObject, lifetime);
        }
    }
}
