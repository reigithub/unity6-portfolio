using System;
using System.Threading.Tasks;
using Game.Contents.Enemy;
using Game.Contents.UI;
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
    public class GameStageScene : GamePrefabScene<GameStageScene, GameStageSceneComponent>, IGameSceneModel<GameStageSceneModel>, IGameSceneArg<int>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/GameStageScene.prefab";

        public GameStageSceneModel SceneModel { get; set; }

        private int _stageId;
        private SceneInstance _stageSceneInstance;

        public Task PreInitialize(int stageId)
        {
            _stageId = stageId;
            var stageMaster = MemoryDatabase.StageMasterTable.FindById(_stageId);
            SceneModel.Initialize(stageMaster);
            return Task.CompletedTask;
        }

        public override async Task<GameObject> LoadAsset()
        {
            var instance = await base.LoadAsset();
            _stageSceneInstance = await AssetService.LoadSceneAsync(SceneModel.StageMaster.AssetName);
            return instance;
        }

        public override async Task Startup()
        {
            RegisterEvents();

            // プレイヤー爆誕の儀
            var playerStart = GameSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            var player = await playerStart.LoadPlayerAsync();

            // エネミー生成
            var enemyStart = GameSceneHelper.GetEnemyStart(_stageSceneInstance.Scene);
            await enemyStart.LoadEnemyAsync(player, _stageId);

            // Memo: ビューがモデルの変更を検知する方法については賛否あると思われるが一旦は愚直に渡す
            // (ReactivePropertyとか疎結合化は後ほど検討する)
            await SceneComponent.Initialize(SceneModel);
            await base.Startup();
        }

        public override Task Ready()
        {
            // ゲーム開始準備OKの合図
            SceneComponent.DoFadeInView();
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.Ready, true);
            return base.Ready();
        }

        public override async Task Terminate()
        {
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
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
                    SceneComponent.UpdateLimitTime();
                    if (SceneModel.IsFailed())
                    {
                        GlobalMessageBroker.GetAsyncPublisher<int, GameStageResult>().Publish(MessageKey.GameStage.Result, GameStageResult.Failed);
                    }
                })
                .AddTo(SceneComponent);

            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Ready, handler: async (_, _) =>
                {
                    SceneModel.StageState = GameStageState.Ready;
                    //カウントダウンしてスタート
                    await GameCountdownUIDialog.RunAsync();
                    GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.GameStage.Start, true);
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Start, handler: _ =>
                {
                    SceneModel.StageState = GameStageState.Start;
                    // UI操作可能になるタイミング
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Pause, handler: async (_, _) =>
                {
                    if (!SceneModel.CanPause()) return;
                    // 一時停止メニュー
                    await GamePauseUIDialog.RunAsync();
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Resume, handler: _ =>
                {
                    // 一時停止メニューを閉じる
                    SceneService.TerminateAsync<GamePauseUIDialog>().Forget();
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Retry, handler: async (_, _) =>
                {
                    SceneModel.StageState = GameStageState.Retry;
                    // 現在のステージへ再遷移
                    await SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, int>(_stageId);
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.ReturnTitle, handler: async (_, _) =>
                {
                    // 現在のシーンを終了させてタイトルに戻る
                    await SceneService.TransitionAsync<GameTitleScene>();
                })
                .AddTo(SceneComponent);

            GlobalMessageBroker.GetAsyncSubscriber<int, GameStageResult>()
                .Subscribe(MessageKey.GameStage.Result, handler: async (result, _) =>
                {
                    // リザルト画面
                    SceneModel.StageState = GameStageState.Result;
                    SceneModel.StageResult = result;
                    SceneComponent.DoFadeOutView();
                    // Debug.LogError($"Stage Result: {result}");
                    await GameResultUIDialog.RunAsync(SceneModel.CreateStageResult());
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetAsyncSubscriber<int, int?>()
                .Subscribe(MessageKey.GameStage.Finish, handler: async (nextStageId, _) =>
                {
                    SceneModel.StageState = GameStageState.Finish;
                    if (nextStageId.HasValue)
                    {
                        // 次のステージへ
                        await SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, int>(nextStageId.Value);
                    }
                    else
                    {
                        // 総合リザルト画面？？？
                        // 今はタイトルに戻しておく
                        await SceneService.TransitionAsync<GameTitleScene>();
                    }
                })
                .AddTo(SceneComponent);


            // プレイヤー設定
            // Memo: MessageBrokerからMessageBroker呼ぶのもアレなので、リファクタリングを検討
            GlobalMessageBroker.GetSubscriber<int, Collider>()
                .Subscribe(MessageKey.Player.OnTriggerEnter, handler: other =>
                {
                    if (!other.gameObject.name.Contains("PickUp"))
                        return;

                    other.gameObject.SafeDestroy();

                    // Memo: オブジェクトに応じてポイントを変更できるマスタを用意（StageItemMaster）
                    SceneModel.AddPoint(1);
                    SceneComponent.UpdateView();
                    if (SceneModel.IsClear())
                    {
                        GlobalMessageBroker.GetAsyncPublisher<int, GameStageResult>().Publish(MessageKey.GameStage.Result, GameStageResult.Clear);
                    }
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, Collision>()
                .Subscribe(MessageKey.Player.OnCollisionEnter, handler: other =>
                {
                    // if (!other.gameObject.CompareTag("Enemy"))
                    //     return;
                    if (!other.gameObject.transform.parent.TryGetComponent<EnemyController>(out var enemyController))
                        return;

                    var hpDamage = enemyController.EnemyMaster.HpAttack;

                    other.gameObject.SafeDestroy();

                    // Memo: エネミーに応じてダメージを変更できるマスタを用意（EnemyMaster）
                    SceneModel.PlayerHpDamaged(hpDamage);
                    SceneComponent.UpdateView();
                    if (SceneModel.IsFailed())
                    {
                        GlobalMessageBroker.GetAsyncPublisher<int, GameStageResult>().Publish(MessageKey.GameStage.Result, GameStageResult.Failed);
                    }
                })
                .AddTo(SceneComponent);
        }
    }
}