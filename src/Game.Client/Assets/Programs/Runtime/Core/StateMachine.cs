#if ENABLE_IL2CPP
using UnityEngine.Scripting;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Game.Core
{
    public class StateMachine<TContext>
    {
        public abstract class State
        {
            protected internal StateMachine<TContext> StateMachine { get; set; }
            protected TContext Context => StateMachine.Context;

            protected internal virtual void Enter()
            {
            }

            protected internal virtual void Update()
            {
            }

            protected internal virtual void Exit()
            {
            }
        }

        private enum StateUpdateType
        {
            Idle,
            Enter,
            Update,
            Exit
        }

        private readonly HashSet<State> _states = new();
        private readonly Dictionary<int, Dictionary<State, State>> _fromToTransitions = new();
        private readonly Dictionary<int, HashSet<State>> _anyTransitions = new();

        private StateUpdateType _stateUpdateType = StateUpdateType.Idle;
        private State _currentState;
        private State _nextState;

        public TContext Context { get; }

        public StateMachine(TContext context)
        {
            Context = context;
        }

        public void AddTransition<TFromState, TToState>(int eventId)
            where TFromState : State, new()
            where TToState : State, new()
        {
            ThrowExceptionIfProcessing();

            var fromState = typeof(TFromState);
            var toState = typeof(TToState);

            if (!_fromToTransitions.ContainsKey(eventId))
                _fromToTransitions[eventId] = new Dictionary<State, State>();

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (from == null || to == null) return;

            if (_fromToTransitions[eventId].ContainsKey(from))
            {
                throw new InvalidOperationException($"Transition already exists: {fromState.Name} -> {toState.Name}, EventId: {eventId}");
            }

            _fromToTransitions[eventId][from] = to;
        }

        public void AddTransition<TAnyState>(int eventId) where TAnyState : State, new()
        {
            ThrowExceptionIfProcessing();

            var anyState = typeof(TAnyState);

            if (!_anyTransitions.ContainsKey(eventId))
                _anyTransitions[eventId] = new HashSet<State>();

            var any = GetOrAddState<TAnyState>();
            if (any == null) return;

            if (_anyTransitions[eventId].Contains(any))
            {
                throw new InvalidOperationException($"Transition already exists: {anyState.Name}, EventId: {eventId}");
            }

            _anyTransitions[eventId].Add(any);
        }

        public void SetStartState<TStartState>() where TStartState : State, new()
        {
            ThrowExceptionIfProcessing();

            _nextState = GetOrAddState<TStartState>();
        }

        private TState GetOrAddState<TState>() where TState : State, new()
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

        public virtual bool TransitionState(int eventId)
        {
            ThrowExceptionIfNotProcessing();

            if (_stateUpdateType == StateUpdateType.Exit)
                throw new InvalidOperationException("Exit Processing");

            if (_currentState == null || _nextState != null)
                return false;

            if (_fromToTransitions.ContainsKey(eventId) && _fromToTransitions[eventId].ContainsKey(_currentState))
            {
                var nextState = _fromToTransitions[eventId][_currentState];
                _currentState = nextState;
            }
            else if (_anyTransitions.ContainsKey(eventId) && _anyTransitions[eventId].Contains(_currentState))
            {
                var nextState = _anyTransitions[eventId].FirstOrDefault(x => x == _currentState);
                _currentState = nextState;
            }
            else
            {
                // 遷移情報が登録されていない
                return false;
            }

            return true;
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
            if (!IsProcessing())
            {
                if (_nextState == null)
                {
                    throw new InvalidOperationException("Next State is Nothing!!");
                }

                // 現在処理中ステートとして設定する
                _currentState = _nextState;
                _nextState = null;

                try
                {
                    _stateUpdateType = StateUpdateType.Enter;
                    _currentState.Enter();
                }
                catch (Exception exception)
                {
                    // 起動時の復帰は現在のステートにnullが入っていないとまずいので遷移前の状態に戻す
                    _nextState = _currentState;
                    _currentState = null;

                    _stateUpdateType = StateUpdateType.Idle;
                    DoHandleException(exception);
                    return;
                }

                if (_nextState == null)
                {
                    _stateUpdateType = StateUpdateType.Idle;
                    return;
                }
            }

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
            catch (Exception exception)
            {
                _stateUpdateType = StateUpdateType.Idle;
                DoHandleException(exception);
                return;
            }
        }

        private void DoHandleException(Exception exception)
        {
            if (exception == null)
                throw new Exception(nameof(exception));

            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        public bool IsCurrentState<TState>() where TState : State
        {
            ThrowExceptionIfNotProcessing();

            return _currentState.GetType() == typeof(TState);
        }

        public string GetCurrentStateName()
        {
            return IsProcessing() ? _currentState.GetType().Name : string.Empty;
        }
    }
}