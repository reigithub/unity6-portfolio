using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Scriptable.Database.Tables;
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

        public async UniTask<HorrorPlayerController> LoadPlayerAsync(HorrorPlayerMaster master)
        {
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _player = await assetService.InstantiateAsync(master.ModelAssetName, transform);
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
