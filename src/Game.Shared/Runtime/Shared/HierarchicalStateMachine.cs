using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Library.Shared
{
    /// <summary>
    /// 階層ステートマシン（HFSM）の各状態を表す抽象基底クラス。
    /// <see cref="State{TContext,TEvent}"/> に <see cref="Parent"/> を加え、
    /// superstate / substate の入れ子（木構造）を構成できる点が異なる。
    /// </summary>
    /// <typeparam name="TContext">コンテキスト型（ステート間で共有するデータ）</typeparam>
    /// <typeparam name="TEvent">遷移イベントの型（通常はenum）</typeparam>
    public abstract class HierarchicalState<TContext, TEvent> : IState, IStateMachineContext<TContext>
    {
        /// <summary>所属するステートマシンへの参照</summary>
        protected internal HierarchicalStateMachine<TContext, TEvent> StateMachine { get; init; }

        /// <summary>親 superstate（ルート直下の状態は null）</summary>
        protected internal HierarchicalState<TContext, TEvent> Parent { get; internal set; }

        /// <summary>共有コンテキストへのアクセサ</summary>
        public TContext Context => StateMachine.Context;

        /// <summary>ステート開始時に呼び出される</summary>
        public virtual void Enter()
        {
        }

        /// <summary>毎フレーム呼び出される</summary>
        public virtual void Update()
        {
        }

        /// <summary>物理演算タイミングで呼び出される（MonoBehaviour.FixedUpdate相当）</summary>
        public virtual void FixedUpdate()
        {
        }

        /// <summary>フレーム終了時に呼び出される（MonoBehaviour.LateUpdate相当）</summary>
        public virtual void LateUpdate()
        {
        }

        /// <summary>ステート終了時に呼び出される</summary>
        public virtual void Exit()
        {
        }
    }

    /// <summary>
    /// 階層ステートマシン（HFSM）。
    /// <para>
    /// フラットな <see cref="StateMachine{TContext,TEvent}"/> と非継承の別クラスで、次の3種の遷移を扱う:
    /// </para>
    /// <list type="bullet">
    /// <item>from-to 遷移（<see cref="AddTransition{TFromState,TToState}"/>）。遷移元が複合ステートなら全子孫から発火する。</item>
    /// <item>任意ステート遷移（<see cref="AddTransition{TAnyState}"/>）。現在ステートに依らず発火し、最優先で採用される。</item>
    /// <item>階層（<see cref="AddSubState{TParent,TChild}"/>）。遷移は LCA（最小共通祖先）を境に Exit / Enter する。</item>
    /// </list>
    /// </summary>
    /// <typeparam name="TContext">コンテキスト型</typeparam>
    /// <typeparam name="TEvent">遷移ルール毎のイベントKeyの型</typeparam>
    public class HierarchicalStateMachine<TContext, TEvent> : IStateMachineContext<TContext>
    {
        private enum StatePhase
        {
            Idle,
            Entering,
            Updating,
            Exiting
        }

        private readonly Dictionary<Type, HierarchicalState<TContext, TEvent>> _states = new();
        private readonly Dictionary<TEvent, Dictionary<HierarchicalState<TContext, TEvent>, HierarchicalState<TContext, TEvent>>> _fromToTransitions = new();
        private readonly Dictionary<TEvent, HierarchicalState<TContext, TEvent>> _anyTransitions = new();

        // 複合ステートの初期子ステート（superstate へ入った時に降下する先）
        private readonly Dictionary<HierarchicalState<TContext, TEvent>, HierarchicalState<TContext, TEvent>> _initialSubStates = new();

        private StatePhase _currentPhase = StatePhase.Idle;
        private HierarchicalState<TContext, TEvent> _currentLeaf;   // 現在アクティブな葉ステート
        private HierarchicalState<TContext, TEvent> _pendingTarget; // 遷移予約（葉/複合いずれも可）

        public TContext Context { get; }

        public HierarchicalStateMachine(TContext context)
        {
            Context = context;
        }

        #region Build

        /// <summary>
        /// 子ステートを親（複合ステート）に紐付ける。
        /// </summary>
        /// <param name="isInitial">true の場合、親へ入った時に降下する初期子ステートに指定する（親ごとに1つ）</param>
        /// <typeparam name="TParent">親 superstate</typeparam>
        /// <typeparam name="TChild">子 substate</typeparam>
        public void AddSubState<TParent, TChild>(bool isInitial = false)
            where TParent : HierarchicalState<TContext, TEvent>, new()
            where TChild : HierarchicalState<TContext, TEvent>, new()
        {
            if (_currentLeaf != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            var parent = GetOrAddState<TParent>();
            var child = GetOrAddState<TChild>();
            child.Parent = parent;

            if (isInitial)
            {
                if (_initialSubStates.ContainsKey(parent))
                    throw new InvalidOperationException($"Initial sub-state already set: {typeof(TParent).Name}");

                _initialSubStates[parent] = child;
            }
        }

        /// <summary>
        /// from-to 遷移ルールを登録します。
        /// </summary>
        /// <param name="eventKey">遷移ルールを識別するイベントKey値</param>
        /// <typeparam name="TFromState">遷移元ステート（葉でも複合でも可。複合に登録すると全子孫から発火する）</typeparam>
        /// <typeparam name="TToState">遷移先ステート</typeparam>
        public void AddTransition<TFromState, TToState>(TEvent eventKey)
            where TFromState : HierarchicalState<TContext, TEvent>, new()
            where TToState : HierarchicalState<TContext, TEvent>, new()
        {
            if (_currentLeaf != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (!_fromToTransitions.TryGetValue(eventKey, out var transitionDict))
            {
                _fromToTransitions[eventKey] = transitionDict = new Dictionary<HierarchicalState<TContext, TEvent>, HierarchicalState<TContext, TEvent>>();
            }

            if (!transitionDict.TryAdd(from, to))
            {
                throw new InvalidOperationException($"Transition already exists: {typeof(TFromState).Name} -> {typeof(TToState).Name}, EventId: {eventKey}");
            }
        }

        /// <summary>
        /// 任意ステートから遷移先に指定できるステートを設定します。
        /// 現在ステートに依らず発火し、<see cref="Transition"/> では from-to より優先されます。
        /// </summary>
        /// <remarks>フラットな <see cref="StateMachine{TContext,TEvent}"/> の同名オーバーロードと同一セマンティクス（イベントごとに単一ターゲット）。</remarks>
        public void AddTransition<TAnyState>(TEvent eventKey) where TAnyState : HierarchicalState<TContext, TEvent>, new()
        {
            if (_currentLeaf != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            var any = GetOrAddState<TAnyState>();

            if (!_anyTransitions.TryAdd(eventKey, any))
            {
                throw new InvalidOperationException($"Transition already exists: {typeof(TAnyState).Name}, EventId: {eventKey}");
            }
        }

        /// <summary>
        /// ステートマシーン処理開始時に初期状態となるステートを設定します（複合なら初期子へ降下）。
        /// </summary>
        public void SetInitState<TInitState>() where TInitState : HierarchicalState<TContext, TEvent>, new()
        {
            if (_currentLeaf != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            _pendingTarget = GetOrAddState<TInitState>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TState GetOrAddState<TState>() where TState : HierarchicalState<TContext, TEvent>, new()
        {
            var stateType = typeof(TState);

            if (_states.TryGetValue(stateType, out var existingState))
            {
                return (TState)existingState;
            }

            var newState = new TState { StateMachine = this };
            _states[stateType] = newState;
            return newState;
        }

        /// <summary>
        /// ステートマシンの実行状態をリセット（遷移テーブル・階層は保持）。
        /// プールから再利用する際に <see cref="SetInitState"/> で初期ステートを再設定可能にする。
        /// </summary>
        public void Reset()
        {
            _currentLeaf = null;
            _pendingTarget = null;
            _currentPhase = StatePhase.Idle;
        }

        #endregion

        #region Transition

        /// <summary>
        /// 遷移テーブルに基づいた遷移を実行します。
        /// 解決順は「任意ステート遷移（最優先）→ 現在の葉から根へ遡った from-to（葉優先）」。
        /// </summary>
        /// <returns>StateEventResult: 遷移リクエストに対する応答</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateEventResult Transition(TEvent eventKey)
        {
            if (_currentLeaf == null)
                throw new InvalidOperationException("State Machine is not Processing!!");

            if (_currentPhase == StatePhase.Exiting)
                throw new InvalidOperationException("Exit Processing");

            // 前回の遷移を開始する前なので、まだ遷移できない
            if (_pendingTarget != null)
                return StateEventResult.Waiting;

            // 任意ステート遷移が最優先（現在ステートに依らず発火）
            if (_anyTransitions.TryGetValue(eventKey, out var anyState))
            {
                _pendingTarget = anyState;
                return StateEventResult.Succeeded;
            }

            // 葉 → 親 → … → ルート の順に探索し、最初に一致した遷移を採用（innermost-first）
            if (_fromToTransitions.TryGetValue(eventKey, out var rules))
            {
                for (var s = _currentLeaf; s != null; s = s.Parent)
                {
                    if (rules.TryGetValue(s, out var toState))
                    {
                        _pendingTarget = toState;
                        return StateEventResult.Succeeded;
                    }
                }
            }

            // 遷移情報が登録されていない
            return StateEventResult.Failed;
        }

        #endregion

        #region Process

        /// <summary>
        /// 現在の葉ステートが指定した型かどうかを判定します。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrentState<TState>() where TState : HierarchicalState<TContext, TEvent>
        {
            if (_currentLeaf == null) throw new InvalidOperationException("State Machine is not Processing!!");

            return _currentLeaf.GetType() == typeof(TState);
        }

        /// <summary>
        /// 祖先を含め、指定した型のステート系列内に居るかどうかを判定します（複合ステート判定に有用）。
        /// </summary>
        public bool IsInState<TState>() where TState : HierarchicalState<TContext, TEvent>
        {
            if (_currentLeaf == null) throw new InvalidOperationException("State Machine is not Processing!!");

            for (var s = _currentLeaf; s != null; s = s.Parent)
            {
                if (s.GetType() == typeof(TState)) return true;
            }

            return false;
        }

        /// <summary>
        /// ステートマシンが動作中かどうかを判定します。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProcessing()
        {
            return _currentLeaf != null;
        }

        /// <summary>
        /// ステートマシンを更新（毎フレーム呼び出し）。
        /// 初回呼び出し時に初期ステートの Enter を実行し、以降は現在の葉ステートの Update を実行する。
        /// 遷移リクエストがある場合は、LCA を境に Exit → Enter の順で遷移処理を行う。
        /// </summary>
        public virtual void Update()
        {
            // プロセスが開始されていなければ、初期Stateをセットしてステートマシーンを起動する
            if (_currentLeaf == null)
            {
                if (_pendingTarget == null)
                    throw new InvalidOperationException("Next State is Nothing!!");

                var target = _pendingTarget;
                _pendingTarget = null;

                try
                {
                    _currentPhase = StatePhase.Entering;
                    EnterChain(null, ResolveEntryLeaf(target));
                }
                catch (Exception e)
                {
                    var failedName = _currentLeaf?.GetType().Name;
                    _pendingTarget = target;
                    _currentLeaf = null;
                    _currentPhase = StatePhase.Idle;
                    throw new InvalidOperationException($"State.Enter() failed in {failedName}: {e.Message}\\n{e.StackTrace}");
                }

                if (_pendingTarget == null)
                {
                    _currentPhase = StatePhase.Idle;
                    return;
                }
            }

            // ステートマシーン更新処理
            try
            {
                if (_pendingTarget == null)
                {
                    _currentPhase = StatePhase.Updating;
                    _currentLeaf.Update();
                }

                while (_pendingTarget != null)
                {
                    var target = _pendingTarget;
                    _pendingTarget = null;
                    PerformTransition(target);
                }

                _currentPhase = StatePhase.Idle;
            }
            catch (Exception e)
            {
                _currentPhase = StatePhase.Idle;
                throw new InvalidOperationException($"StateMachine.Update() failed in {_currentLeaf?.GetType().Name}: {e.Message}\\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 物理演算タイミングで現在の葉ステートの FixedUpdate を呼び出します。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void FixedUpdate()
        {
            _currentLeaf?.FixedUpdate();
        }

        /// <summary>
        /// フレーム終了時に現在の葉ステートの LateUpdate を呼び出します。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void LateUpdate()
        {
            _currentLeaf?.LateUpdate();
        }

        #endregion

        #region Hierarchy

        /// <summary>
        /// target が複合ステートなら初期子へ再帰降下し、実行葉を解決します。
        /// </summary>
        private HierarchicalState<TContext, TEvent> ResolveEntryLeaf(HierarchicalState<TContext, TEvent> target)
        {
            var s = target;
            while (_initialSubStates.TryGetValue(s, out var child))
            {
                s = child;
            }

            return s;
        }

        /// <summary>
        /// 遷移を実行します。fromLeaf から LCA の手前まで Exit し、LCA 配下から toLeaf まで Enter します。
        /// 兄弟間遷移では共通の親を再入しません。fromLeaf == toLeaf の自己遷移は Exit → Enter で再入します。
        /// </summary>
        private void PerformTransition(HierarchicalState<TContext, TEvent> target)
        {
            var fromLeaf = _currentLeaf;
            var toLeaf = ResolveEntryLeaf(target);

            // 自己遷移（外部遷移として Exit → Enter で再入する）
            if (fromLeaf == toLeaf)
            {
                _currentPhase = StatePhase.Exiting;
                fromLeaf.Exit();

                _currentPhase = StatePhase.Entering;
                _currentLeaf = toLeaf;
                toLeaf.Enter();
                return;
            }

            var lca = FindLowestCommonAncestor(fromLeaf, toLeaf);

            // Exit: fromLeaf から LCA の手前まで（LCA は抜けない。LCA が null なら根まで全部抜ける）
            _currentPhase = StatePhase.Exiting;
            for (var s = fromLeaf; s != lca; s = s.Parent)
            {
                s.Exit();
            }

            // Enter: LCA 直下から toLeaf まで（上 → 下）
            _currentPhase = StatePhase.Entering;
            EnterChain(lca, toLeaf);

            // toLeaf が LCA 自身（=祖先への遷移で Enter 対象が空）だった場合に備えて確定させる
            _currentLeaf = toLeaf;
        }

        /// <summary>
        /// ancestorExclusive（含まない。null なら根から）から leaf まで、上 → 下の順に Enter します。
        /// Enter 内での <see cref="Transition"/> を許可するため、Enter するごとに現在の葉を進めます。
        /// </summary>
        private void EnterChain(HierarchicalState<TContext, TEvent> ancestorExclusive, HierarchicalState<TContext, TEvent> leaf)
        {
            // leaf → ancestor の順に積み、反転して上から Enter する
            var path = new List<HierarchicalState<TContext, TEvent>>();
            for (var s = leaf; s != ancestorExclusive; s = s.Parent)
            {
                if (s == null)
                    throw new InvalidOperationException("Target leaf is not a descendant of the given ancestor");

                path.Add(s);
            }

            for (int i = path.Count - 1; i >= 0; i--)
            {
                _currentLeaf = path[i];
                path[i].Enter();
            }
        }

        /// <summary>
        /// 2 葉の最小共通祖先（LCA）を求めます。共通祖先が無ければ null（別ツリー = 根同士の遷移）。
        /// </summary>
        private HierarchicalState<TContext, TEvent> FindLowestCommonAncestor(HierarchicalState<TContext, TEvent> a, HierarchicalState<TContext, TEvent> b)
        {
            var ancestors = new HashSet<HierarchicalState<TContext, TEvent>>();
            for (var s = a; s != null; s = s.Parent)
            {
                ancestors.Add(s);
            }

            for (var s = b; s != null; s = s.Parent)
            {
                if (ancestors.Contains(s)) return s;
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// EventKeyがint型の階層ステートマシーン。
    /// </summary>
    public class HierarchicalStateMachine<TContext> : HierarchicalStateMachine<TContext, int>
    {
        public HierarchicalStateMachine(TContext context) : base(context)
        {
        }
    }
}
