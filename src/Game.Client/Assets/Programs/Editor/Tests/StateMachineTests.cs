using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    [TestFixture]
    public class StateMachineTests
    {
        #region Test Context and States

        private class TestContext
        {
            public List<string> CallLog { get; } = new();
            public int Value { get; set; }
        }

        private enum TestEvent
        {
            ToStateA,
            ToStateB,
            ToStateC,
            ToAny
        }

        private class StateA : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateA.Enter");
            }

            public override void Update()
            {
                Context.CallLog.Add("StateA.Update");
            }

            public override void FixedUpdate()
            {
                Context.CallLog.Add("StateA.FixedUpdate");
            }

            public override void LateUpdate()
            {
                Context.CallLog.Add("StateA.LateUpdate");
            }

            public override void Exit()
            {
                Context.CallLog.Add("StateA.Exit");
            }
        }

        private class StateB : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateB.Enter");
            }

            public override void Update()
            {
                Context.CallLog.Add("StateB.Update");
            }

            public override void FixedUpdate()
            {
                Context.CallLog.Add("StateB.FixedUpdate");
            }

            public override void LateUpdate()
            {
                Context.CallLog.Add("StateB.LateUpdate");
            }

            public override void Exit()
            {
                Context.CallLog.Add("StateB.Exit");
            }
        }

        private class StateC : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateC.Enter");
            }

            public override void Update()
            {
                Context.CallLog.Add("StateC.Update");
            }

            public override void Exit()
            {
                Context.CallLog.Add("StateC.Exit");
            }
        }

        private class StateWithTransitionInEnter : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateWithTransitionInEnter.Enter");
                StateMachine.Transition(TestEvent.ToStateB);
            }

            public override void Exit()
            {
                Context.CallLog.Add("StateWithTransitionInEnter.Exit");
            }
        }

        private class StateWithException : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateWithException.Enter");
                throw new InvalidOperationException("Test exception in Enter");
            }
        }

        private class StateWithExitException : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.CallLog.Add("StateWithExitException.Enter");
            }

            public override void Exit()
            {
                Context.CallLog.Add("StateWithExitException.Exit");
                throw new InvalidOperationException("Test exception in Exit");
            }
        }

        private class ForceTransitionAllowedStateMachine : StateMachine<TestContext, TestEvent>
        {
            protected override bool AllowForceTransition => true;

            public ForceTransitionAllowedStateMachine(TestContext context) : base(context)
            {
            }
        }

        #endregion

        #region Constructor Tests

        [Test]
        public void Constructor_WithContext_SetsContext()
        {
            var context = new TestContext { Value = 42 };
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            Assert.That(stateMachine.Context, Is.EqualTo(context));
            Assert.That(stateMachine.Context.Value, Is.EqualTo(42));
        }

        [Test]
        public void Constructor_WithNullContext_AllowsNull()
        {
            var stateMachine = new StateMachine<TestContext, TestEvent>(null);

            Assert.That(stateMachine.Context, Is.Null);
        }

        #endregion

        #region SetInitState Tests

        [Test]
        public void SetInitState_BeforeProcessing_SetsInitialState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void SetInitState_DuringProcessing_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.Throws<InvalidOperationException>(() => stateMachine.SetInitState<StateB>());
        }

        #endregion

        #region AddTransition Tests

        [Test]
        public void AddTransition_FromTo_AddsTransitionRule()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.Transition(TestEvent.ToStateB), Is.EqualTo(StateEventResult.Succeeded));
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateB>(), Is.True);
        }

        [Test]
        public void AddTransition_DuplicateFromTo_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);

            Assert.Throws<InvalidOperationException>(() =>
                stateMachine.AddTransition<StateA, StateC>(TestEvent.ToStateB));
        }

        [Test]
        public void AddTransition_DuringProcessing_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.Throws<InvalidOperationException>(() =>
                stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB));
        }

        [Test]
        public void AddTransition_Any_AddsAnyTransitionRule()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA>(TestEvent.ToAny);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void AddTransition_DuplicateAny_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA>(TestEvent.ToAny);

            Assert.Throws<InvalidOperationException>(() =>
                stateMachine.AddTransition<StateA>(TestEvent.ToAny));
        }

        #endregion

        #region Transition Tests

        [Test]
        public void Transition_ValidTransition_ReturnsSuccess()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            var result = stateMachine.Transition(TestEvent.ToStateB);

            Assert.That(result, Is.EqualTo(StateEventResult.Succeeded));
        }

        [Test]
        public void Transition_InvalidTransition_ReturnsFailed()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            var result = stateMachine.Transition(TestEvent.ToStateC);

            Assert.That(result, Is.EqualTo(StateEventResult.Failed));
        }

        [Test]
        public void Transition_BeforeProcessing_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateA>();

            // var result = stateMachine.Transition(TestEvent.ToStateB);
            // Assert.That(result, Is.EqualTo(StateEventResult.Failed));

            Assert.Throws<InvalidOperationException>(() => stateMachine.Transition(TestEvent.ToStateB));
        }

        [Test]
        public void Transition_WhenNextStateAlreadySet_ReturnsWaiting()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.AddTransition<StateA, StateC>(TestEvent.ToStateC);
            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            stateMachine.Transition(TestEvent.ToStateB);
            var result = stateMachine.Transition(TestEvent.ToStateC);

            Assert.That(result, Is.EqualTo(StateEventResult.Waiting));
        }

        [Test]
        public void Transition_ExecutesStateLifecycle()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateA>();

            stateMachine.Update();
            context.CallLog.Clear();

            stateMachine.Transition(TestEvent.ToStateB);
            stateMachine.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "StateA.Exit",
                "StateB.Enter"
            }));
        }

        #endregion

        #region ForceTransition Tests

        [Test]
        public void ForceTransition_WhenNotAllowed_DoesNotTransition()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            stateMachine.ForceTransition<StateB>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void ForceTransition_WhenAllowed_TransitionsToState()
        {
            var context = new TestContext();
            var stateMachine = new ForceTransitionAllowedStateMachine(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            stateMachine.ForceTransition<StateB>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateB>(), Is.True);
        }

        [Test]
        public void ForceTransition_WhenAllowed_BypassesTransitionTable()
        {
            var context = new TestContext();
            var stateMachine = new ForceTransitionAllowedStateMachine(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            stateMachine.ForceTransition<StateC>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateC>(), Is.True);
        }

        #endregion

        #region IsCurrentState Tests

        [Test]
        public void IsCurrentState_WhenMatching_ReturnsTrue()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void IsCurrentState_WhenNotMatching_ReturnsFalse()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.IsCurrentState<StateB>(), Is.False);
        }

        [Test]
        public void IsCurrentState_BeforeProcessing_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();

            Assert.Throws<InvalidOperationException>(() => stateMachine.IsCurrentState<StateA>());
        }

        #endregion

        #region IsProcessing Tests

        [Test]
        public void IsProcessing_BeforeUpdate_ReturnsFalse()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();

            Assert.That(stateMachine.IsProcessing(), Is.False);
        }

        [Test]
        public void IsProcessing_AfterUpdate_ReturnsTrue()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(stateMachine.IsProcessing(), Is.True);
        }

        #endregion

        #region Update Tests

        [Test]
        public void Update_WithoutInitState_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            Assert.Throws<InvalidOperationException>(() => stateMachine.Update());
        }

        [Test]
        public void Update_FirstCall_CallsEnterOnInitState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();

            Assert.That(context.CallLog, Contains.Item("StateA.Enter"));
        }

        [Test]
        public void Update_SubsequentCalls_CallsUpdateOnCurrentState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();
            context.CallLog.Clear();

            stateMachine.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "StateA.Update" }));
        }

        [Test]
        public void Update_WithTransitionInEnter_ProcessesMultipleTransitions()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateWithTransitionInEnter, StateB>(TestEvent.ToStateB);
            stateMachine.SetInitState<StateWithTransitionInEnter>();
            stateMachine.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "StateWithTransitionInEnter.Enter",
                "StateWithTransitionInEnter.Exit",
                "StateB.Enter"
            }));
            Assert.That(stateMachine.IsCurrentState<StateB>(), Is.True);
        }

        [Test]
        public void Update_WithExceptionInEnter_ThrowsAndResetsState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateWithException>();

            Assert.Throws<InvalidOperationException>(() => stateMachine.Update());
            Assert.That(stateMachine.IsProcessing(), Is.False);
        }

        [Test]
        public void Update_WithExceptionInExit_ThrowsException()
        {
            var context = new TestContext();
            var stateMachine = new ForceTransitionAllowedStateMachine(context);

            stateMachine.SetInitState<StateWithExitException>();
            stateMachine.Update();

            stateMachine.ForceTransition<StateB>();

            Assert.Throws<InvalidOperationException>(() => stateMachine.Update());
        }

        #endregion

        #region FixedUpdate Tests

        [Test]
        public void FixedUpdate_WhenProcessing_CallsFixedUpdateOnCurrentState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();
            context.CallLog.Clear();

            stateMachine.FixedUpdate();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "StateA.FixedUpdate" }));
        }

        [Test]
        public void FixedUpdate_WhenNotProcessing_DoesNothing()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.FixedUpdate();

            Assert.That(context.CallLog, Is.Empty);
        }

        #endregion

        #region LateUpdate Tests

        [Test]
        public void LateUpdate_WhenProcessing_CallsLateUpdateOnCurrentState()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();
            context.CallLog.Clear();

            stateMachine.LateUpdate();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "StateA.LateUpdate" }));
        }

        [Test]
        public void LateUpdate_WhenNotProcessing_DoesNothing()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.LateUpdate();

            Assert.That(context.CallLog, Is.Empty);
        }

        #endregion

        #region State Context Tests

        [Test]
        public void State_HasAccessToContext()
        {
            var context = new TestContext { Value = 100 };
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateWithContextAccess>();
            stateMachine.Update();

            Assert.That(context.Value, Is.EqualTo(200));
        }

        private class StateWithContextAccess : State<TestContext, TestEvent>
        {
            public override void Enter()
            {
                Context.Value *= 2;
            }
        }

        #endregion

        #region Full Lifecycle Tests

        [Test]
        public void FullLifecycle_MultipleTransitions_ExecutesCorrectly()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.AddTransition<StateA, StateB>(TestEvent.ToStateB);
            stateMachine.AddTransition<StateB, StateC>(TestEvent.ToStateC);
            stateMachine.AddTransition<StateC, StateA>(TestEvent.ToStateA);
            stateMachine.SetInitState<StateA>();

            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);

            Assert.That(stateMachine.Transition(TestEvent.ToStateB), Is.EqualTo(StateEventResult.Succeeded));
            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<StateB>(), Is.True);

            Assert.That(stateMachine.Transition(TestEvent.ToStateC), Is.EqualTo(StateEventResult.Succeeded));
            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<StateC>(), Is.True);

            Assert.That(stateMachine.Transition(TestEvent.ToStateA), Is.EqualTo(StateEventResult.Succeeded));
            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<StateA>(), Is.True);

            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "StateA.Enter",
                "StateA.Exit",
                "StateB.Enter",
                "StateB.Exit",
                "StateC.Enter",
                "StateC.Exit",
                "StateA.Enter"
            }));
        }

        [Test]
        public void FullLifecycle_UpdateFixedUpdateLateUpdate_ExecutesInOrder()
        {
            var context = new TestContext();
            var stateMachine = new StateMachine<TestContext, TestEvent>(context);

            stateMachine.SetInitState<StateA>();
            stateMachine.Update();
            context.CallLog.Clear();

            stateMachine.FixedUpdate();
            stateMachine.Update();
            stateMachine.LateUpdate();

            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "StateA.FixedUpdate",
                "StateA.Update",
                "StateA.LateUpdate"
            }));
        }

        #endregion

        #region StateMachine<TContext> (int EventKey) Tests

        [Test]
        public void StateMachineWithIntEventKey_WorksCorrectly()
        {
            var context = new IntEventContext();
            var stateMachine = new StateMachine<IntEventContext>(context);

            stateMachine.AddTransition<IntStateA, IntStateB>(1);
            stateMachine.SetInitState<IntStateA>();

            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<IntStateA>(), Is.True);

            Assert.That(stateMachine.Transition(1), Is.EqualTo(StateEventResult.Succeeded));
            stateMachine.Update();
            Assert.That(stateMachine.IsCurrentState<IntStateB>(), Is.True);
        }

        private class IntEventContext
        {
        }

        private class IntStateA : State<IntEventContext, int>
        {
        }

        private class IntStateB : State<IntEventContext, int>
        {
        }

        #endregion
    }
}