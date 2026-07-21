using System;
using System.Collections.Generic;
using Game.Shared.Services.Interfaces;

namespace Game.Core.Services
{
    public class GameServiceManager
    {
        private static readonly Lazy<GameServiceManager> _instanceLazy = new(() => new GameServiceManager());
        public static GameServiceManager Instance => _instanceLazy.Value;

        private readonly Dictionary<Type, IGameService> _gameServices = new();

        private GameServiceManager()
        {
        }

        public static void StartUp()
        {
            Instance._gameServices.Clear();
        }

        public static void Shutdown()
        {
            foreach (var gameService in Instance._gameServices.Values)
                gameService.Shutdown();

            Instance._gameServices.Clear();
        }

        /// <summary>
        /// 生成済みインスタンス登録
        /// </summary>
        public static void Register<TInterface, TImplement>(TImplement service)
            where TImplement : TInterface, IGameService
        {
            var type = typeof(TInterface);
            if (Instance._gameServices.ContainsKey(type))
                return;

            service.Startup();
            Instance._gameServices[type] = service;
        }

        /// <summary>
        /// Memo: テスト用サービス登録窓口
        /// </summary>
        public static void Register<TInterface>(TInterface service)
            where TInterface : IGameService
        {
            var type = typeof(TInterface);
            if (Instance._gameServices.ContainsKey(type))
                return;

            service.Startup();
            Instance._gameServices[type] = service;
        }

        public static T Resolve<T>()
        {
            return (T)Instance._gameServices[typeof(T)];
        }

        public static void Unregister<T>()
        {
            var type = typeof(T);
            if (!Instance._gameServices.TryGetValue(type, out var service))
                return;

            service.Shutdown();
            Instance._gameServices.Remove(type);
        }
    }
}
