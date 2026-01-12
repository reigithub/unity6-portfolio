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
            foreach (var gameService in _gameServices.Values)
            {
                gameService.Shutdown();
            }

            _gameServices.Clear();
        }

        private bool TryGetOrAdd<T>(out T service)
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

        public static T Get<T>() where T : IGameService, new()
        {
            Instance.TryGetOrAdd<T>(out var service);
            return service;
        }

        public void Startup<T>()
            where T : IGameService, new()
        {
            TryGetOrAdd<T>(out _);
        }

        public void Shutdown<T>()
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