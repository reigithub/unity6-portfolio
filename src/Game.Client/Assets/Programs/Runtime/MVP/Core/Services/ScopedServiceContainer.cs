using System;
using System.Collections.Generic;
using VContainer;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// 動的なサービスライフサイクル管理
    /// インゲーム専用サービス等、特定期間のみ存在するサービスを管理
    /// </summary>
    public class ScopedServiceContainer : IScopedServiceContainer, IDisposable
    {
        private readonly IObjectResolver _resolver;
        private readonly Dictionary<Type, object> _services = new();

        public ScopedServiceContainer(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public T Add<T>() where T : class, new()
        {
            var type = typeof(T);

            if (_services.TryGetValue(type, out var existing))
            {
                return (T)existing;
            }

            var service = new T();
            _resolver.Inject(service);
            _services[type] = service;

            return service;
        }

        public T Get<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service) ? (T)service : null;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = null;
            return false;
        }

        public void Remove<T>() where T : class
        {
            if (_services.Remove(typeof(T), out var service))
            {
                (service as IDisposable)?.Dispose();
            }
        }

        public bool Contains<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        public void Dispose()
        {
            foreach (var service in _services.Values)
            {
                (service as IDisposable)?.Dispose();
            }
            _services.Clear();
        }
    }
}
