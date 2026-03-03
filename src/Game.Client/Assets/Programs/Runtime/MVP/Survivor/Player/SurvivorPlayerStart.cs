using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Shared.Survivor;
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
        /// <param name="resolver"></param>
        /// <param name="playerMaster">プレイヤー基本情報（AssetName取得用）</param>
        /// <param name="levelMaster">レベル依存ステータス（初期化用）</param>
        public async UniTask<SurvivorPlayerController> LoadPlayerAsync(IObjectResolver resolver, SurvivorPlayerMaster playerMaster, SurvivorPlayerLevelMaster levelMaster)
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

            resolver.Inject(playerController);

            _spawnedPlayer = playerController;

            // プレイヤー初期化（VContainerからのInjectは親スコープから行われる）
            playerController.Initialize(levelMaster);

#if !UNITY_SERVER
            // Presenter初期化（ビジュアル駆動）— サーバーでは不要
            var playerPresenter = playerObj.AddComponent<SurvivorPlayerPresenter>();
            var diedSub = resolver.Resolve<ISubscriber<SurvivorSignals.Player.Died>>();
            playerPresenter.Initialize(playerController, diedSub);
#endif

            Debug.Log($"[SurvivorPlayerStart] Player spawned: {playerMaster.Name} at {transform.position}");

            return playerController;
        }
    }
}