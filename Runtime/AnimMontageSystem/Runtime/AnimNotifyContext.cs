using System;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public readonly struct AnimNotifyContext
    {
        public AnimNotifyContext(
            GameObject owner,
            Animator animator,
            AnimMontageSO montage,
            float montageTime,
            float deltaTime)
        {
            Owner = owner;
            Animator = animator;
            Montage = montage;
            MontageTime = montageTime;
            DeltaTime = deltaTime;
        }

        public GameObject Owner { get; }
        public Animator Animator { get; }
        public AnimMontageSO Montage { get; }
        public float MontageTime { get; }
        public float DeltaTime { get; }
    }
}
