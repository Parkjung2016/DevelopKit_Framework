using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    /// <summary>실행 조건, 비용, 효과와 사용자 동작을 정의하는 Ability 에셋입니다.</summary>
    public abstract class AbilitySO : ScriptableObject
    {
        [SerializeField] private GameplayTag abilityTag = default;
        [SerializeField] private GameplayTag blockedByTag = default;
        [SerializeField] private bool activateWhenGranted = false;
        [SerializeField] private List<AbilityStatCost> statCosts = new();
        [SerializeReference] private List<AbilityEffect> effects = new();

        private ObjectAbilitySystem system;
        private IAbilitySystemOwner owner;
        private AbilityContext activeContext;

        public event Action<AbilityContext> OnActivated;
        public event Action<AbilityContext> OnEnded;

        public GameplayTag AbilityTag => abilityTag;
        public GameplayTag BlockedByTag => blockedByTag;
        public bool ActivateWhenGranted => activateWhenGranted;
        public IReadOnlyList<AbilityStatCost> StatCosts => statCosts;
        public IReadOnlyList<AbilityEffect> Effects => effects;
        public bool IsActive { get; private set; }
        public ObjectAbilitySystem System => system;
        public IAbilitySystemOwner Owner => owner;
        internal AbilityContext ActiveContext => activeContext;

        internal void Register(ObjectAbilitySystem abilitySystem, IAbilitySystemOwner abilityOwner)
        {
            system = abilitySystem;
            owner = abilityOwner;
            OnRegistered();
        }

        internal void Unregister()
        {
            OnUnregistered();
            system = null;
            owner = null;
            OnActivated = null;
            OnEnded = null;
        }

        internal bool CanStart(in AbilityContext context, out string failureReason)
        {
            if (IsActive)
            {
                failureReason = "Ability is already active.";
                return false;
            }

            if (!CanPayCosts(context.GetStats(AbilityStatTarget.Self), out failureReason))
                return false;

            for (int i = 0; i < effects.Count; i++)
            {
                AbilityEffect effect = effects[i];
                if (effect != null && !effect.CanApply(context, out failureReason))
                    return false;
            }

            return CanActivate(context, out failureReason);
        }

        internal void ActivateInternal(in AbilityContext context, InputAction.CallbackContext? inputContext)
        {
            activeContext = context;
            IsActive = true;
            PayCosts(context.GetStats(AbilityStatTarget.Self));

            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Apply(context);

            OnActivated?.Invoke(context);

            if (inputContext.HasValue)
                OnActivate(context, inputContext.Value);
            else
                OnActivate(context);
        }

        internal void EndInternal()
        {
            if (!IsActive)
                return;

            AbilityContext context = activeContext;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                AbilityEffect effect = effects[i];
                if (effect != null && effect.RemoveWhenAbilityEnds)
                    effect.Remove(context);
            }

            IsActive = false;
            activeContext = default;
            OnEnd(context);
            OnEnded?.Invoke(context);
        }

        public void EndAbility()
        {
            if (system != null)
                system.EndAbility(this);
            else
                EndInternal();
        }

        public AbilitySO CreateRuntimeInstance()
        {
            AbilitySO instance = Instantiate(this);
            instance.name = name;
            return instance;
        }

        protected virtual bool CanActivate(in AbilityContext context, out string failureReason)
        {
            failureReason = null;
            return true;
        }

        protected virtual void OnRegistered()
        {
        }

        protected virtual void OnUnregistered()
        {
        }

        protected virtual void OnActivate(in AbilityContext context)
        {
        }

        protected virtual void OnActivate(in AbilityContext context, InputAction.CallbackContext inputContext) =>
            OnActivate(context);

        protected virtual void OnEnd(in AbilityContext context)
        {
        }

        private bool CanPayCosts(StatCollection statCollection, out string failureReason)
        {
            for (int i = 0; i < statCosts.Count; i++)
            {
                AbilityStatCost cost = statCosts[i];
                if (cost == null || !cost.HasCost)
                    continue;

                if (!cost.TryGetStat(statCollection, out Stat stat))
                {
                    failureReason = $"Cost Stat '{cost.Stat?.StatName ?? "<None>"}' was not found.";
                    return false;
                }

                float totalCost = 0f;
                for (int j = 0; j < statCosts.Count; j++)
                {
                    AbilityStatCost other = statCosts[j];
                    if (other != null &&
                        other.HasCost &&
                        other.TryGetStat(statCollection, out Stat otherStat) &&
                        ReferenceEquals(stat, otherStat))
                    {
                        totalCost += other.CalculateCost(otherStat);
                    }
                }

                if (stat.BaseValue - totalCost < stat.MinValue)
                {
                    failureReason = $"Not enough {stat.StatName}.";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        private void PayCosts(StatCollection sourceStats)
        {
            for (int i = 0; i < statCosts.Count; i++)
            {
                AbilityStatCost cost = statCosts[i];
                if (cost == null || !cost.HasCost || !cost.TryGetStat(sourceStats, out Stat stat))
                    continue;

                bool alreadyHandled = false;
                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    AbilityStatCost previous = statCosts[previousIndex];
                    if (previous != null &&
                        previous.TryGetStat(sourceStats, out Stat previousStat) &&
                        ReferenceEquals(stat, previousStat))
                    {
                        alreadyHandled = true;
                        break;
                    }
                }

                if (alreadyHandled)
                    continue;

                float totalCost = 0f;
                for (int costIndex = i; costIndex < statCosts.Count; costIndex++)
                {
                    AbilityStatCost groupedCost = statCosts[costIndex];
                    if (groupedCost != null &&
                        groupedCost.HasCost &&
                        groupedCost.TryGetStat(sourceStats, out Stat groupedStat) &&
                        ReferenceEquals(stat, groupedStat))
                    {
                        totalCost += groupedCost.CalculateCost(stat);
                    }
                }

                stat.AddBaseValue(-totalCost);
            }
        }
    }
}