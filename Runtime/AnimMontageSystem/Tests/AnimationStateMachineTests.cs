using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PJDev.DevelopKit.Framework.AnimMontageSystem.Runtime.Tests
{
    public sealed class AnimationStateMachineTests
    {
        private AnimStateMachineSO stateMachine;
        private AnimSequenceSO idle;
        private AnimSequenceSO run;

        [SetUp]
        public void SetUp()
        {
            stateMachine = ScriptableObject.CreateInstance<AnimStateMachineSO>();
            idle = ScriptableObject.CreateInstance<AnimSequenceSO>();
            run = ScriptableObject.CreateInstance<AnimSequenceSO>();
            idle.name = "Idle";
            run.name = "Run";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(run);
            Object.DestroyImmediate(idle);
            Object.DestroyImmediate(stateMachine);
        }

        [Test]
        public void RenameParameter_UpdatesTransitionConditions()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);
            stateMachine.AddParameter("Speed", AnimStateParameterType.Float);
            AnimStateTransition transition = stateMachine.AddTransition(idleState.Id, runState.Id);
            transition.AddCondition(new AnimStateCondition
            {
                Parameter = "Speed",
                Mode = AnimStateConditionMode.Greater,
                Threshold = 0.1f
            });

            stateMachine.RenameParameterAt(0, "Move Speed");

            Assert.That(stateMachine.Parameters[0].Name, Is.EqualTo("Move Speed"));
            Assert.That(transition.Conditions[0].Parameter, Is.EqualTo("Move Speed"));
        }

        [Test]
        public void AddTransition_SameDirection_ReusesExistingTransition()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);

            AnimStateTransition first = stateMachine.AddTransition(idleState.Id, runState.Id);
            AnimStateTransition duplicate = stateMachine.AddTransition(idleState.Id, runState.Id);

            Assert.That(duplicate, Is.SameAs(first));
            Assert.That(stateMachine.Transitions.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddTransition_ReverseDirection_CreatesSeparateTransition()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);

            AnimStateTransition forward = stateMachine.AddTransition(idleState.Id, runState.Id);
            AnimStateTransition reverse = stateMachine.AddTransition(runState.Id, idleState.Id);

            Assert.That(reverse, Is.Not.SameAs(forward));
            Assert.That(stateMachine.Transitions.Count, Is.EqualTo(2));
        }

        [Test]
        public void RemoveNode_RemovesTransitionsAndAliasSource()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);
            AnimStateAlias alias = stateMachine.AddAlias(Vector2.up);
            alias.AddSource(runState.Id);
            stateMachine.AddTransition(idleState.Id, runState.Id);
            stateMachine.AddTransition(alias.Id, runState.Id);

            stateMachine.RemoveNode(runState.Id);

            Assert.That(stateMachine.States.Count, Is.EqualTo(1));
            Assert.That(stateMachine.Transitions, Is.Empty);
            Assert.That(alias.SourceNodeIds, Is.Empty);
            Assert.That(stateMachine.DefaultNodeId, Is.EqualTo(idleState.Id));
        }

        [Test]
        public void NestedStateMachine_HasIndependentEntryAndDefaultNode()
        {
            AnimStateMachineNode machine = stateMachine.AddStateMachine(Vector2.zero);
            AnimSequenceState nested = stateMachine.AddState(idle, Vector2.right, machine.Id);
            Vector2 entryPosition = new(42f, 86f);

            stateMachine.SetEntryPosition(machine.Id, entryPosition);

            Assert.That(nested.ParentStateMachineId, Is.EqualTo(machine.Id));
            Assert.That(stateMachine.GetDefaultNodeId(machine.Id), Is.EqualTo(nested.Id));
            Assert.That(stateMachine.GetEntryPosition(machine.Id), Is.EqualTo(entryPosition));
        }

        [Test]
        public void RemoveStateMachine_RemovesAllDescendants()
        {
            AnimStateMachineNode parent = stateMachine.AddStateMachine(Vector2.zero);
            AnimStateMachineNode child = stateMachine.AddStateMachine(Vector2.right, parent.Id);
            AnimSequenceState nested = stateMachine.AddState(run, Vector2.one, child.Id);

            stateMachine.RemoveNode(parent.Id);

            Assert.That(stateMachine.FindNode(parent.Id), Is.Null);
            Assert.That(stateMachine.FindNode(child.Id), Is.Null);
            Assert.That(stateMachine.FindNode(nested.Id), Is.Null);
        }

        [Test]
        public void BooleanRule_EvaluatesNestedAndOrNot()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);
            stateMachine.AddParameter("A", AnimStateParameterType.Bool);
            stateMachine.AddParameter("B", AnimStateParameterType.Bool);
            stateMachine.AddParameter("C", AnimStateParameterType.Bool);
            AnimStateTransition transition = stateMachine.AddTransition(idleState.Id, runState.Id);

            AnimStateCondition a = new() { Parameter = "A", ValueType = AnimStateParameterType.Bool };
            AnimStateCondition b = new() { Parameter = "B", ValueType = AnimStateParameterType.Bool };
            AnimStateCondition c = new() { Parameter = "C", ValueType = AnimStateParameterType.Bool };
            transition.AddCondition(a);
            transition.AddCondition(b);
            transition.AddCondition(c);
            AnimStateRuleNode or = transition.AddRuleNode(AnimStateRuleOperator.Or, Vector2.zero);
            AnimStateRuleNode not = transition.AddRuleNode(AnimStateRuleOperator.Not, Vector2.zero);
            AnimStateRuleNode and = transition.AddRuleNode(AnimStateRuleOperator.And, Vector2.zero);
            a.RuleTargetId = or.Id;
            b.RuleTargetId = or.Id;
            c.RuleTargetId = not.Id;
            or.TargetId = and.Id;
            not.TargetId = and.Id;
            transition.RuleResultSourceId = and.Id;

            GameObject owner = new("State Machine Rule Test");
            owner.SetActive(false);
            Animator animator = owner.AddComponent<Animator>();
            AnimationStateMachinePlayer player = owner.AddComponent<AnimationStateMachinePlayer>();
            SetPrivateField(player, "animator", animator);
            SetPrivateField(player, "stateMachine", stateMachine);
            owner.SetActive(true);

            try
            {
                player.SetBool("A", false);
                player.SetBool("B", true);
                player.SetBool("C", false);
                Assert.That(EvaluateRule(player, transition), Is.True);

                player.SetBool("C", true);
                Assert.That(EvaluateRule(player, transition), Is.False);

                player.SetBool("B", false);
                player.SetBool("C", false);
                Assert.That(EvaluateRule(player, transition), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void RemoveRuleNode_ReconnectsItsInputs()
        {
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);
            AnimStateTransition transition = stateMachine.AddTransition(idleState.Id, runState.Id);
            AnimStateCondition condition = new();
            transition.AddCondition(condition);
            AnimStateRuleNode first = transition.AddRuleNode(AnimStateRuleOperator.Or, Vector2.zero);
            AnimStateRuleNode next = transition.AddRuleNode(AnimStateRuleOperator.And, Vector2.right);
            condition.RuleTargetId = first.Id;
            first.TargetId = next.Id;

            transition.RemoveRuleNode(first.Id);

            Assert.That(condition.RuleTargetId, Is.EqualTo(next.Id));
            Assert.That(transition.RuleNodes.Count, Is.EqualTo(1));
        }

        private static bool EvaluateRule(
            AnimationStateMachinePlayer player,
            AnimStateTransition transition)
        {
            MethodInfo method = typeof(AnimationStateMachinePlayer).GetMethod(
                "ConditionsPass", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)method.Invoke(player, new object[] { transition });
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        [Test]
        public void AnimationEndTiming_WaitsForNonLoopingSequenceEnd()
        {
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            idle.SetClip(clip);
            AnimSequenceState idleState = stateMachine.AddState(idle, Vector2.zero);
            AnimSequenceState runState = stateMachine.AddState(run, Vector2.right);
            AnimStateTransition transition = stateMachine.AddTransition(idleState.Id, runState.Id);
            transition.Timing = AnimStateTransitionTiming.AnimationEnd;
            idleState.Loop = false;

            MethodInfo method = typeof(AnimationStateMachinePlayer).GetMethod(
                "IsTransitionTimingReady", BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                Assert.That(method, Is.Not.Null);
                Assert.That((bool)method.Invoke(null, new object[] { transition, idleState, 0.99f }), Is.False);
                Assert.That((bool)method.Invoke(null, new object[] { transition, idleState, 1f }), Is.True);
                idleState.Loop = true;
                Assert.That((bool)method.Invoke(null, new object[] { transition, idleState, 2f }), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
        [Test]
        public void StateLoop_OverridesAnimationClipImportLoopSetting()
        {
            var clip = new AnimationClip { wrapMode = WrapMode.ClampForever };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            idle.SetClip(clip);
            AnimSequenceState state = stateMachine.AddState(idle, Vector2.zero);
            PlayableGraph graph = PlayableGraph.Create("State Loop Test");
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            MethodInfo method = typeof(AnimationStateMachinePlayer).GetMethod(
                "SyncPlayableTime", BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                Assert.That(method, Is.Not.Null);
                state.Loop = true;
                playable.SetTime(1d);
                playable.SetDone(true);
                method.Invoke(null, new object[] { playable, state, 0.9f, 1.1f });
                Assert.That(playable.GetTime(), Is.EqualTo(0.1d).Within(0.0001d));
                Assert.That(playable.IsDone(), Is.False);
                Assert.That(playable.GetSpeed(), Is.EqualTo(state.Speed));

                state.Loop = false;
                playable.SetSpeed(state.Speed);
                method.Invoke(null, new object[] { playable, state, 0.9f, 1.1f });
                Assert.That(playable.GetTime(), Is.EqualTo(1d).Within(0.0001d));
                Assert.That(playable.GetSpeed(), Is.Zero);
            }
            finally
            {
                if (graph.IsValid())
                    graph.Destroy();
                Object.DestroyImmediate(clip);
            }
        }
        [Test]
        public void Player_WithoutAnimatorController_CreatesAnimationOutput()
        {
            GameObject owner = new("Controllerless State Machine Player");
            owner.SetActive(false);
            AnimationStateMachinePlayer player = owner.AddComponent<AnimationStateMachinePlayer>();
            SetPrivateField(player, "stateMachine", stateMachine);

            try
            {
                owner.SetActive(true);

                Assert.That(player.Animator, Is.Not.Null);
                Assert.That(player.Animator.runtimeAnimatorController, Is.Null);
                Assert.That(player.IsReady, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
        [Test]
        public void AddParameter_UsesUniqueReadableName()
        {
            stateMachine.AddParameter("Speed", AnimStateParameterType.Float);
            AnimStateParameter duplicate = stateMachine.AddParameter("Speed", AnimStateParameterType.Float);

            Assert.That(duplicate.Name, Is.EqualTo("Speed 1"));
        }
    }
}
