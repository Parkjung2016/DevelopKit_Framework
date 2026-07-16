using PJDev.DevelopKit.Framework.StatSystem.Runtime;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    /// <summary>Ability 한 번의 실행에 필요한 객체와 스탯 정보를 전달합니다.</summary>
    public readonly struct AbilityContext
    {
        public AbilityContext(
            ObjectAbilitySystem system,
            AbilitySO ability,
            IAbilitySystemOwner owner,
            StatCollection sourceStats,
            StatCollection targetStats)
        {
            System = system;
            Ability = ability;
            Owner = owner;
            SourceStats = sourceStats;
            TargetStats = targetStats ?? sourceStats;
        }

        public ObjectAbilitySystem System { get; }
        public AbilitySO Ability { get; }
        public IAbilitySystemOwner Owner { get; }
        private StatCollection SourceStats { get; }
        private StatCollection TargetStats { get; }

        public StatCollection GetStats(AbilityStatTarget target) =>
            target == AbilityStatTarget.Self ? SourceStats : TargetStats;
    }

    public enum AbilityStatTarget
    {
        Self,
        Target
    }
}
