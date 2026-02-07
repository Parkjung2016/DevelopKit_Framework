using Skddkkkk.DevelopKit.BasicTemplate.Runtime;
using System;
using BandoWare.GameplayTags;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skddkkkk.DevelopKit.Framework.AbilitySystem.Runtime
{
    public abstract class AbilitySO : ScriptableObject
    {
        public event Action OnAbilityActivated;
        public event Action OnAbilityEnded;
        [field: SerializeField] public GameplayTag GrantedGamePlayTag { get; private set; }
        [field: SerializeField] public GameplayTag BlockedGamePlayTags { get; private set; }
        [field: SerializeField] public bool ActivateAbilityWhenRegistered { get; private set; }

        [field: ReadOnly] public bool IsActivating { get; private set; }


        public virtual void RegisteredAbility(IAbilitySystemOwner owner)
        {
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