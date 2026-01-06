using System;
using System.Threading.Tasks;
using Game.Contents.Enemy;
using Game.Contents.Player;
using Game.Contents.UI;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using MessagePipe;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Contents.Scenes
{
    public class GameStageScene : GamePrefabScene<GameStageScene, GameStageSceneComponent>, IGameSceneArg<int>
    {
        protected override string AssetPathOrAddress => "GameStageScene";

        public GameStageSceneModel SceneModel { get; set; }

        private int _stageId;
        private SceneInstance _stageSceneInstance;

        private PlayerStart _playerStart;

        public Task ArgHandle(int stageId)
        {
            _stageId = stageId;
            return Task.CompletedTask;
        }

        public override Task PreInitialize()
        {
            SceneModel = new GameStageSceneModel();
            SceneModel.Initialize(_stageId);
            return base.PreInitialize();
        }

        public override async Task LoadAsset()
        {
            await base.LoadAsset();

            // 追加でStageMasterに対応したUnityシーン(3Dフィールド)をロードする
            _stageSceneInstance = await AssetService.LoadSceneAsync(SceneModel.StageMaster.AssetName);

            // ステージアセットに設定されたSkyboxをメインカメラに反映
            var skybox = GameSceneHelper.GetSkybox(_stageSceneInstance.Scene);
            if (skybox)
            {
                GlobalMessageBroker.GetPublisher<int, Material>().Publish(MessageKey.System.Skybox, skybox.material);
            }
        }

        public override async Task Startup()
        {
            RegisterEvents();

            // プレイヤー爆誕の儀
            _playerStart = GameSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            var player = await _playerStart.LoadPlayerAsync(SceneModel.PlayerMaster);

            // エネミー生成
            var enemyStarts = GameSceneHelper.GetEnemyStarts(_stageSceneInstance.Scene);
            foreach (var enemyStart in enemyStarts)
            {
                await enemyStart.LoadEnemyAsync(player, _stageId);
            }

            // ステージアイテム生成
            var stageItemStarts = GameSceneHelper.GetStageItemStarts(_stageSceneInstance.Scene);
            foreach (var stageItemStart in stageItemStarts)
            {
                await stageItemStart.LoadStageItemAsync(_stageId);
            }

            await SceneComponent.Initialize(SceneModel);

            await base.Startup();
        }

        public override async Task Ready()
        {
            if (SceneModel.IsFirstStage) GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.GameStageService.Startup, true);

            // ゲーム開始準備OKの合図
            SceneModel.StageState = GameStageState.Ready;
            await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.System.TimeScale, false);
            await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.System.Cursor, true);
            var audioTask = AudioService.PlayRandomOneAsync(AudioPlayTag.StageReady);
            //カウントダウンしてスタート
            await GameCountdownUIDialog.RunAsync();
            await audioTask;
            await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.System.TimeScale, true);
            await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.System.Cursor, false);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.Escape, true);
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.InputSystem.ScrollWheel, true);
            SceneModel.StageState = GameStageState.Start;
            SceneComponent.DoFadeIn();
            _playerStart.PlayerHUD.DoFadeIn();
            await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageStart);
            await base.Ready();
        }

        public override async Task Terminate()
        {
            GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.System.DefaultSkybox, true);
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
            AudioService.StopBgm();
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
                    TryShowResultDialogAsync().Forget();
                })
                .AddTo(SceneComponent);

            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Pause, handler: async (_, token) =>
                {
                    if (!SceneModel.CanPause()) return;

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StagePause, token).Forget();

                    // 一時停止メニュー
                    await GamePauseUIDialog.RunAsync();
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Resume, handler: async (_, token) =>
                {
                    if (!SceneModel.CanPause()) return;

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageResume, token).Forget();

                    await SceneService.TerminateAsync(typeof(GamePauseUIDialog));
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Retry, handler: async (_, token) =>
                {
                    SceneModel.StageState = GameStageState.Retry;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageRetry, token).Forget();
                    // 現在のステージへ再遷移
                    await SceneService.TransitionAsync<GameStageScene, int>(_stageId);
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.ReturnTitle, handler: async (_, token) =>
                {
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    // 現在のシーンを終了させてタイトルに戻る
                    await SceneService.TransitionAsync<GameTitleScene>();
                })
                .AddTo(SceneComponent);

            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Finish, handler: async (_, token) =>
                {
                    SceneModel.StageState = GameStageState.Finish;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFinish, token).Forget();

                    if (SceneModel.NextStageId.HasValue)
                    {
                        // 次のステージへ
                        await SceneService.TransitionAsync<GameStageScene, int>(SceneModel.NextStageId.Value);
                        return;
                    }

                    // 総合リザルトへ
                    await SceneService.TransitionAsync<GameTotalResultScene>();
                })
                .AddTo(SceneComponent);


            // プレイヤー設定
            GlobalMessageBroker.GetSubscriber<int, Collider>()
                .Subscribe(MessageKey.Player.OnTriggerEnter, handler: other =>
                {
                    if (!other.gameObject.CompareTag("StageItem"))
                        return;

                    // 今はとりあえず一番近いやつでOK
                    var itemMaster = MemoryDatabase.StageItemMasterTable.FindClosestByAssetName(other.name);
                    var point = itemMaster?.Point ?? 1;

                    other.gameObject.SafeDestroy();

                    AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.PlayerGetPoint).Forget();
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetPoint).Forget();

                    SceneModel.AddPoint(point);

                    TryShowResultDialogAsync().Forget();
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, Collision>()
                .Subscribe(MessageKey.Player.OnCollisionEnter, handler: other =>
                {
                    if (!other.gameObject.CompareTag("Enemy"))
                        return;

                    if (!other.gameObject.TryGetComponent<EnemyController>(out var enemyController))
                        return;
                    // if (!other.transform.parent.TryGetComponent<EnemyController>(out var enemyController))
                    //     return;

                    var hpDamage = enemyController.EnemyMaster.HpAttack;

                    other.gameObject.SafeDestroy();

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();

                    SceneModel.PlayerHpDamaged(hpDamage);

                    _playerStart.PlayerHUD.CurrentHp.Value = SceneModel.PlayerCurrentHp;

                    TryShowResultDialogAsync().Forget();
                })
                .AddTo(SceneComponent);
        }

        private async Task TryShowResultDialogAsync()
        {
            if (SceneModel.IsClear())
            {
                SceneModel.StageResult = GameStageResult.Clear;
            }

            if (SceneModel.IsFailed())
            {
                SceneModel.StageResult = GameStageResult.Failed;
            }

            if (SceneModel.StageResult is GameStageResult.None)
                return;

            SceneModel.StageState = GameStageState.Result;
            SceneComponent.DoFadeOut();
            _playerStart.PlayerHUD.DoFadeOut();

            if (SceneModel.StageResult is GameStageResult.Clear)
                AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageClear).Forget();
            else
                AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFailed).Forget();

            var result = SceneModel.CreateStageResult();

            await GameResultUIDialog.RunAsync(result);
        }
    }
}