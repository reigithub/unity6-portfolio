using System;
using System.Collections.Generic;
using Game.Client.MasterData;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes.Models
{
    /// <summary>
    /// Survivor ステージのセッション共有状態モデル。
    /// Wave / GameTime / StageMaster / 勝敗結果 / プレイヤー貢献度コレクションを保持する。
    /// SP/MP 問わず、サーバー (SurvivorNetworkStageScene) とクライアント (SurvivorClientStageScene) の両方で使用される。
    /// プレイヤー個別状態 (HP/EXP/Score 等) は <see cref="SurvivorStageModel"/> が担う。
    /// </summary>
    public class SurvivorNetworkStageModel : IDisposable
    {
        private readonly IMasterDataService _masterDataService;
        private SurvivorStageMaster _stageMaster;

        public SurvivorNetworkStageModel() { }

        [Inject]
        public SurvivorNetworkStageModel(IMasterDataService masterDataService)
        {
            _masterDataService = masterDataService;
        }

        // セッション共有プロパティ
        public ReactiveProperty<float> GameTime { get; } = new(0f);
        public ReactiveProperty<int> CurrentWave { get; } = new(1);

        public SurvivorStageMaster StageMaster => _stageMaster;

        /// <summary>制限時間（秒）。0以下は無制限</summary>
        public float TimeLimit => _stageMaster?.TimeLimit ?? 0;

        /// <summary>制限時間に到達したかどうか</summary>
        public bool IsTimeUp => TimeLimit > 0 && GameTime.Value >= TimeLimit;

        // セッション勝敗結果
        private SurvivorNetworkGameResult? _networkResult;
        public bool HasNetworkResult => _networkResult.HasValue;
        public SurvivorNetworkGameResult NetworkResult => _networkResult.Value;

        /// <summary>サーバーから受信したゲーム結果を設定（クライアント側で使用）</summary>
        public void SetNetworkResult(SurvivorNetworkGameResult result) => _networkResult = result;

        // プレイヤー貢献度コレクション（PR1 時点では受け皿のみ、実データ流入は PR3/PR5）
        private readonly List<SurvivorNetworkPlayerResult> _playerContributions = new();
        public IReadOnlyList<SurvivorNetworkPlayerResult> PlayerContributions => _playerContributions;

        /// <summary>
        /// プレイヤー貢献度コレクションを設定する。
        /// リザルト画面で「誰がどの程度貢献したか」を表示するための受け皿。
        /// </summary>
        public void SetPlayerContributions(IReadOnlyList<SurvivorNetworkPlayerResult> contributions)
        {
            _playerContributions.Clear();
            if (contributions != null)
            {
                for (int i = 0; i < contributions.Count; i++)
                {
                    _playerContributions.Add(contributions[i]);
                }
            }
        }

        public void Initialize(int stageId)
        {
            var memoryDb = _masterDataService.MemoryDatabase;
            if (!memoryDb.SurvivorStageMasterTable.TryFindById(stageId, out _stageMaster))
            {
                throw new InvalidOperationException($"Stage master not found: {stageId}");
            }
        }

        public void Dispose()
        {
            GameTime.Dispose();
            CurrentWave.Dispose();
            _playerContributions.Clear();
        }
    }
}
