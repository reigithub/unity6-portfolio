using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Shared
{
    internal interface IState
    {
        public void Enter()
        {
        }

        public void Update()
        {
        }

        // MonoBehavior.FixedUpdate
        public void FixedUpdate()
        {
        }

        // MonoBehavior.LateUpdate
        public void LateUpdate()
        {
        }

        public void Exit()
        {
        }
    }

    internal interface IStateMachineContext<out TContext>
    {
        public TContext Context { get; }
    }

    /// <summary>
    /// ステートマシンの各状態を表す抽象基底クラス
    /// </summary>
    /// <typeparam name="TContext">コンテキスト型（ステート間で共有するデータ）</typeparam>
    /// <typeparam name="TEvent">遷移イベントの型（通常はenum）</typeparam>
    public abstract class State<TContext, TEvent> : IState, IStateMachineContext<TContext>
    {
        /// <summary>所属するステートマシンへの参照</summary>
        protected internal StateMachine<TContext, TEvent> StateMachine { get; init; }

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

    public enum StateEventResult
    {
        Waiting,   // 遷移リクエストしたが順番待ち、次回Updateで再度リクエスト
        Succeeded, // 遷移リクエストが受付られ、次回Updateで処理される
        Failed     // 遷移テーブルにないリクエスト
    }

    /// <summary>
    /// ステートマシーン
    /// </summary>
    /// <typeparam name="TContext">コンテキスト型</typeparam>
    /// <typeparam name="TEvent">遷移ルール毎のイベントKeyの型</typeparam>
    /// <remarks>Memo: TEvent型はenumくらいしか指定しないのでwhere制約つけてもいいのかもしれない</remarks>
    public class StateMachine<TContext, TEvent> : IStateMachineContext<TContext>
    {
        private enum StatePhase
        {
            Idle,
            Entering,
            Updating,
            Exiting
        }

        private readonly Dictionary<Type, IState> _states = new();
        private readonly Dictionary<TEvent, Dictionary<IState, IState>> _fromToTransitionTable = new();
        private readonly Dictionary<TEvent, HashSet<IState>> _anyTransitionTable = new();

        private StatePhase _currentPhase = StatePhase.Idle;
        private IState _currentState;
        private IState _nextState;

        public TContext Context { get; }

        protected virtual bool AllowForceTransition => false;

        public StateMachine(TContext context)
        {
            Context = context;
        }

        #region Build

        /// <summary>
        /// 遷移ルールを遷移テーブルに登録します
        /// </summary>
        /// <param name="eventKey">遷移ルールを識別するイベントKey値</param>
        /// <typeparam name="TFromState">遷移元ステート</typeparam>
        /// <typeparam name="TToState">遷移先ステート</typeparam>
        /// <remarks>
        /// <para>イベントは遷移先ステートが判別できる名称が推奨されます</para>
        /// <para>イベント毎の遷移先リストを保持します</para>
        /// </remarks>
        public void AddTransition<TFromState, TToState>(TEvent eventKey)
            where TFromState : State<TContext, TEvent>, new()
            where TToState : State<TContext, TEvent>, new()
        {
            if (_currentState != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (!_fromToTransitionTable.TryGetValue(eventKey, out var transitionDict))
            {
                _fromToTransitionTable[eventKey] = transitionDict = new Dictionary<IState, IState>();
            }

            // WARN: Unity2020以降なら動作する
            // #if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            if (!transitionDict.TryAdd(from, to))
            {
                throw new InvalidOperationException($"Transition already exists: {typeof(TFromState).Name} -> {typeof(TToState).Name}, EventId: {eventKey}");
            }
        }

        /// <summary>
        /// 任意ステートから遷移先に指定できるステートを設定
        /// </summary>
        /// <remarks>WARN: 優先度が低く遷移テーブルに見つからない場合のみ使用されます</remarks>
        public void AddTransition<TAnyState>(TEvent eventKey) where TAnyState : State<TContext, TEvent>, new()
        {
            if (_currentState != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            var any = GetOrAddState<TAnyState>();

            if (!_anyTransitionTable.TryGetValue(eventKey, out var anySet))
            {
                _anyTransitionTable[eventKey] = anySet = new HashSet<IState>();
            }

            if (!anySet.Add(any))
            {
                throw new InvalidOperationException($"Transition already exists: {typeof(TAnyState).Name}, EventId: {eventKey}");
            }
        }

        /// <summary>
        /// ステートマシーン処理開始時に初期状態となるステートを設定
        /// </summary>
        public void SetInitState<TInitState>() where TInitState : State<TContext, TEvent>, new()
        {
            if (_currentState != null)
                throw new InvalidOperationException("State Machine is Processing!!");

            _nextState = GetOrAddState<TInitState>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TState GetOrAddState<TState>() where TState : State<TContext, TEvent>, new()
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

        #endregion

        #region Transition

        /// <summary>
        /// 遷移テーブルに基づいた遷移を実行
        /// </summary>
        /// <returns>StateEventResult: 遷移リクエストに対する応答</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StateEventResult Transition(TEvent eventKey)
        {
            if (_currentState == null)
                throw new InvalidOperationException("State Machine is not Processing!!");

            if (_currentPhase == StatePhase.Exiting)
                throw new InvalidOperationException("Exit Processing");

            // 前回の遷移を開始する前なので、まだ遷移できない
            if (_nextState != null)
                return StateEventResult.Waiting;

            if (_fromToTransitionTable.TryGetValue(eventKey, out var transitionDict) &&
                transitionDict.TryGetValue(_currentState, out var toState))
            {
                _nextState = toState;
                return StateEventResult.Succeeded;
            }

            if (_anyTransitionTable.TryGetValue(eventKey, out var anySet) &&
                anySet.Contains(_currentState))
            {
                _nextState = _currentState;
                return StateEventResult.Succeeded;
            }

            // 遷移情報が登録されていない
            return StateEventResult.Failed;
        }

        /// <summary>
        /// 遷移テーブルを無視したState直接指定の遷移
        /// WARN: 強制的に次に遷移すべきステートを上書きします
        /// </summary>
        public void ForceTransition<TState>() where TState : State<TContext, TEvent>, new()
        {
            if (_currentPhase == StatePhase.Exiting)
                throw new InvalidOperationException("Cannot transition during Exit");

            // 強制的な遷移が許可されていない
            if (!AllowForceTransition)
                throw new InvalidOperationException("Disallow force transition");

            _nextState = GetOrAddState<TState>();
        }

        #endregion

        #region Process

        /// <summary>
        /// 現在のステートが指定した型かどうかを判定
        /// </summary>
        /// <typeparam name="TState">判定するステート型</typeparam>
        /// <returns>現在のステートが指定型の場合true</returns>
        /// <exception cref="InvalidOperationException">ステートマシンが開始されていない場合</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCurrentState<TState>() where TState : State<TContext, TEvent>
        {
            if (_currentState == null) throw new InvalidOperationException("State Machine is not Processing!!");

            return _currentState.GetType() == typeof(TState);
        }

        /// <summary>
        /// ステートマシンが動作中かどうかを判定
        /// </summary>
        /// <returns>動作中の場合true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProcessing()
        {
            return _currentState != null;
        }

        /// <summary>
        /// ステートマシンを更新（毎フレーム呼び出し）
        /// 初回呼び出し時に初期ステートのEnterを実行し、以降は現在ステートのUpdateを実行
        /// 遷移リクエストがある場合は、Exit→Enterの順で遷移処理を行う
        /// </summary>
        /// <exception cref="InvalidOperationException">初期ステートが設定されていない場合</exception>
        public virtual void Update()
        {
            // プロセスが開始されていなければ、初期Stateをセットしてステートマシーンを起動する
            if (_currentState == null)
            {
                if (_nextState == null)
                    throw new InvalidOperationException("Next State is Nothing!!");

                // 実行ステートを変更
                {
                    _currentState = _nextState;
                    _nextState = null;
                }

                try
                {
                    _currentPhase = StatePhase.Entering;
                    _currentState.Enter();
                }
                catch (Exception e)
                {
                    _nextState = _currentState;
                    _currentState = null;
                    _currentPhase = StatePhase.Idle;
                    throw new InvalidOperationException($"State.Enter() failed in {_currentState?.GetType().Name}: {e.Message}\\n{e.StackTrace}");
                }

                if (_nextState == null)
                {
                    _currentPhase = StatePhase.Idle;
                    return;
                }
            }

            // ステートマシーン更新処理
            try
            {
                if (_nextState == null)
                {
                    _currentPhase = StatePhase.Updating;
                    _currentState.Update();
                }

                while (_nextState != null)
                {
                    _currentPhase = StatePhase.Exiting;
                    _currentState.Exit();

                    _currentState = _nextState;
                    _nextState = null;

                    _currentPhase = StatePhase.Entering;
                    _currentState.Enter();
                }

                _currentPhase = StatePhase.Idle;
            }
            catch (Exception e)
            {
                _currentPhase = StatePhase.Idle;
                throw new InvalidOperationException($"StateMachine.Update() failed in {_currentState?.GetType().Name}: {e.Message}\\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 物理演算タイミングで現在ステートのFixedUpdateを呼び出す
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void FixedUpdate()
        {
            _currentState?.FixedUpdate();
        }

        /// <summary>
        /// フレーム終了時に現在ステートのLateUpdateを呼び出す
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void LateUpdate()
        {
            _currentState?.LateUpdate();
        }

        #endregion
    }

    /// <summary>
    /// EventKeyがint型のステートマシーン
    /// </summary>
    public class StateMachine<TContext> : StateMachine<TContext, int>
    {
        public StateMachine(TContext context) : base(context)
        {
        }
    }
}