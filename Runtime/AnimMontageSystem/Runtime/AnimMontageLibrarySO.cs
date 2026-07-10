using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [CreateAssetMenu(fileName = "MontageLibrary_", menuName = "PJDev/Animation/Montage Library")]
    public sealed class AnimMontageLibrarySO : ScriptableObject
    {
        [SerializeField] private GameObject previewModel;
        [SerializeField] private AnimMontageSO[] montages = Array.Empty<AnimMontageSO>();

        public GameObject PreviewModel => previewModel;
        public IReadOnlyList<AnimMontageSO> Montages => montages ?? Array.Empty<AnimMontageSO>();

        public bool Contains(AnimMontageSO montage)
        {
            if (montage == null || montages == null)
                return false;

            for (int i = 0; i < montages.Length; i++)
            {
                if (montages[i] == montage)
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (montages == null || montages.Length == 0)
                return;

            var unique = new List<AnimMontageSO>(montages.Length);
            for (int i = 0; i < montages.Length; i++)
            {
                AnimMontageSO montage = montages[i];
                if (montage != null && !unique.Contains(montage))
                    unique.Add(montage);
            }

            if (unique.Count != montages.Length)
                montages = unique.ToArray();
        }
#endif
    }
}
