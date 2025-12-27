using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Services
{
    public partial class GameServiceManager
    {
        public static readonly GameServiceManager Instance = new();

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
            ClearCacheIfTransientOnMemory();
            TryGetOrAddService<T>(out var service);
            return service;
        }

        public void AddService<T>()
            where T : GameService, new()
        {
            TryGetOrAddService<T>(out _);
        }

        private void ClearCacheIfTransientOnMemory()
        {
            var transientCount = _gameServiceByName.Values.Count(x => !x.AllowResidentOnMemory);
            if (transientCount > TransientOnMemoryBuffer)
                return;

            var (name, service) = _gameServiceByName.FirstOrDefault(x => !x.Value.AllowResidentOnMemory);
            if (service is null)
                return;

            service.Shutdown();
            _gameServiceByName.Remove(name);
        }
    }
}