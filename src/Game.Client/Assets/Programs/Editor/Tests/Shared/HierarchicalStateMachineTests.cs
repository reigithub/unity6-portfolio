using System;
using System.Collections.Generic;
using Game.Library.Shared;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    /// <summary>
    /// <see cref="HierarchicalStateMachine{TContext,TEvent}"/> の単体テスト。
    /// フラット <see cref="StateMachine{TContext,TEvent}"/> とのパリティ、任意ステート遷移、
    /// 階層（初期子降下 / LCA 遷移 / 自己遷移 / IsInState）を検証する。
    /// </summary>
    [TestFixture]
    public class HierarchicalStateMachineTests
    {
        #region Test Context and States

        private class TestContext
        {
            public List<string> CallLog { get; } = new();
            public int Value { get; set; }
        }

        private enum TestEvent
        {
            ToA,
            ToB,
            ToC,
            ToChase,
            ToAttack,
            Stagger,
            Recover,
            Die,
            Precedence
        }

        /// <summary>ライフサイクルを型名付きで CallLog に記録する共通基底（テスト用）。</summary>
        private abstract class LoggingState : HierarchicalState<TestContext, TestEvent>
        {
            public override void Enter() => Context.CallLog.Add($"{GetType().Name}.Enter");
            public override void Update() => Context.CallLog.Add($"{GetType().Name}.Update");
            public override void FixedUpdate() => Context.CallLog.Add($"{GetType().Name}.FixedUpdate");
            public override void LateUpdate() => Context.CallLog.Add($"{GetType().Name}.LateUpdate");
            public override void Exit() => Context.CallLog.Add($"{GetType().Name}.Exit");
        }

        // フラット（親を持たない）ステート
        private class StateA : LoggingState { }
        private class StateB : LoggingState { }
        private class StateC : LoggingState { }

        // 階層ステート:
        //   Alive（複合）
        //     ├ Patrol（Alive の初期子）
        //     ├ Combat（複合）
        //     │   ├ Chase（Combat の初期子）
        //     │   └ Attack
        //     └ Stagger
        //   Dead（ルート。Alive の兄弟）
        private class Alive : LoggingState { }
        private class Patrol : LoggingState { }
        private class Combat : LoggingState { }
        private class Chase : LoggingState { }
        private class Attack : LoggingState { }
        private class Stagger : LoggingState { }
        private class Dead : LoggingState { }

        /// <summary>Enter 内で遷移をリクエストするステート（Enter 内 Transition の検証用）。</summary>
        private class EnterTransitionState : LoggingState
        {
            public override void Enter()
            {
                base.Enter();
                StateMachine.Transition(TestEvent.ToB);
            }
        }

        // int イベント版の検証用
        private class IntStateA : HierarchicalState<TestContext, int> { }
        private class IntStateB : HierarchicalState<TestContext, int> { }

        /// <summary>
        /// 標準的な階層ツリーと遷移を組んだステートマシンを構築して返す。
        /// </summary>
        private static HierarchicalStateMachine<TestContext, TestEvent> BuildHierarchy(TestContext context)
        {
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddSubState<Alive, Patrol>(isInitial: true);
            sm.AddSubState<Alive, Combat>();
            sm.AddSubState<Alive, Stagger>();
            sm.AddSubState<Combat, Chase>(isInitial: true);
            sm.AddSubState<Combat, Attack>();

            sm.AddTransition<Patrol, Combat>(TestEvent.ToChase);   // 複合ターゲット（初期子 Chase へ降下）
            sm.AddTransition<Chase, Attack>(TestEvent.ToAttack);   // Combat 内の兄弟間
            sm.AddTransition<Attack, Chase>(TestEvent.ToChase);    // Combat 内の兄弟間
            sm.AddTransition<Alive, Stagger>(TestEvent.Stagger);   // 親レベル（全 Alive 子孫から発火）
            sm.AddTransition<Stagger, Patrol>(TestEvent.Recover);
            sm.AddTransition<Dead>(TestEvent.Die);                 // 任意ステート → Dead

            sm.SetInitState<Alive>();
            return sm;
        }

        #endregion

        #region Flat Parity

        [Test]
        public void Constructor_WithContext_SetsContext()
        {
            var context = new TestContext { Value = 42 };
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            Assert.That(sm.Context, Is.EqualTo(context));
            Assert.That(sm.Context.Value, Is.EqualTo(42));
        }

        [Test]
        public void SetInitState_ThenUpdate_EntersInitialState()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();
            sm.Update();

            Assert.That(sm.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void FromToTransition_MovesToTargetState()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();
            sm.Update();

            Assert.That(sm.Transition(TestEvent.ToB), Is.EqualTo(StateEventResult.Succeeded));
            sm.Update();

            Assert.That(sm.IsCurrentState<StateB>(), Is.True);
        }

        [Test]
        public void Transition_ExecutesExitThenEnter()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();
            sm.Update();
            context.CallLog.Clear();

            sm.Transition(TestEvent.ToB);
            sm.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "StateA.Exit", "StateB.Enter" }));
        }

        [Test]
        public void Update_InvokesCurrentStateUpdate()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.SetInitState<StateA>();
            sm.Update();
            context.CallLog.Clear();

            sm.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "StateA.Update" }));
        }

        [Test]
        public void FixedUpdate_And_LateUpdate_InvokeLeafOnly()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();                       // Alive -> Patrol
            sm.Transition(TestEvent.ToChase);
            sm.Update();                       // -> Combat/Chase
            context.CallLog.Clear();

            sm.FixedUpdate();
            sm.LateUpdate();

            // 親（Alive/Combat）ではなく葉（Chase）のみ駆動される
            Assert.That(context.CallLog, Is.EqualTo(new[] { "Chase.FixedUpdate", "Chase.LateUpdate" }));
        }

        [Test]
        public void Transition_InvalidEvent_ReturnsFailed()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();
            sm.Update();

            Assert.That(sm.Transition(TestEvent.ToC), Is.EqualTo(StateEventResult.Failed));
        }

        [Test]
        public void Transition_WhenPendingAlreadySet_ReturnsWaiting()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.AddTransition<StateA, StateC>(TestEvent.ToC);
            sm.SetInitState<StateA>();
            sm.Update();

            sm.Transition(TestEvent.ToB);
            Assert.That(sm.Transition(TestEvent.ToC), Is.EqualTo(StateEventResult.Waiting));
        }

        [Test]
        public void Transition_BeforeProcessing_Throws()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();

            Assert.Throws<InvalidOperationException>(() => sm.Transition(TestEvent.ToB));
        }

        [Test]
        public void IsProcessing_ReflectsLifecycle()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.SetInitState<StateA>();
            Assert.That(sm.IsProcessing(), Is.False);

            sm.Update();
            Assert.That(sm.IsProcessing(), Is.True);
        }

        [Test]
        public void AddTransition_DuringProcessing_Throws()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);
            sm.SetInitState<StateA>();
            sm.Update();

            Assert.Throws<InvalidOperationException>(() => sm.AddTransition<StateA, StateB>(TestEvent.ToB));
        }

        [Test]
        public void AddTransition_DuplicateFromTo_Throws()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);

            Assert.Throws<InvalidOperationException>(() => sm.AddTransition<StateA, StateC>(TestEvent.ToB));
        }

        [Test]
        public void Reset_ClearsProcessingAndAllowsRestart()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.SetInitState<StateA>();
            sm.Update();
            sm.Transition(TestEvent.ToB);
            sm.Update();

            sm.Reset();
            Assert.That(sm.IsProcessing(), Is.False);

            sm.SetInitState<StateA>();
            sm.Update();
            Assert.That(sm.IsCurrentState<StateA>(), Is.True);
        }

        [Test]
        public void Transition_RequestedInEnter_IsProcessedSameUpdate()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<EnterTransitionState, StateB>(TestEvent.ToB);
            sm.SetInitState<EnterTransitionState>();
            sm.Update();

            Assert.That(sm.IsCurrentState<StateB>(), Is.True);
            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "EnterTransitionState.Enter",
                "EnterTransitionState.Exit",
                "StateB.Enter"
            }));
        }

        [Test]
        public void IntEventStateMachine_Transitions()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext>(context);

            sm.AddTransition<IntStateA, IntStateB>(1);
            sm.SetInitState<IntStateA>();
            sm.Update();

            Assert.That(sm.Transition(1), Is.EqualTo(StateEventResult.Succeeded));
            sm.Update();

            Assert.That(sm.IsCurrentState<IntStateB>(), Is.True);
        }

        #endregion

        #region Any-State Transition

        [Test]
        public void AnyTransition_FiresFromUnrelatedState()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA, StateB>(TestEvent.ToB);
            sm.AddTransition<StateC>(TestEvent.Die); // 任意ステート → StateC
            sm.SetInitState<StateA>();
            sm.Update();

            sm.Transition(TestEvent.ToB);
            sm.Update(); // StateB へ

            Assert.That(sm.Transition(TestEvent.Die), Is.EqualTo(StateEventResult.Succeeded));
            sm.Update();

            Assert.That(sm.IsCurrentState<StateC>(), Is.True);
        }

        [Test]
        public void AnyTransition_TakesPrecedenceOverFromTo()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            // 同一イベントに from-to（StateA->StateB）と 任意（->StateC）を登録
            sm.AddTransition<StateA, StateB>(TestEvent.Precedence);
            sm.AddTransition<StateC>(TestEvent.Precedence);
            sm.SetInitState<StateA>();
            sm.Update();

            sm.Transition(TestEvent.Precedence);
            sm.Update();

            // 任意ステート遷移が優先されて StateC になる
            Assert.That(sm.IsCurrentState<StateC>(), Is.True);
        }

        [Test]
        public void AddTransition_DuplicateAny_Throws()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddTransition<StateA>(TestEvent.Die);

            Assert.Throws<InvalidOperationException>(() => sm.AddTransition<StateB>(TestEvent.Die));
        }

        #endregion

        #region Hierarchy

        [Test]
        public void InitialState_DescendsIntoInitialSubState()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);

            sm.Update();

            // Alive（初期子 Patrol）へ入ると親 → 子の順に Enter される
            Assert.That(context.CallLog, Is.EqualTo(new[] { "Alive.Enter", "Patrol.Enter" }));
            Assert.That(sm.IsCurrentState<Patrol>(), Is.True);
            Assert.That(sm.IsInState<Alive>(), Is.True);
        }

        [Test]
        public void CompositeTarget_ResolvesToInitialSubState_AndKeepsSharedAncestor()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();               // -> Patrol
            context.CallLog.Clear();

            sm.Transition(TestEvent.ToChase); // Patrol -> Combat（初期子 Chase へ降下）
            sm.Update();

            // 共通祖先 Alive は再入されない。Patrol.Exit → Combat.Enter → Chase.Enter
            Assert.That(context.CallLog, Is.EqualTo(new[] { "Patrol.Exit", "Combat.Enter", "Chase.Enter" }));
            Assert.That(sm.IsCurrentState<Chase>(), Is.True);
        }

        [Test]
        public void SiblingTransition_DoesNotReenterSharedParent()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.ToChase);
            sm.Update();               // -> Combat/Chase
            context.CallLog.Clear();

            sm.Transition(TestEvent.ToAttack); // Chase -> Attack（Combat 内の兄弟）
            sm.Update();

            // Combat / Alive は保持（Chase.Exit → Attack.Enter のみ）
            Assert.That(context.CallLog, Is.EqualTo(new[] { "Chase.Exit", "Attack.Enter" }));
            Assert.That(sm.IsInState<Combat>(), Is.True);
        }

        [Test]
        public void ParentLevelTransition_FiresFromDeepDescendant()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.ToChase);
            sm.Update();               // -> Combat/Chase（Alive の孫）
            context.CallLog.Clear();

            // Alive レベルに登録した Stagger 遷移が、孫 Chase から発火する
            Assert.That(sm.Transition(TestEvent.Stagger), Is.EqualTo(StateEventResult.Succeeded));
            sm.Update();

            // Chase・Combat を抜けて（Alive は保持）Stagger へ
            Assert.That(context.CallLog, Is.EqualTo(new[] { "Chase.Exit", "Combat.Exit", "Stagger.Enter" }));
            Assert.That(sm.IsCurrentState<Stagger>(), Is.True);
            Assert.That(sm.IsInState<Alive>(), Is.True);
        }

        [Test]
        public void AnyTransition_FromDeepState_ExitsAllAncestors()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.ToChase);
            sm.Update();               // -> Combat/Chase
            context.CallLog.Clear();

            sm.Transition(TestEvent.Die); // 任意 → Dead（ルート）
            sm.Update();

            // 葉から根まで全て Exit してから Dead を Enter
            Assert.That(context.CallLog, Is.EqualTo(new[]
            {
                "Chase.Exit",
                "Combat.Exit",
                "Alive.Exit",
                "Dead.Enter"
            }));
            Assert.That(sm.IsCurrentState<Dead>(), Is.True);
            Assert.That(sm.IsInState<Alive>(), Is.False);
        }

        [Test]
        public void SelfTransition_ReentersState()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.Stagger);
            sm.Update();               // Patrol -> Stagger
            context.CallLog.Clear();

            // Stagger 中に再度 Stagger（Alive レベル遷移が Stagger 自身へ）→ 自己遷移で Exit→Enter
            sm.Transition(TestEvent.Stagger);
            sm.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "Stagger.Exit", "Stagger.Enter" }));
            Assert.That(sm.IsCurrentState<Stagger>(), Is.True);
        }

        [Test]
        public void StaggerRecover_ReturnsToSibling()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.Stagger);
            sm.Update();               // -> Stagger
            context.CallLog.Clear();

            sm.Transition(TestEvent.Recover); // Stagger -> Patrol（Alive 内の兄弟）
            sm.Update();

            Assert.That(context.CallLog, Is.EqualTo(new[] { "Stagger.Exit", "Patrol.Enter" }));
            Assert.That(sm.IsCurrentState<Patrol>(), Is.True);
        }

        [Test]
        public void IsInState_ChecksAncestors()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();
            sm.Transition(TestEvent.ToChase);
            sm.Update();               // -> Combat/Chase

            Assert.That(sm.IsCurrentState<Chase>(), Is.True);
            Assert.That(sm.IsCurrentState<Alive>(), Is.False); // 葉ではない
            Assert.That(sm.IsInState<Chase>(), Is.True);
            Assert.That(sm.IsInState<Combat>(), Is.True);
            Assert.That(sm.IsInState<Alive>(), Is.True);
            Assert.That(sm.IsInState<Dead>(), Is.False);
        }

        [Test]
        public void AddSubState_DuplicateInitial_Throws()
        {
            var context = new TestContext();
            var sm = new HierarchicalStateMachine<TestContext, TestEvent>(context);

            sm.AddSubState<Alive, Patrol>(isInitial: true);

            Assert.Throws<InvalidOperationException>(() => sm.AddSubState<Alive, Combat>(isInitial: true));
        }

        [Test]
        public void AddSubState_DuringProcessing_Throws()
        {
            var context = new TestContext();
            var sm = BuildHierarchy(context);
            sm.Update();

            Assert.Throws<InvalidOperationException>(() => sm.AddSubState<Alive, StateA>());
        }

        #endregion
    }
}
