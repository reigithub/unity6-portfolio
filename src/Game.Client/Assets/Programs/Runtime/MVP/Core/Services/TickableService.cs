using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// 動的なTick登録を管理するサービス
    /// VContainerのPlayerLoop統合を利用し、登録されたActionを各タイミングで実行
    /// </summary>
    public class TickableService : ITickableService, ITickable, IFixedTickable, ILateTickable, IDisposable
    {
        private readonly TickActionList _tickActions = new();
        private readonly TickActionList _fixedTickActions = new();
        private readonly TickActionList _lateTickActions = new();

        public void Register<T>(Action action) where T : class
        {
            if (action == null) return;
            GetActionList<T>().Add(action);
        }

        public void Unregister<T>(Action action) where T : class
        {
            if (action == null) return;
            GetActionList<T>().Remove(action);
        }

        private TickActionList GetActionList<T>() where T : class
        {
            var type = typeof(T);

            if (type == typeof(ITickable))
                return _tickActions;
            if (type == typeof(IFixedTickable))
                return _fixedTickActions;
            if (type == typeof(ILateTickable))
                return _lateTickActions;

            throw new ArgumentException($"Unsupported tickable type: {type.Name}. Use ITickable, IFixedTickable, or ILateTickable.");
        }

        void ITickable.Tick() => _tickActions.Execute();
        void IFixedTickable.FixedTick() => _fixedTickActions.Execute();
        void ILateTickable.LateTick() => _lateTickActions.Execute();

        public void Dispose()
        {
            _tickActions.Clear();
            _fixedTickActions.Clear();
            _lateTickActions.Clear();
        }

        /// <summary>
        /// イテレーション中の追加・削除に対応したActionリスト
        /// </summary>
        private class TickActionList
        {
            private readonly List<Action> _actions = new();
            private readonly List<Action> _pendingAdds = new();
            private readonly List<Action> _pendingRemoves = new();
            private bool _isIterating;

            public void Add(Action action)
            {
                if (_isIterating)
                {
                    _pendingAdds.Add(action);
                }
                else
                {
                    _actions.Add(action);
                }
            }

            public void Remove(Action action)
            {
                if (_isIterating)
                {
                    _pendingRemoves.Add(action);
                }
                else
                {
                    _actions.Remove(action);
                }
            }

            public void Execute()
            {
                _isIterating = true;

                foreach (var action in _actions)
                {
                    action?.Invoke();
                }

                _isIterating = false;

                // 保留中の追加・削除を処理
                if (_pendingAdds.Count > 0)
                {
                    foreach (var action in _pendingAdds)
                    {
                        _actions.Add(action);
                    }
                    _pendingAdds.Clear();
                }

                if (_pendingRemoves.Count > 0)
                {
                    foreach (var action in _pendingRemoves)
                    {
                        _actions.Remove(action);
                    }
                    _pendingRemoves.Clear();
                }
            }

            public void Clear()
            {
                _actions.Clear();
                _pendingAdds.Clear();
                _pendingRemoves.Clear();
            }
        }
    }
}
