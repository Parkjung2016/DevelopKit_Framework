using System;
using System.Collections.Generic;
using PJDev.DevelopKit.Framework.GameplayTagSystem.Runtime;
using PJDev.DevelopKit.Framework.StatSystem.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    [AddComponentMenu("PJDev/Framework/Object Ability System")]
    public sealed class ObjectAbilitySystem : MonoBehaviour
    {
        [Header("Setup")] [SerializeField] private AbilitySetupSO abilitySetup = null;
        [SerializeField] private AbilityInputBridgeSO inputBridge = null;

        [Header("Owner Components")] [SerializeField]
        private ObjectGameplayTagContainer tags = null;

        [SerializeField] private ObjectStatSystem stats = null;

        private readonly Dictionary<GameplayTag, AbilitySO> abilities = new();
        private IAbilitySystemOwner owner;
        private IInputActionCollection2 inputActions;
        private AbilityInputBridgeSO runtimeInputBridge;

        public event Action<AbilityContext> OnAbilityActivated;
        public event Action<AbilityContext> OnAbilityEnded;

        public ObjectGameplayTagContainer Tags => tags;
        public ObjectStatSystem Stats => stats;
        public IAbilitySystemOwner Owner => owner;
        public int AbilityCount => abilities.Count;
        public bool IsInitialized { get; private set; }
        

        private void OnEnable()
        {
            if (IsInitialized)
                runtimeInputBridge?.Bind();
        }

        private void OnDisable()
        {
            runtimeInputBridge?.Unbind();
            EndAllAbilities();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        public void Initialize(IAbilitySystemOwner abilityOwner = null)
        {
            if (IsInitialized)
                return;

            owner = abilityOwner;
            tags ??= GetComponent<ObjectGameplayTagContainer>();
            stats ??= GetComponent<ObjectStatSystem>();
            if (stats != null && !stats.IsInitialized)
                stats.Initialize();

            IsInitialized = true;
            GiveSetupAbilities();

            if (inputBridge != null)
            {
                runtimeInputBridge = inputBridge.CreateRuntimeInstance();
                runtimeInputBridge.Initialize(this, inputActions);
                if (isActiveAndEnabled)
                    runtimeInputBridge.Bind();
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            runtimeInputBridge?.Unbind();
            EndAllAbilities();

            foreach (AbilitySO ability in abilities.Values)
            {
                ability.Unregister();
                if (ability != null)
                    Destroy(ability);
            }

            abilities.Clear();

            if (runtimeInputBridge != null)
                Destroy(runtimeInputBridge);

            runtimeInputBridge = null;
            owner = null;
            IsInitialized = false;
            OnAbilityActivated = null;
            OnAbilityEnded = null;
        }

        /// <summary>
        /// 외부에서 생성하고 관리하는 Input Actions 컬렉션을 연결합니다.
        /// <code>abilitySystem.SetInputActions(inputManager.Input);</code>
        /// </summary>
        public void SetInputActions(IInputActionCollection2 actions)
        {
            inputActions = actions;
            runtimeInputBridge?.SetInputActions(actions);
        }

        public bool TryGiveAbility(AbilitySO abilityAsset)
        {
            if (abilityAsset == null || !abilityAsset.AbilityTag.IsValid)
                return false;
            if (abilities.ContainsKey(abilityAsset.AbilityTag))
                return false;

            AbilitySO ability = abilityAsset.CreateRuntimeInstance();
            abilities.Add(ability.AbilityTag, ability);
            ability.Register(this, owner);

            if (ability.ActivateWhenGranted)
                TryActivateInternal(ability, stats.Stats, null);

            return true;
        }

        public bool TryRemoveAbility(GameplayTag abilityTag)
        {
            if (!abilities.TryGetValue(abilityTag, out AbilitySO ability))
                return false;

            if (ability.IsActive)
                EndAbility(ability);

            abilities.Remove(abilityTag);
            ability.Unregister();
            Destroy(ability);
            return true;
        }

        public bool TryRemoveAbility(AbilitySO ability) =>
            ability != null && TryRemoveAbility(ability.AbilityTag);

        public bool TryActivateAbility(AbilitySO ability, ObjectStatSystem targetStats = null)
        {
            if (!TryGetAbility(ability, out AbilitySO runtimeAbility))
                return false;

            return TryActivateInternal(runtimeAbility, targetStats?.Stats, null);
        }

        public bool TryActivateAbility(
            AbilitySO ability,
            InputAction.CallbackContext inputContext,
            StatCollection targetStatCollection = null)
        {
            if (!TryGetAbility(ability, out AbilitySO runtimeAbility))
                return false;

            return TryActivateInternal(runtimeAbility, targetStatCollection, inputContext);
        }

        public bool TryActivateAbility(GameplayTag abilityTag, StatCollection targetStatCollection = null)
        {
            return abilities.TryGetValue(abilityTag, out AbilitySO ability) &&
                   TryActivateInternal(ability, targetStatCollection, null);
        }

        public bool EndAbility(AbilitySO ability)
        {
            if (ability == null || !ability.IsActive ||
                !abilities.TryGetValue(ability.AbilityTag, out AbilitySO registered))
                return false;
            if (!ReferenceEquals(ability, registered))
                return false;

            AbilityContext context = ability.ActiveContext;
            if (ability.AbilityTag.IsValid && tags != null)
                tags.Container.RemoveTag(ability.AbilityTag);

            ability.EndInternal();
            OnAbilityEnded?.Invoke(context);
            return true;
        }

        public void EndAllAbilities()
        {
            foreach (AbilitySO ability in abilities.Values)
            {
                if (ability.IsActive)
                    EndAbility(ability);
            }
        }

        public bool CanActivateAbility(AbilitySO ability, StatCollection targetStatCollection = null)
        {
            if (!TryGetAbility(ability, out AbilitySO runtimeAbility))
                return false;

            return CanActivateInternal(runtimeAbility, targetStatCollection, out _);
        }

        public bool HasAbility(AbilitySO ability) =>
            ability != null && abilities.ContainsKey(ability.AbilityTag);

        public bool HasAbility(GameplayTag abilityTag) =>
            abilities.ContainsKey(abilityTag);

        public bool TryGetAbility(GameplayTag abilityTag, out AbilitySO ability) =>
            abilities.TryGetValue(abilityTag, out ability);

        public bool TryGetAbility<T>(GameplayTag abilityTag, out T ability) where T : AbilitySO
        {
            if (abilities.TryGetValue(abilityTag, out AbilitySO found) && found is T typed)
            {
                ability = typed;
                return true;
            }

            ability = null;
            return false;
        }

        public bool TryGetAbility(AbilitySO abilityAsset, out AbilitySO ability)
        {
            ability = null;
            return abilityAsset != null &&
                   abilities.TryGetValue(abilityAsset.AbilityTag, out ability);
        }

        private bool TryActivateInternal(
            AbilitySO ability,
            StatCollection targetStatCollection,
            InputAction.CallbackContext? inputContext)
        {
            if (!CanActivateInternal(ability, targetStatCollection, out AbilityContext context))
                return false;

            if (ability.AbilityTag.IsValid && tags != null)
                tags.Container.AddTag(ability.AbilityTag);

            ability.ActivateInternal(context, inputContext);
            OnAbilityActivated?.Invoke(context);
            return true;
        }

        private bool CanActivateInternal(
            AbilitySO ability,
            StatCollection targetStatCollection,
            out AbilityContext context)
        {
            context = default;
            if (!IsInitialized || ability == null || ability.IsActive)
                return false;

            if (tags != null && tags.Container.HasAny(ability.BlockedByTags))
            {
                return false;
            }

            context = new AbilityContext(this, ability, owner, stats.Stats,
                targetStatCollection ?? stats.Stats);
            return ability.CanStart(context, out _);
        }

        private void GiveSetupAbilities()
        {
            if (abilitySetup == null)
                return;

            foreach (AbilitySO ability in abilitySetup)
                TryGiveAbility(ability);
        }
    }
}