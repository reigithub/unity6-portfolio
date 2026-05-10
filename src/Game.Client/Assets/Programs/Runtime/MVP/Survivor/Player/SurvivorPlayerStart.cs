using Cysharp.Threading.Tasks;
using Fusion;
using Game.Client.MasterData;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤー生成地点
    /// ステージシーンに配置され、Fusion Spawn 済みの SurvivorFusionPlayer GO を
    /// シーン階層に配置し、SurvivorPlayerController の初期化を委譲する
    /// </summary>
    public class SurvivorPlayerStart : MonoBehaviour
    {
        [Inject] private readonly IFusionRunnerService _runnerService;

        /// <summary>
        /// Fusion Spawn 済みの SurvivorFusionPlayer GO を取得してプレイヤーコントローラーを初期化する。
        /// Visual の非同期初期化も含め、SurvivorPlayerController に委譲する。
        /// </summary>
        /// <param name="resolver">VContainer リゾルバー（Presenter への Inject 用）</param>
        /// <param name="playerMaster">プレイヤーマスターデータ</param>
        /// <param name="levelMaster">プレイヤーレベルマスターデータ</param>
        /// <param name="sceneComponentRoot">親 Transform（null の場合は再配置しない）</param>
        /// <param name="targetPlayer">対象プレイヤー（null の場合はローカルプレイヤーを取得）</param>
        /// <returns>初期化済み SurvivorPlayerController、取得失敗時は null</returns>
        public async UniTask<SurvivorPlayerController> LoadPlayerAsync(
            IObjectResolver resolver,
            SurvivorPlayerMaster playerMaster,
            SurvivorPlayerLevelMaster levelMaster,
            Transform sceneComponentRoot = null,
            PlayerRef? targetPlayer = null)
        {
            // 1. Fusion Spawn 済みの SurvivorFusionPlayer を取得
            //    サーバー側: 直前の SpawnConnectedPlayers で即座に利用可能
            //    クライアント側: サーバーからのレプリケーション完了を待機
            SurvivorFusionPlayer fusionPlayer = null;

            // 症状 1 診断 (観察期間限定): LoadPlayerAsync の所要時間を内訳で計測する。
            // wait=Server レプリケーション待ち / parent=SetParent / init=Initialize 同期処理 / visual=InitializeVisualAsync
            // 症状 1 真因確定後の次 PR で削除すること。
            var swWait = System.Diagnostics.Stopwatch.StartNew();
            if (targetPlayer.HasValue)
            {
                // 特定プレイヤー指定: TryGetPlayerComponent で直接取得
                if (!_runnerService.TryGetPlayerComponent(targetPlayer.Value, out fusionPlayer))
                {
                    await UniTask.WaitUntil(
                        () => _runnerService.TryGetPlayerComponent(targetPlayer.Value, out fusionPlayer),
                        cancellationToken: destroyCancellationToken);
                }
            }
            else
            {
                // 未指定: クライアント側のローカルプレイヤー取得（既存動作維持）
                if (!_runnerService.TryGetLocalPlayerComponent(out fusionPlayer))
                {
                    await UniTask.WaitUntil(
                        () => _runnerService.TryGetLocalPlayerComponent(out fusionPlayer),
                        cancellationToken: destroyCancellationToken);
                }
            }
            swWait.Stop();

            if (fusionPlayer == null)
            {
                Debug.LogError("[SurvivorPlayerStart] SurvivorFusionPlayer not found!");
                return null;
            }

            var playerGo = fusionPlayer.gameObject;

            // 2. SceneComponent(SurvivorStageScene(Clone)) の子として配置
            // Fusion のオブジェクトプロバイダは GameRootScene にインスタンス化するため、
            // SceneComponent 配下に移動して他のゲームオブジェクト（アイテム、敵プロキシ等）と
            // 同じ階層・物理シーンに配置する。
            var swParent = System.Diagnostics.Stopwatch.StartNew();
            if (sceneComponentRoot != null)
            {
                playerGo.transform.SetParent(sceneComponentRoot, true);
            }
            swParent.Stop();
            Debug.Log($"[SurvivorPlayerStart] Player parented to {playerGo.transform.parent?.name}, scene={playerGo.scene.name}");

            // 3. プレハブ上の SurvivorPlayerController を取得して初期化
            //    スポーン位置（PlayerStart の Transform）を渡して KCC を設定
            var swInit = System.Diagnostics.Stopwatch.StartNew();
            var playerController = playerGo.GetComponent<SurvivorPlayerController>();
            playerController.Initialize(levelMaster, transform.position);
            swInit.Stop();

            // 4. Visual の非同期初期化（モデルロード・Presenter DI・有効化）
            var swVisual = System.Diagnostics.Stopwatch.StartNew();
            await playerController.InitializeVisualAsync(playerMaster, resolver);
            swVisual.Stop();

            Debug.Log($"[DIAG-LoadDetail] target={targetPlayer}, " +
                      $"wait={swWait.ElapsedMilliseconds}ms, " +
                      $"parent={swParent.ElapsedMilliseconds}ms, " +
                      $"init={swInit.ElapsedMilliseconds}ms, " +
                      $"visual={swVisual.ElapsedMilliseconds}ms");
            Debug.Log($"[SurvivorPlayerStart] Player configured at {playerGo.transform.position}");
            return playerController;
        }
    }
}
