using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PJDev.DevelopKit.Framework.AbilitySystem.Runtime
{
    [Serializable]
    public sealed class AbilityInputBinding
    {
        [SerializeField] private InputActionReference input = null;
        [SerializeField] private AbilitySO ability = null;

        public InputActionReference Input => input;
        public AbilitySO Ability => ability;
    }

    [CreateAssetMenu(fileName = "SO_AbilityInputBridge", menuName = "PJDev/Ability System/Input Bridge")]
    public sealed class AbilityInputBridgeSO : ScriptableObject
    {
        [SerializeField] private List<AbilityInputBinding> bindings = new();

        private readonly Dictionary<InputAction, Action<InputAction.CallbackContext>> callbacks = new();
        private ObjectAbilitySystem abilitySystem;
        private bool isBound;

        internal void Initialize(ObjectAbilitySystem system)
        {
            abilitySystem = system;
        }

        public void Bind()
        {
            if (isBound || abilitySystem == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                AbilityInputBinding binding = bindings[i];
                InputAction action = binding?.Input?.action;
                AbilitySO ability = binding?.Ability;
                if (action == null || ability == null || callbacks.ContainsKey(action))
                    continue;

                Action<InputAction.CallbackContext> callback =
                    context => abilitySystem.TryActivateAbility(ability, context);
                callbacks.Add(action, callback);
                action.performed += callback;
                action.Enable();
            }

            isBound = true;
        }

        public void Unbind()
        {
            if (!isBound)
                return;

            foreach (KeyValuePair<InputAction, Action<InputAction.CallbackContext>> pair in callbacks)
                pair.Key.performed -= pair.Value;

            callbacks.Clear();
            isBound = false;
        }

        internal AbilityInputBridgeSO CreateRuntimeInstance()
        {
            AbilityInputBridgeSO instance = Instantiate(this);
            instance.name = name;
            return instance;
        }
    }
}