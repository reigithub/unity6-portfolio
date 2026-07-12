using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Extensions;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// プレイヤー生成地点
    /// </summary>
    public class HorrorPlayerStart : MonoBehaviour
    {
        private GameObject _player;

        public async UniTask<HorrorPlayerController> LoadPlayerAsync()
        {
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _player = await assetService.InstantiateAsync("HorrorPlayer", transform);
            if (_player.TryGetComponent<HorrorPlayerController>(out var playerController))
            {
                return playerController;
            }

            throw new MissingComponentException($"Cannot find {nameof(HorrorPlayerController)}");
        }

        public void UnloadPlayer()
        {
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            assetService.ReleaseInstance(_player);
        }
    }
}
