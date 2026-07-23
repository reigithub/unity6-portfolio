using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.ScoreTimeAttack.Enemy;
using Game.ScoreTimeAttack.Player;
using Game.ScoreTimeAttack.UI;
using Game.ScoreTimeAttack.Enums;
using Game.Shared.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackStageScene : GamePrefabScene<ScoreTimeAttackStageScene, ScoreTimeAttackStageSceneComponent>, IGameSceneArg<int>, IPlayerCollisionHandler
    {
        protected override string AssetPathOrAddress => "ScoreTimeAttackStageScene";

        private IAddressableAssetService _assetService;
        private IAudioService _audioService;
        private IGameSceneService _sceneService;
        private IMasterDataService _masterDataService;
        private IInputSystemService _inputService;
        private IMessagePipeService _messagePipeService;

        public ScoreTimeAttackStageSceneModel SceneModel { get; set; }

        private int _stageId;
        private SceneInstance _stageSceneInstance;

        public UniTask SetArg(int stageId)
        {
            _stageId = stageId;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            _assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _audioService = GameServiceManager.Resolve<IAudioService>();
            _sceneService = GameServiceManager.Resolve<IGameSceneService>();
            _masterDataService = GameServiceManager.Resolve<IMasterDataService>();
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

            SceneModel = new ScoreTimeAttackStageSceneModel();
            SceneModel.Initialize(_stageId);
            return base.PreInitialize();
        }

        public override async UniTask LoadAsset()
        {
            await base.LoadAsset();

            Physics.simulationMode = SimulationMode.FixedUpdate;
            // 追加でStageMasterに対応したUnityシーン(3Dフィールド)をロードする
            _stageSceneInstance = await _assetService.LoadSceneAsync(SceneModel.StageMaster.AssetName);
        }

        public override async UniTask Startup()
        {
            RegisterEvents();

            // プレイヤー爆誕の儀（衝突ハンドラーとしてthisを渡す）
            var playerStart = ScoreTimeAttackStageSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            var player = await playerStart.LoadPlayerAsync(SceneModel.PlayerMaster, this);

            // エネミー生成
            var enemyStarts = ScoreTimeAttackStageSceneHelper.GetEnemyStarts(_stageSceneInstance.Scene);
            foreach (var enemyStart in enemyStarts)
            {
                await enemyStart.LoadEnemyAsync(player, _stageId);
            }

            // ステージアイテム生成
            var stageItemStarts = ScoreTimeAttackStageSceneHelper.GetStageItemStarts(_stageSceneInstance.Scene);
            foreach (var stageItemStart in stageItemStarts)
            {
                await stageItemStart.LoadStageItemAsync(_stageId);
            }

            SceneComponent.Initialize(SceneModel);

            await base.Startup();
        }

        public override async UniTask Ready()
        {
            // ゲーム開始準備OKの合図
            SceneModel.StageState = GameStageState.Ready;
            ApplicationEvents.PauseTime();
            ApplicationEvents.ShowCursor();
            var audioTask = _audioService.PlayRandomOneAsync(AudioPlayTag.StageReady);
            //カウントダウンしてスタート
            await GameCountdownUIDialog.RunAsync();
            _inputService.Player.Menu.Enable();
            _inputService.UI.ScrollWheel.Enable();
            ApplicationEvents.ResumeTime();
            ApplicationEvents.HideCursor();
            SceneModel.StageState = GameStageState.Start;
            SceneComponent.DoFadeIn();
            _messagePipeService.Publish(MessageKey.Player.HudFadeIn);
            await audioTask;
            await _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageStart);
            await base.Ready();
        }

        public override async UniTask Terminate()
        {
            await _assetService.UnloadSceneAsync(_stageSceneInstance);
            _audioService.StopBgmAsync().Forget();
            await base.Terminate();
        }

        private void RegisterEvents()
        {
            // 制限時間カウントダウン
            SceneComponent
                .UpdateAsObservable()
                .Where(_ => SceneModel.StageState == GameStageState.Start)
                .ThrottleFirst(TimeSpan.FromSeconds(1f))
                .Subscribe(_ =>
                {
                    SceneModel.ProgressTime();
                    TryShowResultAsync().Forget();
                })
                .AddTo(Disposables);

            // UIキー入力設定
            SceneComponent
                .UpdateAsObservable()
                .Where(_ => State.IsProcessing())
                .Subscribe(_ =>
                {
                    if (_inputService.Player.Menu.WasPressedThisFrame())
                    {
                        ShowPauseAsync().Forget();
                        return;
                    }

                    if (_inputService.UI.ScrollWheel.WasPressedThisFrame())
                    {
                        var scrollWheel = _inputService.UI.ScrollWheel.ReadValue<Vector2>().normalized;
                        _messagePipeService.Publish(MessageKey.UI.ScrollWheel, scrollWheel);
                    }
                })
                .AddTo(Disposables);
        }

        #region IPlayerCollisionHandler Implementation

        /// <summary>
        /// プレイヤーがトリガーに入った時の処理（アイテム取得）
        /// </summary>
        public void HandlePlayerTriggerEnter(Collider other)
        {
            if (!other.gameObject.CompareTag("Item"))
                return;

            // 今はとりあえず一番近いやつでOK
            var itemMaster = _masterDataService.MemoryDatabase.ScoreTimeAttackStageItemMasterTable.FindClosestByAssetName(other.name);
            var point = itemMaster?.Point ?? 1;

            other.gameObject.SafeDestroy();

            _audioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.PlayerGetPoint).Forget();
            _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetPoint).Forget();

            SceneModel.AddPoint(point);

            TryShowResultAsync().Forget();
        }

        /// <summary>
        /// プレイヤーが衝突した時の処理（敵との衝突ダメージ）
        /// </summary>
        public void HandlePlayerCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag("Enemy"))
                return;

            if (!collision.gameObject.TryGetComponent<ScoreTimeAttackEnemyController>(out var enemyController))
                return;

            var hpDamage = enemyController.EnemyMaster.HpAttack;

            collision.gameObject.SafeDestroy();

            _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();

            SceneModel.PlayerHpDamaged(hpDamage);

            _messagePipeService.Publish(MessageKey.Player.HpChanged, SceneModel.PlayerCurrentHp);

            TryShowResultAsync().Forget();
        }

        #endregion

        private async UniTask ShowPauseAsync(CancellationToken token = default)
        {
            if (!SceneModel.CanPause()) return;

            _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StagePause, token).Forget();

            // 一時停止メニュー
            var result = await GamePauseUIDialog.RunAsync();
            switch (result)
            {
                case PauseDialogResult.Resume:
                {
                    _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageResume, token).Forget();
                    break;
                }
                case PauseDialogResult.Retry:
                {
                    SceneModel.StageState = GameStageState.Retry;
                    _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageRetry, token).Forget();
                    // 現在のステージへ再遷移
                    await _sceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(_stageId);
                    break;
                }
                case PauseDialogResult.ReturnToTitle:
                {
                    await _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    // 現在のシーンを終了させてタイトルに戻る
                    await _sceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
                    break;
                }
                case PauseDialogResult.Quit:
                {
                    ApplicationEvents.RequestShutdown();
                    break;
                }
            }
        }

        private async UniTask TryShowResultAsync()
        {
            if (!SceneModel.HasStageResult())
                return;

            SceneModel.StageState = GameStageState.Result;
            SceneComponent.DoFadeOut();
            _messagePipeService.Publish(MessageKey.Player.HudFadeOut);

            var stageResult = SceneModel.CreateStageResult();

            var result = await GameResultUIDialog.RunAsync(stageResult);
            switch (result)
            {
                case ResultDialogResult.NextStage:
                {
                    if (!SceneModel.NextStageId.HasValue) return;
                    await _sceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(SceneModel.NextStageId.Value);
                    break;
                }
                case ResultDialogResult.Finish:
                {
                    SceneModel.StageState = GameStageState.Finish;
                    _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFinish).Forget();
                    await _sceneService.TransitionAsync<ScoreTimeAttackTotalResultScene>();
                    break;
                }
                case ResultDialogResult.ReturnToTitle:
                {
                    await _audioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle);
                    ApplicationEvents.ResumeTime();
                    // 現在のシーンを終了させてタイトルに戻る
                    await _sceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
                    break;
                }
            }
        }
    }
}
