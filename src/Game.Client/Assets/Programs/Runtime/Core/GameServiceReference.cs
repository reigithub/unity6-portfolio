namespace Game.Core
{
    public struct GameServiceReference<TService>
        where TService : GameService, new()
    {
        private TService _service;

        public TService Service
            => _service ??= GameServiceManager.Instance.GetService<TService>();

        // public static implicit operator TService(GameServiceReference<TService> reference)
        //     => reference.Service;
    }
}