using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Editor.Tests
{
    internal interface IOldState
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

    internal interface IOldStateMachineContext<out TContext>
    {
        public TContext Context { get; }
    }

    public abstract class OldState<TContext, TEvent> : IOldState, IOldStateMachineContext<TContext>
    {
        protected internal OldStateMachine<TContext, TEvent> StateMachine { get; set; }
        public TContext Context => StateMachine.Context;

        public virtual void Enter()
        {
        }

        public virtual void Update()
        {
        }

        public virtual void FixedUpdate()
        {
        }

        public virtual void LateUpdate()
        {
        }

        public virtual void Exit()
        {
        }
    }

    public enum OldStateEventResult
    {
        Waiting,   // 遷移リクエストしたが順番待ち、次回Updateで再度リクエスト
        Succeeded, // 遷移リクエストが受付られ、次回Updateで処理される
        Failed     // 遷移テーブルにないリクエスト
    }

    /// <summary>
    /// ステートマシーン
    /// </summary>
    /// <typeparam name="TContext">コンテキスト型</typeparam>
    /// <typeparam name="TEventKey">遷移ルール毎のイベントKeyの型</typeparam>
    /// <remarks>Memo: TEventKey型はenumくらいしか指定しないのでwhere制約つけてもいいのかもしれない</remarks>
    public class OldStateMachine<TContext, TEventKey> : IOldStateMachineContext<TContext>
    {
        private enum StateUpdateType
        {
            Idle,
            Enter,
            Update,
            Exit
        }

        private readonly HashSet<IOldState> _states = new();
        private readonly Dictionary<TEventKey, Dictionary<IOldState, IOldState>> _fromToTransitionTable = new();
        private readonly Dictionary<TEventKey, HashSet<IOldState>> _anyTransitionTable = new();

        private StateUpdateType _stateUpdateType = StateUpdateType.Idle;
        private IOldState _currentState;
        private IOldState _nextState;

        public TContext Context { get; }

        protected virtual bool AllowForceTransition => false;

        public OldStateMachine(TContext context)
        {
            Context = context;
        }

        #region Build

        /// <summary>
        /// 遷移ルールを追加
        /// </summary>
        /// <param name="eventKey">遷移ルールを識別するイベントKey値</param>
        /// <typeparam name="TFromState">遷移元ステート</typeparam>
        /// <typeparam name="TToState">遷移先ステート</typeparam>
        /// <remarks>
        /// <para>イベントKeyは遷移先ステートが判別できる名称が推奨されます</para>
        /// <para>イベントKey毎の遷移リストを保持します</para>
        /// </remarks>
        public void AddTransition<TFromState, TToState>(TEventKey eventKey)
            where TFromState : OldState<TContext, TEventKey>, new()
            where TToState : OldState<TContext, TEventKey>, new()
        {
            ThrowExceptionIfProcessing();

            var fromState = typeof(TFromState);
            var toState = typeof(TToState);

            if (!_fromToTransitionTable.ContainsKey(eventKey))
                _fromToTransitionTable[eventKey] = new Dictionary<IOldState, IOldState>();

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (from == null || to == null) return;

            if (_fromToTransitionTable[eventKey].ContainsKey(from))
            {
                throw new InvalidOperationException($"Transition already exists: {fromState.Name} -> {toState.Name}, EventId: {eventKey}");
            }

            _fromToTransitionTable[eventKey][from] = to;
        }

        /// <summary>
        /// 任意ステートから遷移先に指定できるステートを設定
        /// </summary>
        /// <remarks>WARN: 優先度が低く遷移テーブルに見つからない場合のみ使用されます</remarks>
        public void AddTransition<TAnyState>(TEventKey eventKey) where TAnyState : OldState<TContext, TEventKey>, new()
        {
            ThrowExceptionIfProcessing();

            var anyState = typeof(TAnyState);

            if (!_anyTransitionTable.ContainsKey(eventKey))
                _anyTransitionTable[eventKey] = new HashSet<IOldState>();

            var any = GetOrAddState<TAnyState>();
            if (any == null) return;

            if (_anyTransitionTable[eventKey].Contains(any))
            {
                throw new InvalidOperationException($"Transition already exists: {anyState.Name}, EventId: {eventKey}");
            }

            _anyTransitionTable[eventKey].Add(any);
        }

        /// <summary>
        /// ステートマシーン処理開始時に初期状態となるステートを設定
        /// </summary>
        public void SetInitState<TInitState>() where TInitState : OldState<TContext, TEventKey>, new()
        {
            ThrowExceptionIfProcessing();

            _nextState = GetOrAddState<TInitState>();
        }

        private TState GetOrAddState<TState>() where TState : OldState<TContext, TEventKey>, new()
        {
            var stateType = typeof(TState);
            foreach (var state in _states)
            {
                if (state.GetType() == stateType)
                {
                    return (TState)state;
                }
            }

            var newState = new TState { StateMachine = this };
            _states.Add(newState);
            return newState;
        }

        #endregion

        #region Transition

        /// <summary>
        /// 遷移テーブルに基づいた遷移を実行
        /// </summary>
        /// <param name="eventKey">どの遷移を実行するかを管理するKeyを指定</param>
        public OldStateEventResult Transition(TEventKey eventKey)
        {
            ThrowExceptionIfNotProcessing();

            if (_stateUpdateType == StateUpdateType.Exit)
                throw new InvalidOperationException("Exit Processing");

            // 前回の遷移を開始する前なので、まだ遷移できない
            if (_currentState == null || _nextState != null)
                return OldStateEventResult.Waiting;

            // 遷移テーブルから次の遷移先を更新
            if (_fromToTransitionTable.ContainsKey(eventKey) && _fromToTransitionTable[eventKey].ContainsKey(_currentState))
            {
                _nextState = _fromToTransitionTable[eventKey][_currentState];
            }
            else if (_anyTransitionTable.ContainsKey(eventKey) && _anyTransitionTable[eventKey].Contains(_currentState))
            {
                _nextState = _anyTransitionTable[eventKey].FirstOrDefault(x => x == _currentState);
            }
            else
            {
                // 遷移情報が登録されていない
                return OldStateEventResult.Failed;
            }

            return OldStateEventResult.Succeeded;
        }

        /// <summary>
        /// 遷移テーブルを無視したState直接指定の遷移
        /// WARN: 強制的に次に遷移すべきステートを上書きします
        /// </summary>
        public void ForceTransition<TState>() where TState : OldState<TContext, TEventKey>, new()
        {
            if (_stateUpdateType == StateUpdateType.Exit)
                throw new InvalidOperationException("Cannot transition during Exit");

            // 強制的な遷移が許可されていない
            if (!AllowForceTransition)
                return;

            _nextState = GetOrAddState<TState>();
        }

        #endregion

        #region Process

        public bool IsCurrentState<TState>() where TState : OldState<TContext, TEventKey>
        {
            ThrowExceptionIfNotProcessing();

            return _currentState.GetType() == typeof(TState);
        }

        public bool IsProcessing()
        {
            return _currentState != null;
        }

        private void ThrowExceptionIfProcessing()
        {
            if (IsProcessing()) throw new InvalidOperationException("State Machine is Processing!!");
        }

        private void ThrowExceptionIfNotProcessing()
        {
            if (!IsProcessing()) throw new InvalidOperationException("State Machine is not Processing!!");
        }

        public virtual void Update()
        {
            // プロセスが開始されていなければ、初期Stateをセットしてステートマシーンを起動する
            if (!IsProcessing())
            {
                _currentState = _nextState ?? throw new InvalidOperationException("Next State is Nothing!!");
                _nextState = null;

                try
                {
                    _stateUpdateType = StateUpdateType.Enter;
                    _currentState.Enter();
                }
                catch (Exception)
                {
                    _nextState = _currentState;
                    _currentState = null;

                    _stateUpdateType = StateUpdateType.Idle;
                    throw;
                }

                if (_nextState == null)
                {
                    _stateUpdateType = StateUpdateType.Idle;
                    return;
                }
            }

            // ステートマシーン更新処理
            try
            {
                if (_nextState == null)
                {
                    _stateUpdateType = StateUpdateType.Update;
                    _currentState.Update();
                }

                while (_nextState != null)
                {
                    _stateUpdateType = StateUpdateType.Exit;
                    _currentState.Exit();

                    _currentState = _nextState;
                    _nextState = null;

                    _stateUpdateType = StateUpdateType.Enter;
                    _currentState.Enter();
                }

                _stateUpdateType = StateUpdateType.Idle;
            }
            catch (Exception)
            {
                _stateUpdateType = StateUpdateType.Idle;
                // ExceptionDispatchInfo.Capture(e).Throw();
                throw;
            }
        }

        public virtual void FixedUpdate()
        {
            if (_currentState != null)
                _currentState.FixedUpdate();
        }

        public virtual void LateUpdate()
        {
            if (_currentState != null)
                _currentState.LateUpdate();
        }

        #endregion
    }
}