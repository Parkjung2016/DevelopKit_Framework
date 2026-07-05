using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public abstract class AnimNotifyStateSO : ScriptableObject
    {
        public virtual string DisplayName => name;
        public virtual Color EditorColor => new(1f, 0.65f, 0.2f, 1f);

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
