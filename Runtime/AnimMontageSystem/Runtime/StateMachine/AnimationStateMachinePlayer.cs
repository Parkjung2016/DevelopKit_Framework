using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    internal readonly struct AnimationStateMachinePose
    {
        public AnimationStateMachinePose(
            AnimationClip currentClip,
            double currentTime,
            float currentWeight,
            AnimationClip nextClip,
            double nextTime,
            float nextWeight)
        {
            CurrentClip = currentClip;
            CurrentTime = currentTime;
            CurrentWeight = currentWeight;
            NextClip = nextClip;
            NextTime = nextTime;
            NextWeight = nextWeight;
        }

        public AnimationClip CurrentClip { get; }
        public double CurrentTime { get; }
        public float CurrentWeight { get; }
        public AnimationClip NextClip { get; }
        public double NextTime { get; }
        public float NextWeight { get; }
    }

    /// <summary>AnimStateMachineSO를 평가하고 Sequence를 Animator에 출력합니다.</summary>
    [AddComponentMenu("PJDev/Animation/Animation State Machine Player")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class AnimationStateMachinePlayer : MonoBehaviour, IAnimNotifyHandler
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimStateMachineSO stateMachine;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool createAnimatorIfMissing = true;

        private readonly Dictionary<int, ParameterValue> values = new();
        private readonly Dictionary<string, AnimStateParameter> parameterByName = new(StringComparer.Ordinal);
        private readonly Dictionary<AnimStateCondition, OwnerConditionBinding> ownerConditions = new();
        private readonly HashSet<int> triggers = new();
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationPlayableOutput output;
        private AnimationClipPlayable currentPlayable;
        private AnimationClipPlayable nextPlayable;
        private RuntimeAnimatorController previousController;
        private AnimSequenceState currentState;
        private AnimSequenceState nextState;
        private AnimStateTransition activeTransition;
        private float currentTime;
        private float nextTime;
        private float transitionTime;
        private NotifyCursor currentNotify = new();
        private NotifyCursor nextNotify = new();
        private readonly AnimStateTransition[] transitionPath = new AnimStateTransition[16];
        private int transitionPathCount;

        public Animator Animator => animator;
        public AnimStateMachineSO StateMachine => stateMachine;
        public AnimSequenceState CurrentState => currentState;
        public AnimSequenceState NextState => nextState;
        public AnimStateTransition ActiveTransition => activeTransition;
        public float StateTime => currentTime;
        public float StateNormalizedTime => GetNormalizedTime(currentState, currentTime);
        public float TransitionProgress => activeTransition == null
            ? 0f
            : activeTransition.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionTime / activeTransition.Duration);
        public bool IsTransitioning => activeTransition != null;
        public bool PlayOnEnable => playOnEnable;
        public bool CreateAnimatorIfMissing => createAnimatorIfMissing;
        public bool IsReady => graph.IsValid() && animator != null && stateMachine != null;

        /// <summary>State가 바뀌기 시작할 때 호출됩니다.</summary>
        public event Action<AnimSequenceState> OnStateEnter;

        /// <summary>State에서 완전히 빠져나왔을 때 호출됩니다.</summary>
        public event Action<AnimSequenceState> OnStateExit;

        /// <summary>현재 Sequence의 Notify가 실행될 때 호출됩니다.</summary>
        public event Action<AnimNotify, AnimNotifyContext> OnNotify;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = gameObject.AddComponent<Animator>();
        }

        private void OnEnable()
        {
            BindNotifyCursor(currentNotify);
            BindNotifyCursor(nextNotify);
            EnsureAnimator();
            Build();
            if (playOnEnable)
                PlayDefault();
        }

        private void OnDisable() => DisposeGraph();

        private void Update()
        {
            if (!graph.IsValid() || currentState == null)
                return;

            float deltaTime = animator != null && animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            float previousCurrentTime = currentTime;
            currentTime += deltaTime * currentState.Speed;
            SyncPlayableTime(currentPlayable, currentState, previousCurrentTime, currentTime);

            if (activeTransition == null)
            {
                DispatchNotifies(currentState, currentNotify, previousCurrentTime, currentTime, 1f);
                if (TryFindTransition(out AnimStateTransition transition, out AnimSequenceState destination))
                    BeginTransition(transition, destination);
                return;
            }

            float previousNextTime = nextTime;
            nextTime += deltaTime * nextState.Speed;
            SyncPlayableTime(nextPlayable, nextState, previousNextTime, nextTime);
            transitionTime += deltaTime;
            float blend = activeTransition.Duration <= 0f
                ? 1f
                : Mathf.Clamp01(transitionTime / activeTransition.Duration);
            mixer.SetInputWeight(0, 1f - blend);
            mixer.SetInputWeight(1, blend);
            DispatchNotifies(currentState, currentNotify, previousCurrentTime, currentTime, 1f - blend);
            DispatchNotifies(nextState, nextNotify, previousNextTime, nextTime, blend);
            if (blend >= 1f)
                CompleteTransition();
        }

        /// <summary>State Machine과 Parameter 기본값을 다시 읽습니다.</summary>
        public void Build()
        {
            DisposeGraph();
            BuildParameters();
            BuildOwnerConditions();
            if (!EnsureAnimator() || stateMachine == null)
                return;

            previousController = animator.runtimeAnimatorController;
            animator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create($"{name} Animation State Machine");
            graph.SetTimeUpdateMode(animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? DirectorUpdateMode.UnscaledGameTime
                : DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output = AnimationPlayableOutput.Create(graph, "Animation State Machine", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        public void PlayDefault()
        {
            if (stateMachine != null)
                PlayState(stateMachine.DefaultNodeId);
        }

        /// <summary>자식에서 Animator를 찾고, 없으면 설정에 따라 자동으로 만듭니다.</summary>
        public bool EnsureAnimator()
        {
            if (animator != null)
                return true;
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null && createAnimatorIfMissing)
                animator = gameObject.AddComponent<Animator>();
            return animator != null;
        }

        /// <summary>애니메이션을 출력할 Animator를 바꾸고 그래프를 다시 구성합니다.</summary>
        public void SetAnimator(Animator value, bool rebuild = true)
        {
            if (animator == value)
                return;
            DisposeGraph();
            animator = value;
            if (!rebuild || !isActiveAndEnabled)
                return;
            Build();
            if (playOnEnable)
                PlayDefault();
        }
        /// <summary>사용할 State Machine을 바꾸고 선택적으로 기본 State를 재생합니다.</summary>
        public void SetStateMachine(AnimStateMachineSO value, bool playDefault = true)
        {
            if (stateMachine == value && graph.IsValid())
                return;

            stateMachine = value;
            Build();
            if (playDefault)
                PlayDefault();
        }

        public bool PlayState(string nodeId)
        {
            transitionPathCount = 0;
            if (stateMachine == null || !TryResolveNode(nodeId, 0, out AnimSequenceState state)
                || state?.Sequence?.Clip == null || !graph.IsValid())
                return false;

            StopCurrentState();
            currentState = state;
            currentTime = 0f;
            currentNotify.Reset();
            currentPlayable = CreatePlayable(state);
            graph.Connect(currentPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            currentNotify.Dispatch(state.Sequence, 0f, 0f, gameObject, animator, this, 1f);
            OnStateEnter?.Invoke(state);
            return true;
        }

        public bool PlayState(AnimSequenceSO sequence)
        {
            AnimSequenceState state = stateMachine?.FindState(sequence);
            return state != null && PlayState(state.Id);
        }

        public void SetFloat(string parameter, float value) => SetValue(parameter, value, 0, false);
        public float GetFloat(string parameter) => GetValue(parameter).Float;
        public void SetInt(string parameter, int value) => SetValue(parameter, 0f, value, false);
        public int GetInt(string parameter) => GetValue(parameter).Int;
        public void SetBool(string parameter, bool value) => SetValue(parameter, 0f, 0, value);
        public bool GetBool(string parameter) => GetValue(parameter).Bool;

        public void SetTrigger(string parameter)
        {
            if (TryGetParameter(parameter, AnimStateParameterType.Trigger, out AnimStateParameter definition))
                triggers.Add(definition.Hash);
        }

        public void ResetTrigger(string parameter)
        {
            if (parameterByName.TryGetValue(parameter, out AnimStateParameter definition))
                triggers.Remove(definition.Hash);
        }

        public bool TryHandle(AnimNotify notify, AnimNotifyContext context) => false;

        internal bool TryGetPose(out AnimationStateMachinePose pose)
        {
            if (!graph.IsValid() || currentState?.Sequence?.Clip == null)
            {
                pose = default;
                return false;
            }

            float currentWeight = activeTransition == null || !mixer.IsValid()
                ? 1f
                : mixer.GetInputWeight(0);
            float nextWeight = activeTransition != null && mixer.IsValid()
                ? mixer.GetInputWeight(1)
                : 0f;
            AnimationClip nextClip = nextState?.Sequence?.Clip;
            pose = new AnimationStateMachinePose(
                currentState.Sequence.Clip,
                GetSampleTime(currentState, currentTime),
                currentWeight,
                nextClip,
                nextClip != null ? GetSampleTime(nextState, nextTime) : 0d,
                nextClip != null ? nextWeight : 0f);
            return true;
        }

        internal void SetOutputWeight(float weight)
        {
            if (graph.IsValid() && output.IsOutputValid())
                output.SetWeight(Mathf.Clamp01(weight));
        }

        private static double GetSampleTime(AnimSequenceState state, float time)
        {
            float length = state?.Sequence?.Length ?? 0f;
            if (length <= 0f)
                return 0d;
            return state.Loop
                ? Mathf.Repeat(time, length)
                : Mathf.Min(time, length);
        }

        private void BuildParameters()
        {
            values.Clear();
            parameterByName.Clear();
            triggers.Clear();
            if (stateMachine == null)
                return;

            for (int i = 0; i < stateMachine.Parameters.Count; i++)
            {
                AnimStateParameter parameter = stateMachine.Parameters[i];
                if (parameter == null || string.IsNullOrEmpty(parameter.Name) || parameterByName.ContainsKey(parameter.Name))
                    continue;
                parameterByName.Add(parameter.Name, parameter);
                values[parameter.Hash] = new ParameterValue
                {
                    Float = parameter.DefaultFloat,
                    Int = parameter.DefaultInt,
                    Bool = parameter.DefaultBool
                };
            }
        }

        private void BuildOwnerConditions()
        {
            ownerConditions.Clear();
            if (stateMachine == null)
                return;

            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                IReadOnlyList<AnimStateCondition> conditions = stateMachine.Transitions[i].Conditions;
                for (int j = 0; j < conditions.Count; j++)
                {
                    AnimStateCondition condition = conditions[j];
                    if (condition == null || condition.Source != AnimStateConditionSource.OwnerMember)
                        continue;
                    OwnerConditionBinding binding = OwnerConditionBinding.Create(gameObject, condition);
                    if (binding != null)
                        ownerConditions[condition] = binding;
                }
            }
        }
        private static void SyncPlayableTime(
            AnimationClipPlayable playable,
            AnimSequenceState state,
            float previousTime,
            float currentTime)
        {
            if (!playable.IsValid() || state?.Sequence == null)
                return;
            float length = state.Sequence.Length;
            if (length <= 0f)
                return;

            if (!state.Loop)
            {
                if (currentTime < length)
                    return;
                playable.SetTime(length);
                playable.SetSpeed(0d);
                return;
            }

            int previousLoop = Mathf.FloorToInt(previousTime / length);
            int currentLoop = Mathf.FloorToInt(currentTime / length);
            if (currentLoop == previousLoop)
                return;
            playable.SetTime(Mathf.Repeat(currentTime, length));
            playable.SetSpeed(state.Speed);
            playable.SetDone(false);
        }
        private AnimationClipPlayable CreatePlayable(AnimSequenceState state)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, state.Sequence.Clip);
            playable.SetApplyFootIK(true);
            playable.SetDuration(Mathf.Max(0.0001f, state.Sequence.Length));
            playable.SetSpeed(state.Speed);
            playable.SetTime(0d);
            return playable;
        }

        private bool TryFindTransition(
            out AnimStateTransition selectedTransition,
            out AnimSequenceState destination)
        {
            if (TryFindTransitionFrom(currentState.Id, out selectedTransition, out destination))
                return true;

            for (int i = 0; i < stateMachine.Aliases.Count; i++)
            {
                AnimStateAlias alias = stateMachine.Aliases[i];
                if (alias != null && AliasMatchesCurrentState(alias)
                    && TryFindTransitionFrom(alias.Id, out selectedTransition, out destination))
                    return true;
            }

            string parentId = currentState.ParentStateMachineId;
            while (!string.IsNullOrEmpty(parentId))
            {
                if (TryFindTransitionFrom(parentId, out selectedTransition, out destination))
                    return true;
                parentId = stateMachine.FindStateMachine(parentId)?.ParentStateMachineId;
            }

            selectedTransition = null;
            destination = null;
            return false;
        }

        private bool TryFindTransitionFrom(
            string sourceNodeId,
            out AnimStateTransition selectedTransition,
            out AnimSequenceState destination)
        {
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition == null || transition.FromStateId != sourceNodeId)
                    continue;
                if (!IsTransitionTimingReady(transition, currentState, currentTime))
                    continue;
                if (!ConditionsPass(transition))
                    continue;

                transitionPathCount = 0;
                transitionPath[transitionPathCount++] = transition;
                if (!TryResolveNode(transition.ToStateId, 0, out destination)
                    || destination == currentState || destination?.Sequence?.Clip == null)
                    continue;

                selectedTransition = transitionPath[transitionPathCount - 1];
                return true;
            }

            selectedTransition = null;
            destination = null;
            transitionPathCount = 0;
            return false;
        }

        private bool TryResolveNode(string nodeId, int depth, out AnimSequenceState destination)
        {
            destination = null;
            if (depth >= transitionPath.Length)
                return false;

            AnimStateNode node = stateMachine.FindNode(nodeId);
            switch (node)
            {
                case AnimSequenceState state:
                    destination = state;
                    return true;
                case AnimStateMachineNode nested:
                    return TryResolveNode(nested.DefaultNodeId, depth + 1, out destination);
                case AnimStateConduit conduit:
                    return TryResolveConduit(conduit, depth + 1, out destination);
                default:
                    return false;
            }
        }

        private bool TryResolveConduit(AnimStateConduit conduit, int depth, out AnimSequenceState destination)
        {
            for (int i = 0; i < stateMachine.Transitions.Count; i++)
            {
                AnimStateTransition transition = stateMachine.Transitions[i];
                if (transition == null || transition.FromStateId != conduit.Id || !ConditionsPass(transition))
                    continue;
                if (transitionPathCount >= transitionPath.Length)
                    break;

                int previousCount = transitionPathCount;
                transitionPath[transitionPathCount++] = transition;
                if (TryResolveNode(transition.ToStateId, depth + 1, out destination))
                    return true;
                transitionPathCount = previousCount;
            }

            destination = null;
            return false;
        }

        private bool AliasMatchesCurrentState(AnimStateAlias alias)
        {
            for (int i = 0; i < alias.SourceNodeIds.Count; i++)
            {
                string sourceId = alias.SourceNodeIds[i];
                if (sourceId == currentState.Id
                    || stateMachine.FindStateMachine(sourceId) != null
                    && stateMachine.IsInStateMachine(currentState.Id, sourceId))
                    return true;
            }
            return false;
        }
        private bool ConditionsPass(AnimStateTransition transition)
        {
            string sourceId = transition.RuleResultSourceId;
            if (string.IsNullOrEmpty(sourceId))
                return true;

            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition?.Id == sourceId)
                    return EvaluateCondition(condition);
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode node = transition.RuleNodes[i];
                if (node?.Id == sourceId)
                    return EvaluateRuleNode(transition, node, 0);
            }
            return false;
        }
        private bool EvaluateRuleNode(AnimStateTransition transition, AnimStateRuleNode node, int depth)
        {
            if (node == null || depth > transition.Conditions.Count + transition.RuleNodes.Count)
                return false;

            bool hasInput = false;
            bool value = node.Operation != AnimStateRuleOperator.Or;
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition == null || condition.RuleTargetId != node.Id)
                    continue;
                if (!ApplyRuleInput(node.Operation, EvaluateCondition(condition), ref hasInput, ref value))
                    return value;
            }

            for (int i = 0; i < transition.RuleNodes.Count; i++)
            {
                AnimStateRuleNode input = transition.RuleNodes[i];
                if (input == null || input.TargetId != node.Id)
                    continue;
                if (!ApplyRuleInput(node.Operation,
                        EvaluateRuleNode(transition, input, depth + 1), ref hasInput, ref value))
                    return value;
            }

            return node.Operation == AnimStateRuleOperator.Not
                ? hasInput && !value
                : hasInput && value;
        }

        private static bool ApplyRuleInput(
            AnimStateRuleOperator operation,
            bool input,
            ref bool hasInput,
            ref bool value)
        {
            if (operation == AnimStateRuleOperator.Not)
            {
                if (hasInput)
                {
                    value = false;
                    return false;
                }
                hasInput = true;
                value = input;
                return true;
            }

            hasInput = true;
            if (operation == AnimStateRuleOperator.And)
            {
                value &= input;
                return value;
            }

            value |= input;
            return !value;
        }

        private bool EvaluateCondition(AnimStateCondition condition)
        {
            if (condition.Source == AnimStateConditionSource.OwnerMember)
            {
                return ownerConditions.TryGetValue(condition, out OwnerConditionBinding binding)
                       && binding.TryRead(condition.ValueType, out ParameterValue ownerValue)
                       && ConditionPasses(condition.ValueType, ownerValue, condition, 0);
            }

            return parameterByName.TryGetValue(condition.Parameter, out AnimStateParameter parameter)
                   && ConditionPasses(parameter.Type, GetValue(parameter.Name), condition, parameter.Hash);
        }

        private bool ConditionPasses(
            AnimStateParameterType type,
            ParameterValue value,
            AnimStateCondition condition,
            int parameterHash)
        {
            return type switch
            {
                AnimStateParameterType.Bool => condition.Mode == AnimStateConditionMode.If ? value.Bool : !value.Bool,
                AnimStateParameterType.Trigger => condition.Mode == AnimStateConditionMode.If
                    ? triggers.Contains(parameterHash)
                    : !triggers.Contains(parameterHash),
                AnimStateParameterType.Int => Compare(value.Int, Mathf.RoundToInt(condition.Threshold), condition.Mode),
                _ => Compare(value.Float, condition.Threshold, condition.Mode)
            };
        }

        private static bool Compare(float value, float threshold, AnimStateConditionMode mode) => mode switch
        {
            AnimStateConditionMode.Greater => value > threshold,
            AnimStateConditionMode.GreaterOrEqual => value >= threshold,
            AnimStateConditionMode.Less => value < threshold,
            AnimStateConditionMode.LessOrEqual => value <= threshold,
            AnimStateConditionMode.NotEqual => !Mathf.Approximately(value, threshold),
            _ => Mathf.Approximately(value, threshold)
        };

        private void BeginTransition(AnimStateTransition transition, AnimSequenceState destination)
        {
            if (destination?.Sequence?.Clip == null)
                return;

            for (int i = 0; i < transitionPathCount; i++)
                ConsumeTriggers(transitionPath[i]);
            activeTransition = transition;
            nextState = destination;
            nextTime = 0f;
            transitionTime = 0f;
            nextNotify.Reset();
            nextPlayable = CreatePlayable(destination);
            graph.Connect(nextPlayable, 0, mixer, 1);
            mixer.SetInputWeight(1, transition.Duration <= 0f ? 1f : 0f);
            nextNotify.Dispatch(destination.Sequence, 0f, 0f, gameObject, animator, this, 0f);
            OnStateEnter?.Invoke(destination);
            if (transition.Duration <= 0f)
                CompleteTransition();
        }

        private void CompleteTransition()
        {
            AnimSequenceState exited = currentState;
            currentNotify.End(gameObject, animator, exited?.Sequence, currentTime);
            mixer.DisconnectInput(0);
            if (currentPlayable.IsValid())
                graph.DestroyPlayable(currentPlayable);
            mixer.DisconnectInput(1);
            graph.Connect(nextPlayable, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);

            currentPlayable = nextPlayable;
            currentState = nextState;
            currentTime = nextTime;
            NotifyCursor reusableCursor = currentNotify;
            currentNotify = nextNotify;
            nextNotify = reusableCursor;
            nextNotify.Reset();
            nextPlayable = default;
            nextState = null;
            activeTransition = null;
            transitionTime = 0f;
            OnStateExit?.Invoke(exited);
        }

        private void DispatchNotifies(
            AnimSequenceState state,
            NotifyCursor cursor,
            float previous,
            float current,
            float weight)
        {
            if (state?.Sequence == null)
                return;
            cursor.DispatchRange(state.Sequence, previous, current, state.Loop, gameObject, animator, this, weight);
        }

        private void ConsumeTriggers(AnimStateTransition transition)
        {
            for (int i = 0; i < transition.Conditions.Count; i++)
            {
                AnimStateCondition condition = transition.Conditions[i];
                if (condition != null
                    && condition.Source == AnimStateConditionSource.Parameter
                    && parameterByName.TryGetValue(condition.Parameter, out AnimStateParameter parameter)
                    && parameter.Type == AnimStateParameterType.Trigger)
                {
                    triggers.Remove(parameter.Hash);
                }
            }
        }

        private static bool IsTransitionTimingReady(
            AnimStateTransition transition,
            AnimSequenceState state,
            float stateTime)
        {
            if (transition == null)
                return false;
            switch (transition.Timing)
            {
                case AnimStateTransitionTiming.Immediate:
                    return true;
                case AnimStateTransitionTiming.ExitTime:
                    return GetNormalizedTime(state, stateTime) >= transition.ExitTime;
                case AnimStateTransitionTiming.AnimationEnd:
                {
                    float length = state?.Sequence?.Length ?? 0f;
                    return state != null && !state.Loop && length > 0f && stateTime >= length;
                }
                default:
                    return false;
            }
        }

        private static float GetNormalizedTime(AnimSequenceState state, float time)
        {
            float length = state?.Sequence?.Length ?? 0f;
            return length > 0f ? time / length : 0f;
        }

        private void SetValue(string parameter, float floatValue, int intValue, bool boolValue)
        {
            if (!parameterByName.TryGetValue(parameter, out AnimStateParameter definition))
                return;
            values[definition.Hash] = new ParameterValue { Float = floatValue, Int = intValue, Bool = boolValue };
        }

        private ParameterValue GetValue(string parameter) =>
            parameterByName.TryGetValue(parameter, out AnimStateParameter definition)
            && values.TryGetValue(definition.Hash, out ParameterValue value)
                ? value
                : default;

        private bool TryGetParameter(string name, AnimStateParameterType type, out AnimStateParameter parameter)
        {
            if (parameterByName.TryGetValue(name, out parameter) && parameter.Type == type)
                return true;
            parameter = null;
            return false;
        }

        private void StopCurrentState()
        {
            if (currentState != null)
            {
                currentNotify.End(gameObject, animator, currentState.Sequence, currentTime);
                OnStateExit?.Invoke(currentState);
            }
            if (graph.IsValid())
            {
                mixer.DisconnectInput(0);
                mixer.DisconnectInput(1);
                if (currentPlayable.IsValid())
                    graph.DestroyPlayable(currentPlayable);
                if (nextPlayable.IsValid())
                    graph.DestroyPlayable(nextPlayable);
            }
            currentPlayable = default;
            nextPlayable = default;
            currentState = null;
            nextState = null;
            activeTransition = null;
        }

        private void DisposeGraph()
        {
            StopCurrentState();
            if (graph.IsValid())
                graph.Destroy();
            if (animator != null && animator.runtimeAnimatorController == null && previousController != null)
                animator.runtimeAnimatorController = previousController;
            previousController = null;
        }

        private void BindNotifyCursor(NotifyCursor cursor)
        {
            cursor.Notify -= ForwardNotify;
            cursor.Notify += ForwardNotify;
        }
        private void ForwardNotify(AnimNotify notify, AnimNotifyContext context) => OnNotify?.Invoke(notify, context);

        private sealed class OwnerConditionBinding
        {
            private const BindingFlags MemberFlags = BindingFlags.Instance
                                                     | BindingFlags.Public
                                                     | BindingFlags.NonPublic
                                                     | BindingFlags.DeclaredOnly;

            private readonly Component owner;
            private readonly FieldInfo field;
            private readonly PropertyInfo property;
            private readonly MethodInfo method;

            private OwnerConditionBinding(Component owner, MemberInfo member)
            {
                this.owner = owner;
                field = member as FieldInfo;
                property = member as PropertyInfo;
                method = member as MethodInfo;
            }

            public static OwnerConditionBinding Create(GameObject ownerObject, AnimStateCondition condition)
            {
                if (string.IsNullOrEmpty(condition.OwnerType))
                    return null;
                Type ownerType = Type.GetType(condition.OwnerType, false);
                if (ownerType == null || !typeof(Component).IsAssignableFrom(ownerType))
                    return null;

                Component owner = ownerObject.GetComponent(ownerType) ?? ownerObject.GetComponentInParent(ownerType);
                if (owner == null || !TryFindMember(ownerType, condition.OwnerMember, out MemberInfo member))
                    return null;
                return new OwnerConditionBinding(owner, member);
            }

            public bool TryRead(AnimStateParameterType valueType, out ParameterValue value)
            {
                value = default;
                try
                {
                    object raw = field != null ? field.GetValue(owner)
                        : property != null ? property.GetValue(owner)
                        : method?.Invoke(owner, null);
                    if (raw == null)
                        return false;

                    switch (valueType)
                    {
                        case AnimStateParameterType.Bool when raw is bool boolValue:
                            value.Bool = boolValue;
                            return true;
                        case AnimStateParameterType.Int:
                            value.Int = Convert.ToInt32(raw);
                            return true;
                        case AnimStateParameterType.Float:
                            value.Float = Convert.ToSingle(raw);
                            return true;
                        default:
                            return false;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private static bool TryFindMember(Type ownerType, string memberKey, out MemberInfo member)
            {
                member = null;
                if (string.IsNullOrEmpty(memberKey) || memberKey.Length < 3 || memberKey[1] != ':')
                    return false;

                char kind = memberKey[0];
                string memberName = memberKey.Substring(2);
                for (Type type = ownerType; type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                {
                    if (kind == 'F')
                        member = type.GetField(memberName, MemberFlags);
                    else if (kind == 'P')
                        member = type.GetProperty(memberName, MemberFlags);
                    else if (kind == 'M')
                    {
                        MethodInfo[] methods = type.GetMethods(MemberFlags);
                        for (int i = 0; i < methods.Length; i++)
                        {
                            if (methods[i].Name == memberName && methods[i].GetParameters().Length == 0)
                            {
                                member = methods[i];
                                break;
                            }
                        }
                    }
                    if (member != null)
                        return true;
                }
                return false;
            }
        }
        private struct ParameterValue
        {
            public float Float;
            public int Int;
            public bool Bool;
        }

        private sealed class NotifyCursor
        {
            private readonly MontageNotifyDispatcher dispatcher = new();

            public NotifyCursor() => dispatcher.OnNotify += Forward;
            public event Action<AnimNotify, AnimNotifyContext> Notify;

            public void Reset() => dispatcher.Reset();

            public void Dispatch(
                AnimSequenceSO sequence,
                float previous,
                float current,
                GameObject owner,
                Animator targetAnimator,
                IAnimNotifyHandler handler,
                float weight) =>
                dispatcher.Dispatch(sequence, previous, current, owner, targetAnimator, handler, weight);

            public void DispatchRange(
                AnimSequenceSO sequence,
                float previous,
                float current,
                bool loop,
                GameObject owner,
                Animator targetAnimator,
                IAnimNotifyHandler handler,
                float weight)
            {
                float length = sequence.Length;
                if (!loop || length <= 0f)
                {
                    Dispatch(sequence, Mathf.Min(previous, length), Mathf.Min(current, length), owner, targetAnimator, handler, weight);
                    return;
                }

                int previousLoop = Mathf.FloorToInt(previous / length);
                int currentLoop = Mathf.FloorToInt(current / length);
                float previousLocal = Mathf.Repeat(previous, length);
                float currentLocal = Mathf.Repeat(current, length);
                if (previousLoop == currentLoop)
                {
                    Dispatch(sequence, previousLocal, currentLocal, owner, targetAnimator, handler, weight);
                    return;
                }

                Dispatch(sequence, previousLocal, length, owner, targetAnimator, handler, weight);
                for (int cycle = previousLoop + 1; cycle <= currentLoop; cycle++)
                {
                    dispatcher.EndActiveStates(owner, targetAnimator, sequence, length);
                    dispatcher.Reset();
                    float end = cycle == currentLoop ? currentLocal : length;
                    Dispatch(sequence, 0f, end, owner, targetAnimator, handler, weight);
                }
            }

            public void End(GameObject owner, Animator targetAnimator, AnimSequenceSO sequence, float time)
            {
                if (sequence != null)
                    dispatcher.EndActiveStates(owner, targetAnimator, sequence, time);
                dispatcher.Reset();
            }

            private void Forward(AnimNotify notify, AnimNotifyContext context) => Notify?.Invoke(notify, context);
        }
    }
}
