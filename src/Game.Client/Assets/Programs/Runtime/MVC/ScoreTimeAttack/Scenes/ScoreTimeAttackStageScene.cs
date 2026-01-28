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
using Game.Library.Shared.MasterData;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackStageScene : GamePrefabScene<ScoreTimeAttackStageScene, ScoreTimeAttackStageSceneComponent>, IGameSceneArg<int>, IPlayerCollisionHandler
    {
        protected override string AssetPathOrAddress => "ScoreTimeAttackStageScene";

        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private GameSceneService _sceneService;
        private GameSceneService SceneService => _sceneService ??= GameServiceManager.Get<GameSceneService>();

        private MasterDataService _masterDataService;
        private MasterDataService MasterDataService => _masterDataService ??= GameServiceManager.Get<MasterDataService>();
        private MemoryDatabase MemoryDatabase => MasterDataService.MemoryDatabase;

        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        public ScoreTimeAttackStageSceneModel SceneModel { get; set; }

        private int _stageId;
        private SceneInstance _stageSceneInstance;

        public UniTask ArgHandle(int stageId)
        {
            _stageId = stageId;
            return UniTask.CompletedTask;
        }

        public override UniTask PreInitialize()
        {
            SceneModel = new ScoreTimeAttackStageSceneModel();
            SceneModel.Initialize(_stageId);
            return base.PreInitialize();
        }

        public override async UniTask LoadAsset()
        {
            await base.LoadAsset();

            // 追加でStageMasterに対応したUnityシーン(3Dフィールド)をロードする
            _stageSceneInstance = await AssetService.LoadSceneAsync(SceneModel.StageMaster.AssetName);

            // ステージアセットに設定されたSkyboxをメインカメラに反映
            var skybox = ScoreTimeAttackStageSceneHelper.GetSkybox(_stageSceneInstance.Scene);
            if (skybox)
            {
                MessagePipeService.Publish(MessageKey.System.Skybox, skybox.material);
            }

            MessagePipeService.Publish(MessageKey.System.DirectionalLight, false);
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
            var audioTask = AudioService.PlayRandomOneAsync(AudioPlayTag.StageReady);
            //カウントダウンしてスタート
            await GameCountdownUIDialog.RunAsync();
            MessagePipeService.Publish(MessageKey.InputSystem.Escape, true);
            MessagePipeService.Publish(MessageKey.InputSystem.ScrollWheel, true);
            ApplicationEvents.ResumeTime();
            ApplicationEvents.HideCursor();
            SceneModel.StageState = GameStageState.Start;
            SceneComponent.DoFadeIn();
            MessagePipeService.Publish(MessageKey.Player.HudFadeIn);
            await audioTask;
            await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageStart);
            await base.Ready();
        }

        public override async UniTask Terminate()
        {
            MessagePipeService.Publish(MessageKey.System.DirectionalLight, true);
            MessagePipeService.Publish(MessageKey.System.DefaultSkybox);
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
            AudioService.StopBgmAsync().Forget();
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
                .AddTo(SceneComponent);

            MessagePipeService.SubscribeAsync<bool>(MessageKey.UI.Escape, async (_, token) => { await ShowPauseAsync(token); })
                .AddTo(SceneComponent);
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
            var itemMaster = MemoryDatabase.ScoreTimeAttackStageItemMasterTable.FindClosestByAssetName(other.name);
            var point = itemMaster?.Point ?? 1;

            other.gameObject.SafeDestroy();

            AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.PlayerGetPoint).Forget();
            AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetPoint).Forget();

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

            AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();

            SceneModel.PlayerHpDamaged(hpDamage);

            MessagePipeService.Publish(MessageKey.Player.HpChanged, SceneModel.PlayerCurrentHp);

            TryShowResultAsync().Forget();
        }

        #endregion

        private async UniTask ShowPauseAsync(CancellationToken token = default)
        {
            if (!SceneModel.CanPause()) return;

            AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StagePause, token).Forget();

            // 一時停止メニュー
            var result = await GamePauseUIDialog.RunAsync();
            switch (result)
            {
                case PauseDialogResult.Resume:
                {
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageResume, token).Forget();
                    break;
                }
                case PauseDialogResult.Retry:
                {
                    SceneModel.StageState = GameStageState.Retry;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageRetry, token).Forget();
                    // 現在のステージへ再遷移
                    await SceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(_stageId);
                    break;
                }
                case PauseDialogResult.ReturnToTitle:
                {
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    // 現在のシーンを終了させてタイトルに戻る
                    await SceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
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
            MessagePipeService.Publish(MessageKey.Player.HudFadeOut);

            var stageResult = SceneModel.CreateStageResult();

            var result = await GameResultUIDialog.RunAsync(stageResult);
            switch (result)
            {
                case ResultDialogResult.NextStage:
                {
                    if (!SceneModel.NextStageId.HasValue) return;
                    await SceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(SceneModel.NextStageId.Value);
                    break;
                }
                case ResultDialogResult.Finish:
                {
                    SceneModel.StageState = GameStageState.Finish;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFinish).Forget();
                    await SceneService.TransitionAsync<ScoreTimeAttackTotalResultScene>();
                    break;
                }
                case ResultDialogResult.ReturnToTitle:
                {
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle);
                    ApplicationEvents.ResumeTime();
                    // 現在のシーンを終了させてタイトルに戻る
                    await SceneService.TransitionAsync<ScoreTimeAttackTitleScene>();
                    break;
                }
            }
        }
    }
}