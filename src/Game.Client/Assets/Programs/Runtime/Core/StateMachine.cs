using System;
using System.Collections.Generic;
using System.Linq;

#if ENABLE_IL2CPP
using UnityEngine.Scripting;
#endif

namespace Game.Core
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

    public interface IStateMachineContext<out TContext>
    {
        public TContext Context { get; }
    }

    public abstract class State<TContext> : IState, IStateMachineContext<TContext>
    {
        protected internal StateMachine<TContext> StateMachine { get; init; }
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

    public class StateMachine<TContext> : IStateMachineContext<TContext>
    {
        private enum StateUpdateType
        {
            Idle,
            Enter,
            Update,
            Exit
        }

        private readonly HashSet<IState> _states = new();
        private readonly Dictionary<int, Dictionary<IState, IState>> _fromToTransitionTable = new();
        private readonly Dictionary<int, HashSet<IState>> _anyTransitionTable = new();

        private StateUpdateType _stateUpdateType = StateUpdateType.Idle;
        private IState _currentState;
        private IState _nextState;

        public TContext Context { get; }

        public StateMachine(TContext context)
        {
            Context = context;
        }

        public void AddTransition<TFromState, TToState>(int eventKey)
            where TFromState : State<TContext>, new()
            where TToState : State<TContext>, new()
        {
            ThrowExceptionIfProcessing();

            var fromState = typeof(TFromState);
            var toState = typeof(TToState);

            if (!_fromToTransitionTable.ContainsKey(eventKey))
                _fromToTransitionTable[eventKey] = new Dictionary<IState, IState>();

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (from == null || to == null) return;

            if (_fromToTransitionTable[eventKey].ContainsKey(from))
            {
                throw new InvalidOperationException($"Transition already exists: {fromState.Name} -> {toState.Name}, EventId: {eventKey}");
            }

            _fromToTransitionTable[eventKey][from] = to;
        }

        public void AddTransition<TAnyState>(int eventKey) where TAnyState : State<TContext>, new()
        {
            ThrowExceptionIfProcessing();

            var anyState = typeof(TAnyState);

            if (!_anyTransitionTable.ContainsKey(eventKey))
                _anyTransitionTable[eventKey] = new HashSet<IState>();

            var any = GetOrAddState<TAnyState>();
            if (any == null) return;

            if (_anyTransitionTable[eventKey].Contains(any))
            {
                throw new InvalidOperationException($"Transition already exists: {anyState.Name}, EventId: {eventKey}");
            }

            _anyTransitionTable[eventKey].Add(any);
        }

        public void SetInitState<TInitState>() where TInitState : State<TContext>, new()
        {
            ThrowExceptionIfProcessing();

            _nextState = GetOrAddState<TInitState>();
        }

        private TState GetOrAddState<TState>() where TState : State<TContext>, new()
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

        /// <summary>
        /// 遷移テーブルに基づいた遷移を実行
        /// </summary>
        /// <param name="eventKey">どの遷移を実行するかを管理するKeyを指定</param>
        public bool Transition(int eventKey)
        {
            ThrowExceptionIfNotProcessing();

            if (_stateUpdateType == StateUpdateType.Exit)
                throw new InvalidOperationException("Exit Processing");

            if (_currentState == null || _nextState != null)
                return false;

            if (_fromToTransitionTable.ContainsKey(eventKey) && _fromToTransitionTable[eventKey].ContainsKey(_currentState))
            {
                var nextState = _fromToTransitionTable[eventKey][_currentState];
                _currentState = nextState;
            }
            else if (_anyTransitionTable.ContainsKey(eventKey) && _anyTransitionTable[eventKey].Contains(_currentState))
            {
                var nextState = _anyTransitionTable[eventKey].FirstOrDefault(x => x == _currentState);
                _currentState = nextState;
            }
            else
            {
                // 遷移情報が登録されていない
                return false;
            }

            return true;
        }

        /// <summary>
        /// 遷移テーブルを無視したState直接指定の遷移
        /// </summary>
        public void TransitionTo<TState>() where TState : State<TContext>, new()
        {
            if (_stateUpdateType == StateUpdateType.Exit)
                throw new InvalidOperationException("Cannot transition during Exit");

            _nextState = GetOrAddState<TState>();
        }

        public bool IsCurrentState<TState>() where TState : State<TContext>
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
            if (IsProcessing())
                throw new InvalidOperationException("State Machine is Running!!");
        }

        private void ThrowExceptionIfNotProcessing()
        {
            if (!IsProcessing())
                throw new InvalidOperationException("State Machine is not Running!!");
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
                catch (Exception e)
                {
                    _nextState = _currentState;
                    _currentState = null;

                    _stateUpdateType = StateUpdateType.Idle;
                    // ExceptionDispatchInfo.Capture(e).Throw();
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
            catch (Exception e)
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
    }
}