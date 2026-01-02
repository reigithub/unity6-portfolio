using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

#if ENABLE_IL2CPP
using UnityEngine.Scripting;
#endif

namespace Game.Core
{
    public interface IStateMachineContext<TContext>
    {
        public TContext Context { get; }
    }

    public abstract class State<TContext> : IStateMachineContext<TContext>
    {
        protected internal StateMachine<TContext> StateMachine { get; init; }
        public TContext Context => StateMachine.Context;

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

    public sealed class StateMachine<TContext> : IStateMachineContext<TContext>
    {
        private enum StateUpdateType
        {
            Idle,
            Enter,
            Update,
            Exit
        }

        private readonly HashSet<State<TContext>> _states = new();
        private readonly Dictionary<int, Dictionary<State<TContext>, State<TContext>>> _fromToTransitions = new();
        private readonly Dictionary<int, HashSet<State<TContext>>> _anyTransitions = new();

        private StateUpdateType _stateUpdateType = StateUpdateType.Idle;
        private State<TContext> _currentState;
        private State<TContext> _nextState;

        public TContext Context { get; }

        public StateMachine(TContext context)
        {
            Context = context;
        }

        public void AddTransition<TFromState, TToState>(int eventId)
            where TFromState : State<TContext>, new()
            where TToState : State<TContext>, new()
        {
            ThrowExceptionIfProcessing();

            var fromState = typeof(TFromState);
            var toState = typeof(TToState);

            if (!_fromToTransitions.ContainsKey(eventId))
                _fromToTransitions[eventId] = new Dictionary<State<TContext>, State<TContext>>();

            var from = GetOrAddState<TFromState>();
            var to = GetOrAddState<TToState>();

            if (from == null || to == null) return;

            if (_fromToTransitions[eventId].ContainsKey(from))
            {
                throw new InvalidOperationException($"Transition already exists: {fromState.Name} -> {toState.Name}, EventId: {eventId}");
            }

            _fromToTransitions[eventId][from] = to;
        }

        public void AddTransition<TAnyState>(int eventId) where TAnyState : State<TContext>, new()
        {
            ThrowExceptionIfProcessing();

            var anyState = typeof(TAnyState);

            if (!_anyTransitions.ContainsKey(eventId))
                _anyTransitions[eventId] = new HashSet<State<TContext>>();

            var any = GetOrAddState<TAnyState>();
            if (any == null) return;

            if (_anyTransitions[eventId].Contains(any))
            {
                throw new InvalidOperationException($"Transition already exists: {anyState.Name}, EventId: {eventId}");
            }

            _anyTransitions[eventId].Add(any);
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

        public bool TransitionState(int eventId)
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

        public void Update()
        {
            if (!IsProcessing())
            {
                if (_nextState == null)
                {
                    throw new InvalidOperationException("Next State is Nothing!!");
                }

                _currentState = _nextState;
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
                    ExceptionDispatchInfo.Capture(e).Throw();
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
            catch (Exception e)
            {
                _stateUpdateType = StateUpdateType.Idle;
                ExceptionDispatchInfo.Capture(e).Throw();
            }
        }

        public bool IsCurrentState<TState>() where TState : State<TContext>
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