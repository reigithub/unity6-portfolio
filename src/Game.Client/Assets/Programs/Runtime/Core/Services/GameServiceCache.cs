namespace Game.Core.Services
{
    /// <summary>
    /// ゲームサービスのキャッシュ作成
    /// </summary>
    /// <typeparam name="TService">サービス</typeparam>
    public struct GameServiceCache<TService>
        where TService : GameService, new()
    {
        private TService _service;

        public TService Service
            => _service ??= GameServiceManager.Instance.GetService<TService>();

        // public static implicit operator TService(GameServiceReference<TService> reference)
        //     => reference.Service;
    }
}