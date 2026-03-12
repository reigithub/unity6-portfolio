using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// MPPM / Dedicated Server 用エントリポイント。
    /// AllPlayersReady シグナル受信後にマスターデータ読み込み → SurvivorNetworkStageScene へ遷移し、
    /// サーバー権威のウェーブ管理・エネミースポーンを開始する。
    /// </summary>
    public class SurvivorServerGameLoop : IAsyncStartable
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly SurvivorUnityServerSession _session;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("[SurvivorServerGameLoop] Waiting for master data and AllPlayersReady...");

            // マスターデータ読み込み（ウェーブ・エネミー定義に必要）
            await _masterDataService.LoadMasterDataAsync();

            // AllPlayersReady シグナルを待機
            var tcs = new UniTaskCompletionSource();
            var subscription = _session.OnAllPlayersReady.Subscribe(_ => tcs.TrySetResult());
            try
            {
                await tcs.Task;
            }
            finally
            {
                subscription.Dispose();
            }

            Debug.Log($"[SurvivorServerGameLoop] AllPlayersReady received, stageId={_session.StageId}");

            // セーブサービスにセッション情報を設定（SurvivorStageScene が参照する）
            _saveService.StartSession(_session.StageId, 1);

            // SurvivorNetworkStageScene へ遷移 → サーバー側ウェーブ管理開始
            await _sceneService.TransitionAsync<SurvivorNetworkStageScene>();

            Debug.Log("[SurvivorServerGameLoop] SurvivorNetworkStageScene loaded on server");
        }
    }
}
