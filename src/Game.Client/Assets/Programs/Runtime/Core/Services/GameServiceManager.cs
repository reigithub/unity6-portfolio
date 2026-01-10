using System;
using System.Collections.Generic;

namespace Game.Core.Services
{
    public class GameServiceManager
    {
        private static readonly Lazy<GameServiceManager> InstanceLazy = new(() => new GameServiceManager());
        public static GameServiceManager Instance => InstanceLazy.Value;

        private readonly Dictionary<Type, IGameService> _gameServices = new();

        private GameServiceManager()
        {
        }

        public void StartUp()
        {
            _gameServices.Clear();
        }

        public void Shutdown()
        {
            _gameServices.Clear();
        }

        private bool TryGetOrAddService<T>(out T service)
            where T : IGameService, new()
        {
            var type = typeof(T);
            if (_gameServices.TryGetValue(type, out var cache))
            {
                service = (T)cache;
                return false;
            }

            service = new T();
            service.Startup();
            _gameServices.Add(type, service);
            return true;
        }

        public T GetService<T>()
            where T : IGameService, new()
        {
            TryGetOrAddService<T>(out var service);
            return service;
        }

        public void StartupService<T>()
            where T : IGameService, new()
        {
            TryGetOrAddService<T>(out _);
        }

        public void ShutdownService<T>()
            where T : IGameService
        {
            var type = typeof(T);
            if (_gameServices.TryGetValue(type, out var service))
            {
                service.Shutdown();
                _gameServices.Remove(type);
            }
        }
    }
}