using Cysharp.Threading.Tasks;
using Game.Core.MasterData.MemoryTables;
using Game.Core.MessagePipe;
using Game.Core.Services;
using R3;
using R3.Triggers;
using UnityEngine;

namespace Game.Contents.Player
{
    /// <summary>
    /// プレイヤー生成地点
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        // 一旦はここで状態管理も行う事とする
        public SDUnityChanPlayerController PlayerController { get; private set; }
        public PlayerHUD PlayerHUD { get; private set; }

        public async UniTask<GameObject> LoadPlayerAsync(PlayerMaster playerMaster)
        {
            var player = await AssetService.InstantiateAsync(playerMaster.AssetName, transform);
            if (player.TryGetComponent<SDUnityChanPlayerController>(out var playerController))
            {
                PlayerController = playerController;
                PlayerController.Initialize(playerMaster);
            }

            var playerHUD = await AssetService.InstantiateAsync("PlayerHUD", transform);
            if (playerHUD.TryGetComponent<PlayerHUD>(out var hud))
            {
                PlayerHUD = hud;
                PlayerHUD.Initialize(playerMaster);
            }

            PlayerController
                .UpdateAsObservable()
                .DistinctUntilChangedBy(_ => playerController.IsRunning())
                .Subscribe(_ => { PlayerHUD.SetRunInput(playerController.IsRunning()); })
                .AddTo(this);

            PlayerHUD.CurrentStamina
                .DistinctUntilChanged()
                .Subscribe(stamina => { PlayerController.SetRunInput(stamina > 0f); })
                .AddTo(this);

            GlobalMessageBroker.GetPublisher<int, GameObject>().Publish(MessageKey.Player.SpawnPlayer, player);

            return player;
        }
    }
}