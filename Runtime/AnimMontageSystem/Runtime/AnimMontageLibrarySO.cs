using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    /// <summary>같은 모델에서 사용할 Animation Sequence와 Montage를 묶습니다.</summary>
    [CreateAssetMenu(fileName = "AnimationLibrary_", menuName = "PJDev/Animation/Animation Library")]
    public sealed class AnimMontageLibrarySO : ScriptableObject
    {
        [SerializeField] private GameObject previewModel;
        [SerializeField] private AnimStateMachineSO stateMachine;
        [SerializeField] private AnimSequenceSO[] sequences = Array.Empty<AnimSequenceSO>();
        [SerializeField] private AnimMontageSO[] montages = Array.Empty<AnimMontageSO>();

        public GameObject PreviewModel => previewModel;
        public AnimStateMachineSO StateMachine => stateMachine;
        public IReadOnlyList<AnimSequenceSO> Sequences => sequences ?? Array.Empty<AnimSequenceSO>();
        public IReadOnlyList<AnimMontageSO> Montages => montages ?? Array.Empty<AnimMontageSO>();

        public bool Contains(AnimSequenceSO sequence) => ContainsReference(sequences, sequence);
        public bool Contains(AnimMontageSO montage) => ContainsReference(montages, montage);

        private static bool ContainsReference<T>(T[] assets, T target) where T : UnityEngine.Object
        {
            if (target == null || assets == null)
                return false;

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] == target)
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sequences = RemoveMissingAndDuplicateAssets(sequences);
            montages = RemoveMissingAndDuplicateMontages(montages);
        }

        private static AnimMontageSO[] RemoveMissingAndDuplicateMontages(AnimMontageSO[] assets)
        {
            if (assets == null || assets.Length == 0)
                return Array.Empty<AnimMontageSO>();

            var unique = new List<AnimMontageSO>(assets.Length);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimMontageSO asset = assets[i];
                if (asset != null && asset is not AnimSequenceSO && !unique.Contains(asset))
                    unique.Add(asset);
            }

            return unique.ToArray();
        }
        private static T[] RemoveMissingAndDuplicateAssets<T>(T[] assets) where T : UnityEngine.Object
        {
            if (assets == null || assets.Length == 0)
                return Array.Empty<T>();

            var unique = new List<T>(assets.Length);
            for (int i = 0; i < assets.Length; i++)
            {
                T asset = assets[i];
                if (asset != null && !unique.Contains(asset))
                    unique.Add(asset);
            }

            return unique.ToArray();
        }
#endif
    }
}