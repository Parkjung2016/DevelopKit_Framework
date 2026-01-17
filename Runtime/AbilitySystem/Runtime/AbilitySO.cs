using Skddkkkk.DevelopKit.BasicTemplate.Runtime;
using Skddkkkk.DevelopKit.Framework.GamePlayTagSystem.Runtime;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skddkkkk.DevelopKit.Framework.AbilitySystem.Runtime
{
    public abstract class AbilitySO : ScriptableObject
    {
        public event Action OnAbilityActivated;
        public event Action OnAbilityEnded;
        [field: SerializeField] public GamePlayTagEnum GrantedGamePlayTag { get; private set; }
        [field: SerializeField] public GamePlayTagEnum BlockedGamePlayTags { get; private set; }
        [field: SerializeField] public bool ActivateAbilityWhenRegistered { get; private set; }

        [field: ReadOnly()] public bool IsActivating { get; private set; }

        protected IAbilitySystemOwner owner;


        public virtual void RegisteredAbility(IAbilitySystemOwner owner)
        {
            this.owner = owner;
            OnAbilityActivated = null;
            OnAbilityEnded = null;
        }

        public virtual void UnRegisteredAbility()
        {
        }

        public virtual void ActivateAbility()
        {
            IsActivating = true;
            OnAbilityActivated?.Invoke();
        }

        public virtual void ActivateAbility(InputAction.CallbackContext context)
        {
            ActivateAbility();
        }

        public virtual void EndAbility()
        {
            IsActivating = false;
            OnAbilityEnded?.Invoke();
        }

        public AbilitySO Clone()
        {
            AbilitySO clonedAbility = Instantiate(this);
            clonedAbility.name = clonedAbility.name.Replace("(Clone)", "");
            return clonedAbility;
        }
    }
}