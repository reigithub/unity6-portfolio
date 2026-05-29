using Cysharp.Threading.Tasks;
using Game.Core.Services;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// プレイヤー生成地点
    /// </summary>
    public class HorrorPlayerStart : MonoBehaviour
    {
        public async UniTask<GameObject> LoadPlayerAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var player = await assetService.InstantiateAsync("HorrorPlayer", transform);
            if (player.TryGetComponent<HorrorPlayerController>(out var playerController))
            {
                playerController.Initialize();
            }

            return player;
        }
    }
}
