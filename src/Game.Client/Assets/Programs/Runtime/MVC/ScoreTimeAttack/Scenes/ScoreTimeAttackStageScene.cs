using System;
using Cysharp.Threading.Tasks;
using Game.ScoreTimeAttack.Enemy;
using Game.Contents.UI;
using Game.ScoreTimeAttack.Enums;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Library.Shared.MasterData;
using Game.MVC.Core.Scenes;
using Game.MVC.ScoreTimeAttack.Scenes;
using Game.MVC.UI;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackStageScene : GamePrefabScene<ScoreTimeAttackStageScene, ScoreTimeAttackStageSceneComponent>, IGameSceneArg<int>
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
        }

        public override async UniTask Startup()
        {
            RegisterEvents();

            // プレイヤー爆誕の儀
            var playerStart = ScoreTimeAttackStageSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            var player = await playerStart.LoadPlayerAsync(SceneModel.PlayerMaster);

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
            await MessagePipeService.PublishAsync(MessageKey.System.TimeScale, false);
            await MessagePipeService.PublishAsync(MessageKey.System.Cursor, true);
            var audioTask = AudioService.PlayRandomOneAsync(AudioPlayTag.StageReady);
            //カウントダウンしてスタート
            await GameCountdownUIDialog.RunAsync();
            await audioTask;
            await MessagePipeService.PublishAsync(MessageKey.System.TimeScale, true);
            await MessagePipeService.PublishAsync(MessageKey.System.Cursor, false);
            MessagePipeService.Publish(MessageKey.InputSystem.Escape, true);
            MessagePipeService.Publish(MessageKey.InputSystem.ScrollWheel, true);
            SceneModel.StageState = GameStageState.Start;
            SceneComponent.DoFadeIn();
            MessagePipeService.Publish(MessageKey.Player.HudFadeIn);
            await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageStart);
            await base.Ready();
        }

        public override async UniTask Terminate()
        {
            MessagePipeService.Publish(MessageKey.System.DefaultSkybox);
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
                    TryShowResultAsync().Forget();
                })
                .AddTo(SceneComponent);

            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameStage.Pause, async (_, token) =>
                {
                    if (!SceneModel.CanPause()) return;

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StagePause, token).Forget();

                    // 一時停止メニュー
                    await GamePauseUIDialog.RunAsync();
                })
                .AddTo(SceneComponent);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameStage.Resume, async (_, token) =>
                {
                    if (!SceneModel.CanPause()) return;

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageResume, token).Forget();

                    await SceneService.TerminateAsync(typeof(GamePauseUIDialog));
                })
                .AddTo(SceneComponent);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameStage.Retry, async (_, token) =>
                {
                    SceneModel.StageState = GameStageState.Retry;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageRetry, token).Forget();
                    // 現在のステージへ再遷移
                    await SceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(_stageId);
                })
                .AddTo(SceneComponent);
            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameStage.ReturnTitle, async (_, token) =>
                {
                    await AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageReturnTitle, token);
                    // 現在のシーンを終了させてタイトルに戻る
                    await SceneService.TransitionAsync<GameTitleScene>();
                })
                .AddTo(SceneComponent);

            MessagePipeService.SubscribeAsync<bool>(MessageKey.GameStage.Finish, async (_, token) =>
                {
                    SceneModel.StageState = GameStageState.Finish;
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.StageFinish, token).Forget();

                    TryShowResultAsync().Forget();

                    if (SceneModel.NextStageId.HasValue && SceneModel.IsClear())
                    {
                        // 次のステージへ
                        await SceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(SceneModel.NextStageId.Value);
                        return;
                    }

                    // 総合リザルトへ
                    await TryShowResultAsync();
                })
                .AddTo(SceneComponent);


            // プレイヤー設定
            MessagePipeService.Subscribe<Collider>(MessageKey.Player.OnTriggerEnter, other =>
                {
                    if (!other.gameObject.CompareTag("StageItem"))
                        return;

                    // 今はとりあえず一番近いやつでOK
                    var itemMaster = MemoryDatabase.ScoreTimeAttackStageItemMasterTable.FindClosestByAssetName(other.name);
                    var point = itemMaster?.Point ?? 1;

                    other.gameObject.SafeDestroy();

                    AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.PlayerGetPoint).Forget();
                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetPoint).Forget();

                    SceneModel.AddPoint(point);

                    TryShowResultAsync().Forget();
                })
                .AddTo(SceneComponent);
            MessagePipeService.Subscribe<Collision>(MessageKey.Player.OnCollisionEnter, other =>
                {
                    if (!other.gameObject.CompareTag("Enemy"))
                        return;

                    if (!other.gameObject.TryGetComponent<ScoreTimeAttackEnemyController>(out var enemyController))
                        return;

                    var hpDamage = enemyController.EnemyMaster.HpAttack;

                    other.gameObject.SafeDestroy();

                    AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();

                    SceneModel.PlayerHpDamaged(hpDamage);

                    MessagePipeService.Publish(MessageKey.Player.HpChanged, SceneModel.PlayerCurrentHp);

                    TryShowResultAsync().Forget();
                })
                .AddTo(SceneComponent);
        }

        private async UniTask TryShowResultAsync()
        {
            if (!SceneModel.HasStageResult())
                return;

            SceneModel.StageState = GameStageState.Result;
            SceneComponent.DoFadeOut();
            MessagePipeService.Publish(MessageKey.Player.HudFadeOut);

            var result = SceneModel.CreateStageResult();

            if (SceneModel.NextStageId.HasValue)
            {
                await GameResultUIDialog.RunAsync(result);
                return;
            }

            // 総合リザルトへ
            await SceneService.TransitionAsync<ScoreTimeAttackTotalResultScene>();
        }
    }
}