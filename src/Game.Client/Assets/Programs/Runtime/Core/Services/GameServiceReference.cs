namespace Game.Core.Services
{
    /// <summary>
    /// ゲームサービスへの参照
    /// </summary>
    /// <typeparam name="TService">サービス</typeparam>
    public struct GameServiceReference<TService>
        where TService : GameService, new()
    {
        public TService Reference => GameServiceManager.Instance.GetService<TService>();
    }
}