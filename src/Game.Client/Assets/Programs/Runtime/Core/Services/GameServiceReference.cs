namespace Game.Core.Services
{
    /// <summary>
    /// ゲームサービスへの参照
    /// </summary>
    /// <typeparam name="TService">サービス</typeparam>
    public struct GameServiceReference<TService> where TService : IGameService, new()
    {
        private TService _reference;
        public TService Reference => _reference ??= GameServiceManager.Instance.GetService<TService>();

        // public static implicit operator TService(GameServiceReference<TService> reference)
        //     => reference.Service;
    }
}