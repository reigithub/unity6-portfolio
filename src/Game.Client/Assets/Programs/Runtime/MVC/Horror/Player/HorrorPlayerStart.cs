using System;
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
            if (string.IsNullOrEmpty(master.ModelAssetName))
                throw new InvalidOperationException($"{nameof(HorrorPlayerMaster)}(Id={master.Id}) の {nameof(master.ModelAssetName)} が未設定です");

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
