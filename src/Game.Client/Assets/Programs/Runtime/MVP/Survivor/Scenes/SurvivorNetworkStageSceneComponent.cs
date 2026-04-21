using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Services;
using UnityEngine;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージシーンのサーバー専用コンポーネント。
    /// UI/HUD/武器カード/ResultPanel などのクライアント要素を持たず、
    /// EnemySpawner・ItemSpawner・PlayerController の管理のみを担当する。
    /// </summary>
    public class SurvivorNetworkStageSceneComponent : GameSceneComponent
    {
        [Header("Spawners")]
        [SerializeField] private SurvivorEnemySpawner _enemySpawner;
        [SerializeField] private SurvivorItemSpawner _itemSpawner;

        public SurvivorEnemySpawner EnemySpawner => _enemySpawner;
        public SurvivorItemSpawner SurvivorItemSpawner => _itemSpawner;
        public SurvivorPlayerController PlayerController { get; private set; }

        /// <summary>
        /// 動的生成されたプレイヤーコントローラーを設定する。
        /// PR2 で Dictionary&lt;PlayerRef, SurvivorPlayerController&gt; に拡張予定。
        /// </summary>
        public void SetPlayerController(SurvivorPlayerController playerController)
        {
            PlayerController = playerController;
        }

        /// <summary>
        /// プレイヤーコントローラーを初期化する。サーバーでは <paramref name="mainCamera"/> は null。
        /// </summary>
        public void InitializePlayer(SurvivorPlayerLevelMaster levelMaster, Camera mainCamera)
        {
            if (PlayerController != null && levelMaster != null)
            {
                PlayerController.Initialize(levelMaster);

                if (mainCamera != null)
                {
                    PlayerController.SetMainCamera(mainCamera.transform);
                }
            }
        }

        public async UniTask InitializeEnemySpawnerAsync(SurvivorStageWaveManager waveManager)
        {
            if (_enemySpawner == null) return;

            if (PlayerController != null)
            {
                _enemySpawner.SetPlayer(PlayerController.transform);
            }

            await _enemySpawner.InitializeAsync(waveManager);
        }

        public async UniTask InitializeItemSpawnerAsync()
        {
            if (_itemSpawner != null)
            {
                await _itemSpawner.InitializeAsync();

                if (_enemySpawner != null)
                {
                    _itemSpawner.ConnectToEnemySpawner(_enemySpawner);
                }
            }
        }
    }
}
