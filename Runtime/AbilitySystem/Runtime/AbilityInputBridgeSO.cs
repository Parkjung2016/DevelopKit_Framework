using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
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
        [Header("외부 주입이 없을 경우 사용할 InputActionAsset")] [SerializeField]
        private InputActionAsset inputAsset;

        [SerializeField] private List<AbilityInputBridgeInfo> abilityInputBridgeInfoList;

        private AbilitySystem abilitySystemCompo;
        private IInputActionCollection2 inputActionCollection;

        private readonly Dictionary<InputAction, Action<InputAction.CallbackContext>> callbackMap = new();

        private bool isBound;

        public void Init(AbilitySystem abilitySystem)
        {
            abilitySystemCompo = abilitySystem;

            ResolveInputCollection();
            Bind();
        }

        public void SetInputActionCollection(IInputActionCollection2 collection)
        {
            inputActionCollection = collection;
        }

        private void ResolveInputCollection()
        {
            inputActionCollection ??= inputAsset;
        }

        public void Bind()
        {
            if (isBound)
                return;

            foreach (var info in abilityInputBridgeInfoList)
            {
                var action = info.activationInput?.action;
                if (action == null)
                    continue;

                action.Enable();

                var callback = CreateCallback(info);

                callbackMap[action] = callback;
                action.performed += callback;
            }

            isBound = true;
        }

        public void Unbind()
        {
            if (isBound == false)
                return;

            foreach (var pair in callbackMap)
            {
                var action = pair.Key;
                var callback = pair.Value;

                action.performed -= callback;
                action.Disable();
            }

            callbackMap.Clear();
            isBound = false;
        }

        private Action<InputAction.CallbackContext> CreateCallback(AbilityInputBridgeInfo info)
        {
            return (ctx) => { abilitySystemCompo.TryActivateAbility(info.abilityToActivate, ctx); };
        }
    }
}