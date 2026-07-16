using System;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    /// <summary>Ability 활성화 시 실행되는 확장 가능한 효과입니다.</summary>
    [Serializable]
    public abstract class AbilityEffect
    {
        public virtual bool CanApply(in AbilityContext context, out string failureReason)
        {
            failureReason = null;
            return true;
        }

        public abstract void Apply(in AbilityContext context);

        public virtual void Remove(in AbilityContext context)
        {
        }

        public virtual bool RemoveWhenAbilityEnds => false;
    }
}
