using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// オプションダイアログ（Presenter）
    /// セーブデータ管理とオーディオ設定を提供
    /// </summary>
    public class SurvivorOptionsDialog : GameDialogScene<SurvivorOptionsDialog, SurvivorOptionsDialogComponent, Unit>
    {
        protected override string AssetPathOrAddress => "SurvivorOptionsDialog";

        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAudioSaveService _audioSaveService;
        [Inject] private readonly IInputService _inputService;

        public static UniTask RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorOptionsDialog, SurvivorOptionsDialogComponent, Unit>();
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            // ステージ一覧を構築
            var stageItems = BuildStageItems();
            SceneComponent.InitializeStageList(stageItems);

            // オーディオ設定を反映
            var audioData = new OptionsAudioData
            {
                MasterVolume = _audioSaveService.Data.MasterVolume,
                BgmVolume = _audioSaveService.Data.BgmVolume,
                VoiceVolume = _audioSaveService.Data.VoiceVolume,
                SeVolume = _audioSaveService.Data.SeVolume
            };
            SceneComponent.InitializeAudioSettings(audioData);

            // イベント購読
            SceneComponent.OnCloseClicked
                .Subscribe(_ => OnClose().Forget())
                .AddTo(Disposables);

            SceneComponent.OnDeleteStageClicked
                .Subscribe(stageId => OnDeleteStageRecord(stageId).Forget())
                .AddTo(Disposables);

            SceneComponent.OnVolumeChanged
                .Subscribe(x => OnVolumeChanged(x.category, x.value))
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            // Escapeキーで閉じる
            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnClose().Forget();
                })
                .AddTo(Disposables);
        }

        private List<OptionsStageData> BuildStageItems()
        {
            var stages = _masterDataService.MemoryDatabase.SurvivorStageMasterTable.All;
            var saveData = _saveService.Data;

            return stages
                .OrderBy(s => s.Id)
                .Select(stage =>
                {
                    var record = saveData.StageRecords.GetValueOrDefault(stage.Id);
                    return new OptionsStageData
                    {
                        StageId = stage.Id,
                        StageName = stage.Name,
                        HasRecord = record != null,
                        HighScore = record?.HighScore ?? 0,
                        StarRating = record?.StarRating ?? 0
                    };
                })
                .ToList();
        }

        private async UniTaskVoid OnClose()
        {
            SceneComponent.SetInteractables(false);

            // 変更があれば保存
            await _audioSaveService.SaveIfDirtyAsync();

            // ダイアログを閉じる
            TrySetResult(Unit.Default);
        }

        private async UniTaskVoid OnDeleteStageRecord(int stageId)
        {
            // 確認ダイアログを表示
            var stageName = _masterDataService.MemoryDatabase.SurvivorStageMasterTable
                .FindById(stageId)?.Name ?? $"Stage {stageId}";

            var options = new SurvivorConfirmDialogOptions
            {
                Title = "DELETE RECORD",
                Message = $"Delete the record for \"{stageName}\"?\nThis action cannot be undone.",
                ConfirmButtonText = "DELETE",
                CancelButtonText = "CANCEL"
            };

            var confirmed = await SurvivorConfirmDialog.RunAsync(_sceneService, options);

            if (confirmed)
            {
                // 記録を削除
                _saveService.DeleteStageRecord(stageId);
                await _saveService.SaveIfDirtyAsync();

                // UI更新
                SceneComponent.UpdateStageRecord(stageId, false, 0, 0);
            }
        }

        private void OnVolumeChanged(string category, int value)
        {
            switch (category)
            {
                case "master":
                    _audioSaveService.SetMasterVolume(value);
                    break;
                case "bgm":
                    _audioSaveService.SetBgmVolume(value);
                    break;
                case "voice":
                    _audioSaveService.SetVoiceVolume(value);
                    break;
                case "se":
                    _audioSaveService.SetSeVolume(value);
                    break;
            }
        }
    }
}
