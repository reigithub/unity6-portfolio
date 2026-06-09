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
        public async UniTask<HorrorPlayerController> LoadPlayerAsync()
        {
            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var player = await assetService.InstantiateAsync("HorrorPlayer", transform);
            if (player.TryGetComponent<HorrorPlayerController>(out var playerController))
            {
                return playerController;
            }

            throw new MissingComponentException($"Cannot find {nameof(HorrorPlayerController)}");
        }
    }
}
