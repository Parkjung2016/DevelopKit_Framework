using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Skddkkkk.DevelopKit.Framework.AbilitySystem.Runtime
{
    [Serializable]
    public class AbilityInputBridgeInfo
    {
        public InputActionReference activationInput;
        public AbilitySO abilityToActivate;
    }

    [CreateAssetMenu(fileName = "SO_AbilityInputBridge", menuName = "SO/GameAbility/Config/InputBridge")]
    public class AbilityInputBridgeSO : ScriptableObject
    {
        [SerializeField] private InputActionAsset playerInput;
        [SerializeField] private List<AbilityInputBridgeInfo> abilityInputBridgeInfoList;

        private Dictionary<string, AbilityInputBridgeInfo> abilityInputBridgeInfoDictionary;
        private AbilitySystem abilitySystemCompo;

        public void Init(AbilitySystem abilitySystem)
        {
            abilityInputBridgeInfoDictionary = new();
            foreach (var abilityInputBridgeInfo in abilityInputBridgeInfoList)
            {
                abilityInputBridgeInfoDictionary.Add(abilityInputBridgeInfo.activationInput.action.name,
                    abilityInputBridgeInfo);
            }

            abilitySystemCompo = abilitySystem;
            SubscribeEvent();
        }

        private void HandleAbilityInputPerformed(InputAction.CallbackContext context)
        {
            AbilityInputBridgeInfo inputBridgeInfo = abilityInputBridgeInfoDictionary[context.action.name];
            abilitySystemCompo.TryActivateAbility(inputBridgeInfo.abilityToActivate, context);
        }


        public void SubscribeEvent()
        {
            foreach (var info in abilityInputBridgeInfoList)
            {
                playerInput.FindAction(info.activationInput.action.name).performed +=
                    HandleAbilityInputPerformed;
            }
        }

        public void UnSubscribeEvent()
        {
            foreach (var info in abilityInputBridgeInfoList)
            {
                playerInput.FindAction(info.activationInput.action.name).performed -=
                    HandleAbilityInputPerformed;
            }
        }
    }
}