using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes.ViewModels;
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
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly ISurvivorSaveService _saveService;

        private readonly StageSelectSceneViewModel _viewModel = new();

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

            SceneComponent.OnOptionsClicked
                .Subscribe(_ => OnOptions().Forget())
                .AddTo(Disposables);

            SceneComponent.OnRankingClicked
                .Subscribe(_ => OnRanking().Forget())
                .AddTo(Disposables);

            // 中断セッションがあれば通知
            // if (_saveService.HasActiveSession)
            //     SceneComponent.ShowResumeOption(_saveService.CurrentSession);
        }

        public override async UniTask Ready()
        {
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
        }

        private List<StageSelectItemData> BuildStageItems()
        {
            var stages = _masterDataService.MemoryDatabase.SurvivorStageMasterTable.All;
            var saveData = _saveService.Data;
            return _viewModel.BuildStageItems(stages, saveData);
        }

        private async UniTaskVoid OnStageSelected(int stageId)
        {
            // TODO(アンロック機構の再設計): アンロック状態はサーバー (PostgreSQL) で管理すべきだが、
            // 現状ローカルセーブデータが源泉になっており不正操作で状態が壊れる構造的不具合がある。
            // StageSelectSceneViewModel.IsUnlocked=true と同じ理由で遷移ガードも一時無効化する。

            SceneComponent.SetInteractables(false);

            // 新規セッション開始
            var playerId = _saveService.Data.SelectedPlayerId;
            _saveService.StartSession(stageId, playerId);
            await _saveService.SaveIfDirtyAsync();

            await _sceneService.TransitionAsync<SurvivorStageConnectScene>();
        }

        private async UniTaskVoid OnBack()
        {
            SceneComponent.SetInteractables(false);
            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }

        private async UniTaskVoid OnOptions()
        {
            SceneComponent.SetInteractables(false);
            await SurvivorOptionsDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }

        private async UniTaskVoid OnRanking()
        {
            SceneComponent.SetInteractables(false);
            await SurvivorRankingDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
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
        public bool HasBestClearTime => Record?.HasBestClearTime ?? false;
        public int ClearCount => Record?.ClearCount ?? 0;
    }
}
