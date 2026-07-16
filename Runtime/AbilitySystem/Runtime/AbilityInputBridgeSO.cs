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
        private readonly HashSet<InputAction> actionsEnabledByBridge = new();
        private ObjectAbilitySystem abilitySystem;
        private IInputActionCollection2 inputActions;
        private bool isBound;

        internal void Initialize(ObjectAbilitySystem system, IInputActionCollection2 actions)
        {
            abilitySystem = system;
            inputActions = actions;
        }

        internal void SetInputActions(IInputActionCollection2 actions)
        {
            bool shouldRebind = isBound;
            if (shouldRebind)
                Unbind();

            inputActions = actions;
            if (shouldRebind)
                Bind();
        }

        public void Bind()
        {
            if (isBound || abilitySystem == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                AbilityInputBinding binding = bindings[i];
                InputAction action = ResolveAction(binding?.Input);
                AbilitySO ability = binding?.Ability;
                if (action == null || ability == null || callbacks.ContainsKey(action))
                    continue;

                Action<InputAction.CallbackContext> callback =
                    context => abilitySystem.TryActivateAbility(ability, context);
                callbacks.Add(action, callback);
                action.performed += callback;

                if (!action.enabled)
                {
                    action.Enable();
                    actionsEnabledByBridge.Add(action);
                }
            }

            isBound = true;
        }

        public void Unbind()
        {
            if (!isBound)
                return;

            foreach (KeyValuePair<InputAction, Action<InputAction.CallbackContext>> pair in callbacks)
                pair.Key.performed -= pair.Value;

            foreach (InputAction action in actionsEnabledByBridge)
                action.Disable();

            callbacks.Clear();
            actionsEnabledByBridge.Clear();
            isBound = false;
        }

        private InputAction ResolveAction(InputActionReference reference)
        {
            InputAction configuredAction = reference?.action;
            if (configuredAction == null || inputActions == null)
                return configuredAction;

            InputAction namedMatch = null;
            string configuredMapName = configuredAction.actionMap?.name;
            foreach (InputAction candidate in inputActions)
            {
                if (candidate.id == configuredAction.id)
                    return candidate;

                if (namedMatch == null &&
                    string.Equals(candidate.name, configuredAction.name, StringComparison.Ordinal) &&
                    string.Equals(candidate.actionMap?.name, configuredMapName, StringComparison.Ordinal))
                {
                    namedMatch = candidate;
                }
            }

            return namedMatch;
        }

        internal AbilityInputBridgeSO CreateRuntimeInstance()
        {
            AbilityInputBridgeSO instance = Instantiate(this);
            instance.name = name;
            return instance;
        }
    }
}