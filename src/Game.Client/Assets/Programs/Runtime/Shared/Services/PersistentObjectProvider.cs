using System;
using System.Collections.Generic;

namespace Game.Shared.Services
{
    /// <summary>
    /// 永続オブジェクトプロバイダーの実装
    /// </summary>
    public class PersistentObjectProvider : IPersistentObjectProvider
    {
        private readonly Dictionary<Type, object> _objects = new();

        public void Register<T>(T instance) where T : class
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            var type = typeof(T);
            if (_objects.ContainsKey(type))
            {
                throw new InvalidOperationException(
                    $"[PersistentObjectProvider] Type '{type.Name}' is already registered. " +
                    "Call Unregister first if you want to replace it.");
            }

            _objects[type] = instance;
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var instance))
            {
                return instance;
            }

            throw new InvalidOperationException(
                $"[PersistentObjectProvider] Type '{typeof(T).Name}' is not registered.");
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            if (_objects.TryGetValue(typeof(T), out var obj))
            {
                instance = (T)obj;
                return true;
            }

            instance = null;
            return false;
        }

        public void Unregister<T>() where T : class
        {
            _objects.Remove(typeof(T));
        }

        public void Clear()
        {
            _objects.Clear();
        }
    }
}
