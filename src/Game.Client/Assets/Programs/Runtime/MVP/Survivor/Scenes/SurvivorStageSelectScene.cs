using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.SaveData;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージ選択シーン（Presenter）
    /// セーブデータを参照してステージ一覧を表示
    /// </summary>
    public class SurvivorStageSelectScene : GamePrefabScene<SurvivorStageSelectScene, SurvivorStageSelectSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly ISurvivorSaveService _saveService;

        protected override string AssetPathOrAddress => "SurvivorStageSelectScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            // ステージ一覧データを構築
            var stageItems = BuildStageItems();
            SceneComponent.Initialize(stageItems);

            // イベント購読
            SceneComponent.OnStageSelected
                .Subscribe(x => OnStageSelected(x).Forget())
                .AddTo(Disposables);

            SceneComponent.OnBackClicked
                .Subscribe(_ => OnBack().Forget())
                .AddTo(Disposables);

            // 中断セッションがあれば通知
            if (_saveService.HasActiveSession)
            {
                SceneComponent.ShowResumeOption(_saveService.CurrentSession);
            }
        }

        private List<StageSelectItemData> BuildStageItems()
        {
            var stages = _masterDataService.MemoryDatabase.SurvivorStageMasterTable.All;
            var saveData = _saveService.Data;

            return stages
                .OrderBy(s => s.Id)
                .Select(stage => new StageSelectItemData
                {
                    StageId = stage.Id,
                    StageName = stage.Name,
                    Description = stage.Description,
                    Difficulty = stage.Difficulty,
                    TimeLimit = stage.TimeLimit,
                    IsUnlocked = saveData.UnlockedStageIds.Contains(stage.Id),
                    Record = saveData.StageRecords.GetValueOrDefault(stage.Id)
                })
                .ToList();
        }

        private async UniTaskVoid OnStageSelected(int stageId)
        {
            if (!_saveService.IsStageUnlocked(stageId))
            {
                // ロック中のステージは選択不可
                return;
            }

            SceneComponent.SetInteractables(false);

            // 新規セッション開始
            var playerId = _saveService.Data.SelectedPlayerId;
            _saveService.StartSession(stageId, playerId);
            await _saveService.SaveIfDirtyAsync();

            await _sceneService.TransitionAsync<SurvivorStageScene>();
        }

        private async UniTaskVoid OnBack()
        {
            SceneComponent.SetInteractables(false);
            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }
    }

    /// <summary>
    /// ステージ選択アイテムのデータ
    /// </summary>
    public class StageSelectItemData
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public string Description { get; set; }
        public int Difficulty { get; set; }
        public int TimeLimit { get; set; }
        public bool IsUnlocked { get; set; }
        public SurvivorStageClearRecord Record { get; set; }

        public bool IsCleared => Record?.IsCleared ?? false;
        public int StarRating => Record?.StarRating ?? 0;
        public int HighScore => Record?.HighScore ?? 0;
        public float BestClearTime => Record?.BestClearTime ?? 0f;
        public int ClearCount => Record?.ClearCount ?? 0;
    }
}