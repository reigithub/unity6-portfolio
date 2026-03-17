using Cysharp.Threading.Tasks;
using Fusion.Addons.KCC;
using Game.Client.MasterData;
using Game.Shared.Constants;
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

            // 2. 物理シーンに移動
            // Fusion のオブジェクトプロバイダは SetActiveScene に関係なく GameRootScene にインスタンス化するため、
            // 物理シーン（PlayerStart が存在するシーン）への移動が必要
            // サーバー側は既に物理シーンにスポーンされるため no-op
            var physicsScene = gameObject.scene;
            if (playerGo.scene != physicsScene)
            {
                SceneManager.MoveGameObjectToScene(playerGo, physicsScene);
            }
            Debug.Log($"[SurvivorPlayerStart] Player scene={playerGo.scene.name}");
            if (playerGo.TryGetComponent<KCC>(out var kcc))
            {
                kcc.SetPosition(transform.position);
                kcc.Settings.CollisionLayerMask = Physics.DefaultRaycastLayers & ~LayerMaskConstants.Enemy;
                // kcc.Settings.AntiJitterDistance = Vector2.zero;
                // kcc.Settings.ForcePredictedLookRotation = true;
                kcc.Settings.InputAuthorityBehavior = EKCCAuthorityBehavior.PredictFixed_PredictRender;
                kcc.Settings.PredictionCorrectionSpeed = 2f;
                Debug.Log($"[SurvivorPlayerStart] KCC configured in scene={playerGo.scene.name}, pos={transform.position}");
            }
            else
            {
                Debug.LogError("[SurvivorPlayerStart] KCC not found on FusionPlayer!");
            }

            // 4. プレハブ上の SurvivorPlayerController を取得して初期化
            var playerController = playerGo.GetComponent<SurvivorPlayerController>();
            playerController.Initialize(levelMaster);

            // 5. モデルロード + Presenter 追加（クライアントのみ — サーバーは視覚不要）
            if (UnityPlaymodeHelper.IsClient())
            {
                var modelAssetName = playerMaster.AssetName + "_Model";
                var modelObj = await _addressableService.InstantiateAsync(modelAssetName, playerGo.transform);
                if (modelObj != null)
                {
                    modelObj.transform.localPosition = Vector3.zero;
                    modelObj.transform.localRotation = Quaternion.identity;
                }

                var playerPresenter = playerGo.AddComponent<SurvivorPlayerPresenter>();
                resolver.Inject(playerPresenter);
                playerPresenter.Initialize(playerController);
            }

            // カメラフォロー用シグナル発行（KCC が RenderData で滑らかに補間するためルート transform）
            _spawnedPublisher?.Publish(new SurvivorSignals.Player.Spawned(playerGo.transform));

            Debug.Log($"[SurvivorPlayerStart] Player configured at {playerGo.transform.position}");
            return playerController;
        }
    }
}
