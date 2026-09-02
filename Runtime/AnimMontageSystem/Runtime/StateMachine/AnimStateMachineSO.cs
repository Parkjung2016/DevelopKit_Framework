using System;
using System.Collections.Generic;
using UnityEngine;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime
{
    public enum AnimStateParameterType
    {
        Float,
        Int,
        Bool,
        Trigger
    }

    public enum AnimStateConditionSource
    {
        Parameter,
        OwnerMember
    }
    public enum AnimStateConditionMode
    {
        If = 0,
        IfNot = 1,
        Greater = 2,
        Less = 3,
        Equals = 4,
        NotEqual = 5,
        GreaterOrEqual = 6,
        LessOrEqual = 7
    }

    public enum AnimStateRuleOperator
    {
        And,
        Or,
        Not
    }

    public enum AnimStateTransitionTiming
    {
        Immediate,
        ExitTime,
        AnimationEnd
    }

    [Serializable]
    public sealed class AnimStateParameter
    {
        [SerializeField] private string name = "Parameter";
        [SerializeField] private AnimStateParameterType type;
        [SerializeField] private float defaultFloat;
        [SerializeField] private int defaultInt;
        [SerializeField] private bool defaultBool;

        public string Name { get => name; set => name = CleanName(value, "Parameter"); }
        public int Hash => Animator.StringToHash(name);
        public AnimStateParameterType Type { get => type; set => type = value; }
        public float DefaultFloat { get => defaultFloat; set => defaultFloat = value; }
        public int DefaultInt { get => defaultInt; set => defaultInt = value; }
        public bool DefaultBool { get => defaultBool; set => defaultBool = value; }

        private static string CleanName(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    [Serializable]
    public sealed class AnimStateCondition
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private AnimStateConditionSource source;
        [SerializeField] private string parameter;
        [SerializeField] private string ownerType;
        [SerializeField] private string ownerMember;
        [SerializeField] private AnimStateParameterType valueType;
        [SerializeField] private AnimStateConditionMode mode;
        [SerializeField] private float threshold;
        [SerializeField, HideInInspector] private Vector2 rulePosition;
        [SerializeField, HideInInspector] private string ruleTargetId;

        public string Id => id;
        public AnimStateConditionSource Source { get => source; set => source = value; }
        public string Parameter { get => parameter; set => parameter = value ?? string.Empty; }
        public string OwnerType { get => ownerType; set => ownerType = value ?? string.Empty; }
        public string OwnerMember { get => ownerMember; set => ownerMember = value ?? string.Empty; }
        public AnimStateParameterType ValueType { get => valueType; set => valueType = value; }
        public AnimStateConditionMode Mode { get => mode; set => mode = value; }
        public float Threshold { get => threshold; set => threshold = value; }
        public Vector2 RulePosition { get => rulePosition; set => rulePosition = value; }
        public string RuleTargetId { get => ruleTargetId; set => ruleTargetId = value ?? string.Empty; }

        internal void EnsureData()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
            ruleTargetId ??= string.Empty;
        }
    }

    [Serializable]
    public sealed class AnimStateRuleNode
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private AnimStateRuleOperator operation;
        [SerializeField, HideInInspector] private Vector2 position;
        [SerializeField, HideInInspector] private string targetId;

        public string Id => id;
        public AnimStateRuleOperator Operation { get => operation; set => operation = value; }
        public Vector2 Position { get => position; set => position = value; }
        public string TargetId { get => targetId; set => targetId = value ?? string.Empty; }

        internal AnimStateRuleNode(AnimStateRuleOperator value, Vector2 graphPosition)
        {
            id = Guid.NewGuid().ToString("N");
            operation = value;
            position = graphPosition;
        }

        internal void EnsureData()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
            targetId ??= string.Empty;
        }
    }

    [Serializable]
    public sealed class AnimStateTransition
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField, HideInInspector] private string fromStateId;
        [SerializeField, HideInInspector] private string toStateId;
        [SerializeField] private bool hasExitTime = true;
        [SerializeField] private bool waitForAnimationEnd;
        [SerializeField, Range(0f, 1f)] private float exitTime = 0.9f;
        [SerializeField, Min(0f)] private float duration = 0.15f;
        [SerializeField] private List<AnimStateCondition> conditions = new();
        [SerializeField, HideInInspector] private List<AnimStateRuleNode> ruleNodes = new();
        [SerializeField, HideInInspector] private string ruleResultSourceId;
        [SerializeField, HideInInspector] private int ruleVersion;
        [SerializeField, HideInInspector] private Vector2 ruleResultPosition = new(520f, 180f);

        public string Id => id;
        public string FromStateId => fromStateId;
        public string ToStateId => toStateId;
        public bool HasExitTime
        {
            get => hasExitTime;
            set
            {
                hasExitTime = value;
                if (value)
                    waitForAnimationEnd = false;
            }
        }
        public bool WaitForAnimationEnd
        {
            get => waitForAnimationEnd;
            set
            {
                waitForAnimationEnd = value;
                if (value)
                    hasExitTime = false;
            }
        }
        public AnimStateTransitionTiming Timing
        {
            get => waitForAnimationEnd
                ? AnimStateTransitionTiming.AnimationEnd
                : hasExitTime
                    ? AnimStateTransitionTiming.ExitTime
                    : AnimStateTransitionTiming.Immediate;
            set
            {
                hasExitTime = value == AnimStateTransitionTiming.ExitTime;
                waitForAnimationEnd = value == AnimStateTransitionTiming.AnimationEnd;
            }
        }
        public float ExitTime { get => exitTime; set => exitTime = Mathf.Clamp01(value); }
        public float Duration { get => duration; set => duration = Mathf.Max(0f, value); }
        public IReadOnlyList<AnimStateCondition> Conditions => conditions;
        public IReadOnlyList<AnimStateRuleNode> RuleNodes => ruleNodes;
        public string RuleResultSourceId { get => ruleResultSourceId; set => ruleResultSourceId = value ?? string.Empty; }
        public Vector2 RuleResultPosition { get => ruleResultPosition; set => ruleResultPosition = value; }

        internal AnimStateTransition(string from, string to)
        {
            id = Guid.NewGuid().ToString("N");
            fromStateId = from;
            toStateId = to;
            ruleVersion = 1;
        }

        public void AddCondition(AnimStateCondition condition)
        {
            condition ??= new AnimStateCondition();
            condition.EnsureData();
            conditions.Add(condition);
            if (string.IsNullOrEmpty(ruleResultSourceId))
                ruleResultSourceId = condition.Id;
        }

        public void RemoveConditionAt(int index)
        {
            if (index < 0 || index >= conditions.Count)
                return;
            AnimStateCondition removed = conditions[index];
            conditions.RemoveAt(index);
            if (removed != null && ruleResultSourceId == removed.Id)
                ruleResultSourceId = string.Empty;
        }

        public AnimStateRuleNode AddRuleNode(AnimStateRuleOperator operation, Vector2 position)
        {
            AnimStateRuleNode node = new(operation, position);
            ruleNodes.Add(node);
            if (string.IsNullOrEmpty(ruleResultSourceId))
                ruleResultSourceId = node.Id;
            return node;
        }

        public void RemoveRuleNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;
            AnimStateRuleNode removed = null;
            for (int i = 0; i < ruleNodes.Count; i++)
            {
                if (ruleNodes[i]?.Id != nodeId)
                    continue;
                removed = ruleNodes[i];
                ruleNodes.RemoveAt(i);
                break;
            }
            if (removed == null)
                return;

            bool wasResultSource = ruleResultSourceId == nodeId;
            string nextTarget = wasResultSource ? string.Empty : removed.TargetId;
            string replacementResult = string.Empty;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i]?.RuleTargetId != nodeId)
                    continue;
                conditions[i].RuleTargetId = nextTarget;
                replacementResult = string.IsNullOrEmpty(replacementResult)
                    ? conditions[i].Id
                    : replacementResult;
            }
            for (int i = 0; i < ruleNodes.Count; i++)
            {
                if (ruleNodes[i]?.TargetId != nodeId)
                    continue;
                ruleNodes[i].TargetId = nextTarget;
                replacementResult = string.IsNullOrEmpty(replacementResult)
                    ? ruleNodes[i].Id
                    : replacementResult;
            }
            if (wasResultSource)
                ruleResultSourceId = replacementResult;
        }

        internal void EnsureData()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
            conditions ??= new List<AnimStateCondition>();
            ruleNodes ??= new List<AnimStateRuleNode>();
            for (int i = 0; i < conditions.Count; i++)
                conditions[i]?.EnsureData();
            for (int i = 0; i < ruleNodes.Count; i++)
                ruleNodes[i]?.EnsureData();
            if (ruleVersion == 0)
                UpgradeRuleResult();
            ruleResultSourceId ??= string.Empty;
        }

        private void UpgradeRuleResult()
        {
            ruleVersion = 1;
            var roots = new List<string>();
            for (int i = 0; i < conditions.Count; i++)
            {
                AnimStateCondition condition = conditions[i];
                if (condition != null && string.IsNullOrEmpty(condition.RuleTargetId))
                    roots.Add(condition.Id);
            }
            for (int i = 0; i < ruleNodes.Count; i++)
            {
                AnimStateRuleNode node = ruleNodes[i];
                if (node != null && string.IsNullOrEmpty(node.TargetId))
                    roots.Add(node.Id);
            }
            if (roots.Count == 0)
                return;
            if (roots.Count == 1)
            {
                ruleResultSourceId = roots[0];
                return;
            }

            AnimStateRuleNode andNode = new(AnimStateRuleOperator.And,
                ruleResultPosition - new Vector2(190f, 0f));
            ruleNodes.Add(andNode);
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && roots.Contains(conditions[i].Id))
                    conditions[i].RuleTargetId = andNode.Id;
            for (int i = 0; i < ruleNodes.Count - 1; i++)
                if (ruleNodes[i] != null && roots.Contains(ruleNodes[i].Id))
                    ruleNodes[i].TargetId = andNode.Id;
            ruleResultSourceId = andNode.Id;
        }
    }

    /// <summary>State Machine 그래프에 배치되는 모든 노드가 공유하는 데이터입니다.</summary>
    [Serializable]
    public abstract class AnimStateNode
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private string name = "Node";
        [SerializeField, HideInInspector] private string parentStateMachineId;
        [SerializeField, HideInInspector] private Vector2 position;

        public string Id => id;
        public string Name { get => name; set => name = CleanName(value, "Node"); }
        public string ParentStateMachineId { get => parentStateMachineId; internal set => parentStateMachineId = value ?? string.Empty; }
        public Vector2 Position { get => position; set => position = value; }

        protected AnimStateNode(string nodeName, Vector2 graphPosition, string parentId)
        {
            id = Guid.NewGuid().ToString("N");
            name = CleanName(nodeName, "Node");
            position = graphPosition;
            parentStateMachineId = parentId ?? string.Empty;
        }

        internal void EnsureData()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
            name = CleanName(name, "Node");
            parentStateMachineId ??= string.Empty;
            OnEnsureData();
        }

        protected virtual void OnEnsureData()
        {
        }

        protected static string CleanName(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    [Serializable]
    public sealed class AnimSequenceState : AnimStateNode
    {
        [SerializeField] private AnimSequenceSO sequence;
        [SerializeField, Min(0.01f)] private float speed = 1f;
        [SerializeField] private bool loop = true;

        public AnimSequenceSO Sequence { get => sequence; set => sequence = value; }
        public float Speed { get => Mathf.Max(0.01f, speed); set => speed = Mathf.Max(0.01f, value); }
        public bool Loop { get => loop; set => loop = value; }

        internal AnimSequenceState(AnimSequenceSO value, Vector2 graphPosition, string parentId)
            : base(value != null ? value.name : "State", graphPosition, parentId)
        {
            sequence = value;
            loop = value?.Clip != null && value.Clip.isLooping;
        }
    }

    /// <summary>흐름이 들어오면 조건을 만족한 첫 번째 Transition으로 즉시 이동하는 결정 노드입니다.</summary>
    [Serializable]
    public sealed class AnimStateConduit : AnimStateNode
    {
        internal AnimStateConduit(Vector2 graphPosition, string parentId)
            : base("Conduit", graphPosition, parentId)
        {
        }
    }

    /// <summary>여러 State가 같은 외부 Transition을 공유하도록 묶는 비실행 규칙 노드입니다.</summary>
    [Serializable]
    public sealed class AnimStateAlias : AnimStateNode
    {
        [SerializeField] private List<string> sourceNodeIds = new();

        public IReadOnlyList<string> SourceNodeIds => sourceNodeIds;

        internal AnimStateAlias(Vector2 graphPosition, string parentId)
            : base("Alias", graphPosition, parentId)
        {
        }

        public bool Contains(string nodeId) => sourceNodeIds.Contains(nodeId);

        public void AddSource(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && !sourceNodeIds.Contains(nodeId))
                sourceNodeIds.Add(nodeId);
        }

        public void RemoveSource(string nodeId) => sourceNodeIds.Remove(nodeId);

        protected override void OnEnsureData() => sourceNodeIds ??= new List<string>();
    }

    /// <summary>자체 Entry와 기본 노드를 갖는 내부 State Machine입니다.</summary>
    [Serializable]
    public sealed class AnimStateMachineNode : AnimStateNode
    {
        [SerializeField, HideInInspector] private string defaultNodeId;
        [SerializeField, HideInInspector] private Vector2 entryPosition = new(24f, 120f);

        public string DefaultNodeId => defaultNodeId;
        public Vector2 EntryPosition { get => entryPosition; set => entryPosition = value; }

        internal AnimStateMachineNode(Vector2 graphPosition, string parentId)
            : base("State Machine", graphPosition, parentId)
        {
        }

        internal void SetDefaultNode(string nodeId) => defaultNodeId = nodeId ?? string.Empty;
    }

    /// <summary>Sequence State, 조건 분기, 공유 State 그룹과 내부 State Machine을 저장하는 계층형 그래프입니다.</summary>
    [CreateAssetMenu(fileName = "StateMachine_", menuName = "PJDev/Animation/Animation State Machine")]
    public sealed class AnimStateMachineSO : ScriptableObject
    {
        [SerializeField] private List<AnimStateParameter> parameters = new();
        [SerializeField] private List<AnimSequenceState> states = new();
        [SerializeField] private List<AnimStateConduit> conduits = new();
        [SerializeField] private List<AnimStateAlias> aliases = new();
        [SerializeField] private List<AnimStateMachineNode> stateMachines = new();
        [SerializeField] private List<AnimStateTransition> transitions = new();
        [SerializeField, HideInInspector] private string defaultNodeId;
        [SerializeField, HideInInspector] private Vector2 entryPosition = new(24f, 120f);

        public IReadOnlyList<AnimStateParameter> Parameters => parameters;
        public IReadOnlyList<AnimSequenceState> States => states;
        public IReadOnlyList<AnimStateConduit> Conduits => conduits;
        public IReadOnlyList<AnimStateAlias> Aliases => aliases;
        public IReadOnlyList<AnimStateMachineNode> StateMachines => stateMachines;
        public IReadOnlyList<AnimStateTransition> Transitions => transitions;
        public string DefaultNodeId => defaultNodeId;
        public Vector2 EntryPosition { get => entryPosition; set => entryPosition = value; }

        public AnimSequenceState AddState(AnimSequenceSO sequence, Vector2 position, string parentId = "")
        {
            var state = new AnimSequenceState(sequence, position, NormalizeParent(parentId));
            states.Add(state);
            EnsureDefaultNode(state.ParentStateMachineId, state.Id);
            return state;
        }

        public AnimStateConduit AddConduit(Vector2 position, string parentId = "")
        {
            var conduit = new AnimStateConduit(position, NormalizeParent(parentId));
            conduits.Add(conduit);
            EnsureDefaultNode(conduit.ParentStateMachineId, conduit.Id);
            return conduit;
        }

        public AnimStateAlias AddAlias(Vector2 position, string parentId = "")
        {
            var alias = new AnimStateAlias(position, NormalizeParent(parentId));
            aliases.Add(alias);
            return alias;
        }

        public AnimStateMachineNode AddStateMachine(Vector2 position, string parentId = "")
        {
            var node = new AnimStateMachineNode(position, NormalizeParent(parentId));
            stateMachines.Add(node);
            EnsureDefaultNode(node.ParentStateMachineId, node.Id);
            return node;
        }

        public bool RemoveNode(string nodeId)
        {
            AnimStateNode node = FindNode(nodeId);
            if (node == null)
                return false;

            var removedIds = new HashSet<string> { nodeId };
            if (node is AnimStateMachineNode)
                CollectDescendants(nodeId, removedIds);

            states.RemoveAll(item => removedIds.Contains(item.Id));
            conduits.RemoveAll(item => removedIds.Contains(item.Id));
            aliases.RemoveAll(item => removedIds.Contains(item.Id));
            stateMachines.RemoveAll(item => removedIds.Contains(item.Id));
            transitions.RemoveAll(item => removedIds.Contains(item.FromStateId) || removedIds.Contains(item.ToStateId));
            for (int i = 0; i < aliases.Count; i++)
            {
                foreach (string removedId in removedIds)
                    aliases[i].RemoveSource(removedId);
            }

            RepairDefaultNodes();
            return true;
        }

        public AnimStateTransition AddTransition(string fromNodeId, string toNodeId)
        {
            if (string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId)
                || fromNodeId == toNodeId)
                return null;

            AnimStateNode fromNode = FindNode(fromNodeId);
            AnimStateNode toNode = FindNode(toNodeId);
            if (fromNode == null || toNode is null or AnimStateAlias
                || fromNode.ParentStateMachineId != toNode.ParentStateMachineId)
                return null;

            AnimStateTransition existing = FindTransition(fromNodeId, toNodeId);
            if (existing != null)
                return existing;

            var transition = new AnimStateTransition(fromNodeId, toNodeId);
            transitions.Add(transition);
            return transition;
        }

        public AnimStateTransition FindTransition(string fromNodeId, string toNodeId)
        {
            for (int i = 0; i < transitions.Count; i++)
            {
                AnimStateTransition transition = transitions[i];
                if (transition != null
                    && transition.FromStateId == fromNodeId
                    && transition.ToStateId == toNodeId)
                    return transition;
            }

            return null;
        }

        public void RemoveTransition(string transitionId) =>
            transitions.RemoveAll(transition => transition.Id == transitionId);

        public bool SetDefaultNode(string parentStateMachineId, string nodeId)
        {
            AnimStateNode node = FindNode(nodeId);
            string parentId = NormalizeParent(parentStateMachineId);
            if (node == null || node is AnimStateAlias || node.ParentStateMachineId != parentId)
                return false;

            if (string.IsNullOrEmpty(parentId))
                defaultNodeId = nodeId;
            else
                FindStateMachine(parentId)?.SetDefaultNode(nodeId);
            return true;
        }

        public string GetDefaultNodeId(string parentStateMachineId)
        {
            string parentId = NormalizeParent(parentStateMachineId);
            return string.IsNullOrEmpty(parentId)
                ? defaultNodeId
                : FindStateMachine(parentId)?.DefaultNodeId ?? string.Empty;
        }

        public Vector2 GetEntryPosition(string parentStateMachineId)
        {
            string parentId = NormalizeParent(parentStateMachineId);
            return string.IsNullOrEmpty(parentId)
                ? entryPosition
                : FindStateMachine(parentId)?.EntryPosition ?? Vector2.zero;
        }

        public void SetEntryPosition(string parentStateMachineId, Vector2 value)
        {
            string parentId = NormalizeParent(parentStateMachineId);
            if (string.IsNullOrEmpty(parentId))
                entryPosition = value;
            else if (FindStateMachine(parentId) is { } machine)
                machine.EntryPosition = value;
        }

        public AnimStateNode FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return null;
            AnimStateNode node = FindById(states, nodeId);
            node ??= FindById(conduits, nodeId);
            node ??= FindById(aliases, nodeId);
            node ??= FindById(stateMachines, nodeId);
            return node;
        }

        public AnimSequenceState FindState(string stateId) => FindById(states, stateId);
        public AnimStateConduit FindConduit(string conduitId) => FindById(conduits, conduitId);
        public AnimStateAlias FindAlias(string aliasId) => FindById(aliases, aliasId);
        public AnimStateMachineNode FindStateMachine(string stateMachineId) => FindById(stateMachines, stateMachineId);

        public AnimSequenceState FindState(AnimSequenceSO sequence)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].Sequence == sequence)
                    return states[i];
            }
            return null;
        }

        public bool IsInStateMachine(string nodeId, string stateMachineId)
        {
            AnimStateNode node = FindNode(nodeId);
            while (node != null && !string.IsNullOrEmpty(node.ParentStateMachineId))
            {
                if (node.ParentStateMachineId == stateMachineId)
                    return true;
                node = FindStateMachine(node.ParentStateMachineId);
            }
            return false;
        }

        public AnimStateParameter AddParameter(string parameterName, AnimStateParameterType type)
        {
            string uniqueName = GetUniqueParameterName(parameterName);
            var parameter = new AnimStateParameter { Name = uniqueName, Type = type };
            parameters.Add(parameter);
            return parameter;
        }

        public void RemoveParameterAt(int index)
        {
            if (index < 0 || index >= parameters.Count)
                return;

            string removedName = parameters[index].Name;
            parameters.RemoveAt(index);
            for (int i = 0; i < transitions.Count; i++)
            {
                AnimStateTransition transition = transitions[i];
                for (int j = transition.Conditions.Count - 1; j >= 0; j--)
                {
                    if (transition.Conditions[j].Source == AnimStateConditionSource.Parameter
                        && transition.Conditions[j].Parameter == removedName)
                        transition.RemoveConditionAt(j);
                }
            }
        }

        public void RenameParameterAt(int index, string requestedName)
        {
            if (index < 0 || index >= parameters.Count)
                return;

            string previousName = parameters[index].Name;
            string baseName = string.IsNullOrWhiteSpace(requestedName) ? "Parameter" : requestedName.Trim();
            string candidate = baseName;
            int suffix = 1;
            while (HasParameter(candidate, index))
                candidate = $"{baseName} {suffix++}";
            if (previousName == candidate)
                return;

            parameters[index].Name = candidate;
            for (int i = 0; i < transitions.Count; i++)
            {
                IReadOnlyList<AnimStateCondition> conditions = transitions[i].Conditions;
                for (int j = 0; j < conditions.Count; j++)
                {
                    if (conditions[j].Source == AnimStateConditionSource.Parameter
                        && conditions[j].Parameter == previousName)
                        conditions[j].Parameter = candidate;
                }
            }
        }

        public string GetUniqueParameterName(string requestedName)
        {
            string baseName = string.IsNullOrWhiteSpace(requestedName) ? "Parameter" : requestedName.Trim();
            string candidate = baseName;
            int suffix = 1;
            while (HasParameter(candidate))
                candidate = $"{baseName} {suffix++}";
            return candidate;
        }

        public bool HasParameter(string parameterName, int exceptIndex = -1)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i != exceptIndex && parameters[i].Name == parameterName)
                    return true;
            }
            return false;
        }

        private void OnEnable() => EnsureData();

#if UNITY_EDITOR
        private void OnValidate() => EnsureData();
#endif

        private void EnsureData()
        {
            parameters ??= new List<AnimStateParameter>();
            states ??= new List<AnimSequenceState>();
            conduits ??= new List<AnimStateConduit>();
            aliases ??= new List<AnimStateAlias>();
            stateMachines ??= new List<AnimStateMachineNode>();
            transitions ??= new List<AnimStateTransition>();
            EnsureNodes(states);
            EnsureNodes(conduits);
            EnsureNodes(aliases);
            EnsureNodes(stateMachines);
            for (int i = 0; i < transitions.Count; i++)
                transitions[i]?.EnsureData();

            transitions.RemoveAll(transition => transition == null
                || FindNode(transition.FromStateId) == null
                || FindNode(transition.ToStateId) is null or AnimStateAlias);
            RemoveDuplicateTransitions();
            for (int i = 0; i < aliases.Count; i++)
            {
                for (int j = aliases[i].SourceNodeIds.Count - 1; j >= 0; j--)
                {
                    string sourceId = aliases[i].SourceNodeIds[j];
                    AnimStateNode source = FindNode(sourceId);
                    if (source is not (AnimSequenceState or AnimStateMachineNode)
                        || source.ParentStateMachineId != aliases[i].ParentStateMachineId)
                        aliases[i].RemoveSource(sourceId);
                }
            }
            RepairDefaultNodes();
        }

        private void RemoveDuplicateTransitions()
        {
            for (int i = transitions.Count - 1; i > 0; i--)
            {
                AnimStateTransition candidate = transitions[i];
                for (int j = 0; j < i; j++)
                {
                    AnimStateTransition existing = transitions[j];
                    if (existing.FromStateId != candidate.FromStateId
                        || existing.ToStateId != candidate.ToStateId)
                        continue;

                    transitions.RemoveAt(i);
                    break;
                }
            }
        }

        private void RepairDefaultNodes()
        {
            if (!IsValidDefault(defaultNodeId, string.Empty))
                defaultNodeId = FindFirstEntryNode(string.Empty)?.Id ?? string.Empty;
            for (int i = 0; i < stateMachines.Count; i++)
            {
                AnimStateMachineNode machine = stateMachines[i];
                if (!IsValidDefault(machine.DefaultNodeId, machine.Id))
                    machine.SetDefaultNode(FindFirstEntryNode(machine.Id)?.Id);
            }
        }

        private bool IsValidDefault(string nodeId, string parentId)
        {
            AnimStateNode node = FindNode(nodeId);
            return node != null && node is not AnimStateAlias && node.ParentStateMachineId == parentId;
        }

        private AnimStateNode FindFirstEntryNode(string parentId)
        {
            for (int i = 0; i < states.Count; i++)
                if (states[i].ParentStateMachineId == parentId)
                    return states[i];
            for (int i = 0; i < conduits.Count; i++)
                if (conduits[i].ParentStateMachineId == parentId)
                    return conduits[i];
            for (int i = 0; i < stateMachines.Count; i++)
                if (stateMachines[i].ParentStateMachineId == parentId)
                    return stateMachines[i];
            return null;
        }

        private void EnsureDefaultNode(string parentId, string nodeId)
        {
            if (string.IsNullOrEmpty(GetDefaultNodeId(parentId)))
                SetDefaultNode(parentId, nodeId);
        }

        private string NormalizeParent(string parentId) =>
            !string.IsNullOrEmpty(parentId) && FindStateMachine(parentId) != null ? parentId : string.Empty;

        private void CollectDescendants(string stateMachineId, HashSet<string> result)
        {
            for (int i = 0; i < stateMachines.Count; i++)
            {
                AnimStateMachineNode child = stateMachines[i];
                if (child.ParentStateMachineId != stateMachineId || !result.Add(child.Id))
                    continue;
                CollectDescendants(child.Id, result);
            }
            AddChildren(states, stateMachineId, result);
            AddChildren(conduits, stateMachineId, result);
            AddChildren(aliases, stateMachineId, result);
        }

        private static void AddChildren<T>(IReadOnlyList<T> nodes, string parentId, HashSet<string> result)
            where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].ParentStateMachineId == parentId)
                    result.Add(nodes[i].Id);
        }

        private static T FindById<T>(IReadOnlyList<T> nodes, string id) where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Id == id)
                    return nodes[i];
            return null;
        }

        private static void EnsureNodes<T>(IReadOnlyList<T> nodes) where T : AnimStateNode
        {
            for (int i = 0; i < nodes.Count; i++)
                nodes[i]?.EnsureData();
        }
    }
}
