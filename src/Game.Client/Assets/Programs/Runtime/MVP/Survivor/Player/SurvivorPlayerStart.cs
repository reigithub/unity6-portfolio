using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤー生成地点
    /// ステージシーンに配置され、Fusion Spawn 済みの SurvivorFusionPlayer GO に
    /// SurvivorPlayerController と視覚モデルを動的に追加する
    /// </summary>
    public class SurvivorPlayerStart : MonoBehaviour
    {
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;

        /// <summary>
        /// Fusion Spawn 済みの SurvivorFusionPlayer GO にプレイヤーコントローラーと視覚モデルを追加する
        /// </summary>
        /// <param name="resolver">VContainer リゾルバ</param>
        /// <param name="playerMaster">プレイヤー基本情報（AssetName取得用）</param>
        /// <param name="levelMaster">レベル依存ステータス（初期化用）</param>
        public async UniTask<SurvivorPlayerController> LoadPlayerAsync(
            IObjectResolver resolver,
            SurvivorPlayerMaster playerMaster,
            SurvivorPlayerLevelMaster levelMaster)
        {
            // 1. Fusion Spawn 済みの SurvivorFusionPlayer を取得
            //    サーバー側: 直前の SpawnConnectedPlayers で即座に利用可能
            //    クライアント側: サーバーからのレプリケーション完了を待機
            if (!_runnerService.TryGet<SurvivorFusionPlayer>(out var fusionPlayer))
            {
                await UniTask.WaitUntil(
                    () => _runnerService.TryGet(out fusionPlayer),
                    cancellationToken: destroyCancellationToken);
            }

            if (fusionPlayer == null)
            {
                Debug.LogError("[SurvivorPlayerStart] SurvivorFusionPlayer not found!");
                return null;
            }

            var playerGo = fusionPlayer.gameObject;

            // 2. DDOL → ステージシーンに移動（物理シーン整合性のため）
            var stageScene = gameObject.scene;
            SceneManager.MoveGameObjectToScene(playerGo, stageScene);

            // 3. Rigidbody を物理有効化（プレハブでは kinematic + gravity off）
            if (playerGo.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // 4. SurvivorPlayerController を AddComponent + DI 注入
            var playerController = playerGo.AddComponent<SurvivorPlayerController>();
            resolver.Inject(playerController);

            // 5. Controller 初期化
            playerController.Initialize(levelMaster);

            // 6. モデルロード + Presenter 追加（クライアントのみ — サーバーは視覚不要）
            Transform cameraFollowTarget = playerGo.transform;
            if (UnityPlaymodeHelper.IsClient())
            {
                // InterpolationTarget 用の空 GO を作成（コライダーを含めない）
                var interpGo = new GameObject("[InterpolationTarget]");
                interpGo.transform.SetParent(playerGo.transform, false);
                fusionPlayer.SetInterpolationTarget(interpGo.transform);
                cameraFollowTarget = interpGo.transform;

                var modelAssetName = playerMaster.AssetName + "_Model";
                var modelObj = await _addressableService.InstantiateAsync(modelAssetName, interpGo.transform);
                if (modelObj != null)
                {
                    modelObj.transform.localPosition = Vector3.zero;
                    modelObj.transform.localRotation = Quaternion.identity;
                }

                var playerPresenter = playerGo.AddComponent<SurvivorPlayerPresenter>();
                resolver.Inject(playerPresenter);
                playerPresenter.Initialize(playerController);
            }

            // カメラフォロー用シグナル発行
            // クライアント: InterpolationTarget（NRB3D が滑らかに補間する Transform）
            // サーバー: ルート Transform（EnemySpawner 等が参照）
            _spawnedPublisher?.Publish(new SurvivorSignals.Player.Spawned(cameraFollowTarget));

            Debug.Log($"[SurvivorPlayerStart] IsClient={UnityPlaymodeHelper.IsClient()}, cameraTarget={cameraFollowTarget.name}, pos={playerGo.transform.position}");
            return playerController;
        }
    }
}
