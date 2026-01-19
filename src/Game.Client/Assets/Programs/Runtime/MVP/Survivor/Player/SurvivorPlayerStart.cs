using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Signals;
using Game.Shared.Services;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤー生成地点
    /// ステージシーンに配置され、プレイヤーアセットを動的に生成する
    /// </summary>
    public class SurvivorPlayerStart : MonoBehaviour
    {
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;

        private SurvivorPlayerController _spawnedPlayer;

        /// <summary>
        /// スポーン済みプレイヤーコントローラー
        /// </summary>
        public SurvivorPlayerController SpawnedPlayer => _spawnedPlayer;

        /// <summary>
        /// プレイヤーを生成し初期化する
        /// </summary>
        public async UniTask<SurvivorPlayerController> LoadPlayerAsync(SurvivorPlayerMaster playerMaster)
        {
            if (_addressableService == null)
            {
                Debug.LogError("[SurvivorPlayerStart] AddressableService is not injected!");
                return null;
            }

            // プレイヤーアセット生成
            var playerObj = await _addressableService.InstantiateAsync(playerMaster.AssetName, transform);
            if (playerObj == null)
            {
                Debug.LogError($"[SurvivorPlayerStart] Failed to instantiate player: {playerMaster.AssetName}");
                return null;
            }

            // SurvivorPlayerControllerを取得
            if (!playerObj.TryGetComponent<SurvivorPlayerController>(out var playerController))
            {
                Debug.LogError($"[SurvivorPlayerStart] Player prefab does not have SurvivorPlayerController: {playerMaster.AssetName}");
                return null;
            }

            _spawnedPlayer = playerController;

            // プレイヤー初期化（VContainerからのInjectは親スコープから行われる）
            playerController.Initialize(playerMaster);

            Debug.Log($"[SurvivorPlayerStart] Player spawned: {playerMaster.Name} at {transform.position}");

            return playerController;
        }
    }
}
