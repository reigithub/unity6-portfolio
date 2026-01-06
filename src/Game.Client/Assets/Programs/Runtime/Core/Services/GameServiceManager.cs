using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Services
{
    public partial class GameServiceManager
    {
        private static readonly Lazy<GameServiceManager> InstanceLazy = new(() => new GameServiceManager());
        public static GameServiceManager Instance => InstanceLazy.Value;

        // public static readonly GameServiceManager Instance = new();

        private readonly Dictionary<string, GameService> _gameServiceByName = new();

        private const int TransientOnMemoryBuffer = 10;

        private GameServiceManager()
        {
        }

        private void Initialize()
        {
            _gameServiceByName.Clear();
        }

        public void StartUp()
        {
            Initialize();
        }

        public void Shutdown()
        {
            Initialize();
        }

        public bool TryGetOrAddService<T>(out T service)
            where T : GameService, new()
        {
            service = null;
            var name = typeof(T).Name;
            if (_gameServiceByName.TryGetValue(name, out var cache))
            {
                service = (T)cache;
                return false;
            }

            service = new T();
            service.Startup();
            _gameServiceByName.Add(name, service);
            return true;
        }

        public T GetService<T>()
            where T : GameService, new()
        {
            TryGetOrAddService<T>(out var service);
            ClearCacheIfTransientOnMemory<T>();
            return service;
        }

        public void StartupService<T>()
            where T : GameService, new()
        {
            TryGetOrAddService<T>(out _);
        }

        public void ShutdownService<T>()
            where T : GameService
        {
            var name = typeof(T).Name;
            if (_gameServiceByName.TryGetValue(name, out var service))
            {
                service.Shutdown();
                _gameServiceByName.Remove(name);
            }
        }

        private void ClearCacheIfTransientOnMemory<T>()
            where T : GameService
        {
            var transientCount = _gameServiceByName.Values
                .Count(x => x.GetType() != typeof(T) && !x.AllowResidentOnMemory);
            if (transientCount <= TransientOnMemoryBuffer)
                return;

            var (name, service) = _gameServiceByName
                .FirstOrDefault(x => x.GetType() != typeof(T) && !x.Value.AllowResidentOnMemory);
            if (service is null)
                return;

            service.Shutdown();
            _gameServiceByName.Remove(name);
        }
    }
}