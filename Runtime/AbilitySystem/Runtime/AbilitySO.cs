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
        [SerializeField] private GameplayTagContainer blockedByTags = new();
        [SerializeField] private bool activateWhenGranted = false;
        [SerializeField] private List<AbilityStatCost> statCosts = new();
        [SerializeReference] private List<AbilityEffect> effects = new();

        private ObjectAbilitySystem system;
        private UnityEngine.Object owner;
        private AbilityContext activeContext;
        [NonSerialized] private List<AbilityCostGroup> costGroups;

        public event Action<AbilityContext> OnActivated;
        public event Action<AbilityContext> OnEnded;

        public GameplayTag AbilityTag => abilityTag;
        public GameplayTagContainer BlockedByTags => blockedByTags;
        public bool ActivateWhenGranted => activateWhenGranted;
        public IReadOnlyList<AbilityStatCost> StatCosts => statCosts;
        public IReadOnlyList<AbilityEffect> Effects => effects;
        public bool IsActive { get; private set; }
        public ObjectAbilitySystem System => system;
        public UnityEngine.Object Owner => owner;
        internal AbilityContext ActiveContext => activeContext;

        internal void Register(ObjectAbilitySystem abilitySystem, UnityEngine.Object abilityOwner)
        {
            system = abilitySystem;
            owner = abilityOwner;
            BuildCostGroups();
            OnRegistered();
        }

        internal void Unregister()
        {
            OnUnregistered();
            system = null;
            owner = null;
            costGroups = null;
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
            EnsureCostGroups();
            for (int i = 0; i < costGroups.Count; i++)
            {
                AbilityCostGroup cost = costGroups[i];
                if (statCollection == null || !statCollection.TryGetStat(cost.StatId, out Stat stat))
                {
                    failureReason = $"Cost Stat '{cost.StatId.Value}' was not found.";
                    return false;
                }

                if (stat.BaseValue - cost.Calculate(stat) < stat.MinValue)
                {
                    failureReason = $"Not enough {stat.Id.Value}.";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        private void PayCosts(StatCollection statCollection)
        {
            EnsureCostGroups();
            for (int i = 0; i < costGroups.Count; i++)
            {
                AbilityCostGroup cost = costGroups[i];
                if (statCollection != null && statCollection.TryGetStat(cost.StatId, out Stat stat))
                    stat.AddBaseValue(-cost.Calculate(stat));
            }
        }

        private void EnsureCostGroups()
        {
            if (costGroups == null)
                BuildCostGroups();
        }

        private void BuildCostGroups()
        {
            int capacity = statCosts?.Count ?? 0;
            costGroups = new List<AbilityCostGroup>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                AbilityStatCost cost = statCosts[i];
                if (cost == null || !cost.HasCost)
                    continue;

                int groupIndex = -1;
                for (int j = 0; j < costGroups.Count; j++)
                {
                    if (costGroups[j].StatId.Equals(cost.StatId))
                    {
                        groupIndex = j;
                        break;
                    }
                }

                AbilityCostGroup group = groupIndex >= 0
                    ? costGroups[groupIndex]
                    : new AbilityCostGroup(cost.StatId);
                group.Add(cost);

                if (groupIndex >= 0)
                    costGroups[groupIndex] = group;
                else
                    costGroups.Add(group);
            }
        }

        private struct AbilityCostGroup
        {
            private float amount;
            private float baseValuePercent;
            private float maxValuePercent;

            public AbilityCostGroup(StatId statId)
            {
                StatId = statId;
                amount = 0f;
                baseValuePercent = 0f;
                maxValuePercent = 0f;
            }

            public StatId StatId { get; }

            public void Add(AbilityStatCost cost)
            {
                amount += cost.Amount;
                if (cost.PercentBase == StatCostPercentBase.MaxValue)
                    maxValuePercent += cost.Percent;
                else
                    baseValuePercent += cost.Percent;
            }

            public float Calculate(Stat stat) =>
                Math.Max(0f,
                    amount +
                    stat.BaseValue * baseValuePercent * 0.01f +
                    stat.MaxValue * maxValuePercent * 0.01f);
        }
    }
}