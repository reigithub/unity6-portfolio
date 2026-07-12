using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Client.MasterData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.ScoreTimeAttack.Player
{
    /// <summary>
    /// プレイヤー生成地点
    /// プレイヤーとHUDの生成のみを担当し、それぞれの連携はMessagePipe経由で行う
    /// </summary>
    public class ScoreTimeAttackPlayerStart : MonoBehaviour
    {
        /// <summary>
        /// プレイヤーとHUDを生成する
        /// </summary>
        /// <param name="playerMaster">プレイヤーマスターデータ</param>
        /// <param name="collisionHandler">衝突イベントハンドラー（オプション）</param>
        public async UniTask<GameObject> LoadPlayerAsync(
            ScoreTimeAttackPlayerMaster playerMaster,
            IPlayerCollisionHandler collisionHandler = null)
        {
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

            // プレイヤー生成
            var player = await assetService.InstantiateAsync(playerMaster.AssetName, transform);
            if (player.TryGetComponent<SDUnityChanPlayerController>(out var playerController))
            {
                playerController.Initialize(playerMaster, collisionHandler);
            }

            // HUD生成
            var playerHUD = await assetService.InstantiateAsync("ScoreTimeAttackPlayerHUD", transform);
            if (playerHUD.TryGetComponent<ScoreTimeAttackPlayerHUD>(out var hud))
            {
                hud.Initialize(playerMaster);
            }

            // プレイヤー生成通知
            messagePipeService.Publish(MessageKey.Player.SpawnPlayer, player);

            return player;
        }
    }
}
