using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Anim Montage Auto Player")]
    [DisallowMultipleComponent]
    public sealed class ObjectAnimMontageAutoPlayer : MonoBehaviour
    {
        [SerializeField] private ObjectAnimMontagePlayer player;
        [SerializeField] private AnimMontageSO montage;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private float startTime;

        public ObjectAnimMontagePlayer Player => player;
        public AnimMontageSO Montage => montage;
        public bool PlayOnAwake => playOnAwake;
        public float StartTime => startTime;

        private void Awake()
        {
            if (player == null)
                player = GetComponent<ObjectAnimMontagePlayer>();

            if (playOnAwake)
                Play();
        }

        public void Play()
        {
            if (player == null || montage == null)
                return;

            player.Play(montage, Mathf.Max(0f, startTime));
        }
    }
}