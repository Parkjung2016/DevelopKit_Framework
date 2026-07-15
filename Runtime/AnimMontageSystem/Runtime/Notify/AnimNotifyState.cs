using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    [System.Serializable]
    public abstract class AnimNotifyState : AnimNotifyBase
    {
        public virtual string DisplayName => GetType().Name;
        public virtual Color EditorColor => new(1f, 0.65f, 0.2f, 1f);
        public virtual float DefaultDuration => 0.2f;

        public virtual void OnBegin(AnimNotifyContext context)
        {
        }

        public virtual void OnTick(AnimNotifyContext context, float deltaTime)
        {
        }

        public virtual void OnEnd(AnimNotifyContext context)
        {
        }
    }
}
