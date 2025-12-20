using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public partial class GameServiceManager
    {
        public static readonly GameServiceManager Instance = new();

        private readonly Dictionary<string, GameService> _gameServiceByName = new();

        private int _nonResidentOnMemoryCount;
        private const int NonResidentOnMemoryBuffer = 10;

        private GameServiceManager()
        {
        }

        private void Initialize()
        {
            _gameServiceByName.Clear();
            _nonResidentOnMemoryCount = 0;
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
            if (!service.AllowResidentOnMemory)
                _nonResidentOnMemoryCount++;

            _gameServiceByName.Add(name, service);
            return true;
        }

        public T GetService<T>()
            where T : GameService, new()
        {
            ClearCacheIfNonResidentOnMemory();
            TryGetOrAddService<T>(out var service);
            return service;
        }

        public void AddService<T>()
            where T : GameService, new()
        {
            TryGetOrAddService<T>(out _);
        }

        private void ClearCacheIfNonResidentOnMemory()
        {
            if (_nonResidentOnMemoryCount >= NonResidentOnMemoryBuffer)
                return;

            var (name, service) = _gameServiceByName
                .FirstOrDefault(x => !x.Value.AllowResidentOnMemory);
            if (service is null)
                return;

            service.Shutdown();
            _gameServiceByName.Remove(name);
            _nonResidentOnMemoryCount--;
        }
    }
}