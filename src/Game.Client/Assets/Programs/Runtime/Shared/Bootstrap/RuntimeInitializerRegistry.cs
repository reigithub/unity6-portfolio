using System;
using System.Collections.Generic;

namespace Game.Shared.Bootstrap
{
    public static class RuntimeInitializerRegistry
    {
        private static readonly List<(int Order, Action Callback)> _callbacks = new();

        public static void Register(int order, Action callback)
        {
            _callbacks.Add((order, callback));
        }

        public static void ExecuteAll()
        {
            _callbacks.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var (_, callback) in _callbacks)
            {
                callback();
            }
            _callbacks.Clear();
        }
    }
}
