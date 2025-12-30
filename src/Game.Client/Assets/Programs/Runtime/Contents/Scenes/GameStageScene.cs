using System.Threading.Tasks;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using MessagePipe;
using R3;
using Sample;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Contents.Scenes
{
    public class GameStageScene : GamePrefabScene<GameStageScene, GameStageSceneComponent>, IGameSceneModel<GameStageSceneModel>, IGameSceneArg<string>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/GameStageScene.prefab";

        public GameStageSceneModel SceneModel { get; set; }

        private string _stageName;
        private SceneInstance _stageSceneInstance;

        public Task PreInitialize(string stageName)
        {
            _stageName = stageName;
            return Task.CompletedTask;
        }

        public override async Task<GameObject> LoadAsset()
        {
            var instance = await base.LoadAsset();
            _stageSceneInstance = await AssetService.LoadSceneAsync(_stageName);
            return instance;
        }

        public override async Task Startup()
        {
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
                    await SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, string>(_stageName);
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
                    Debug.LogError($"Stage Result: {result}");
                    await GameResultUIDialog.RunAsync(SceneModel.CreateStageResult());
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, bool>()
                .Subscribe(MessageKey.GameStage.Finish, handler: _ =>
                {
                    // 次のステージへ
                    SceneModel.StageState = GameStageState.Finish;
                })
                .AddTo(SceneComponent);


            // プレイヤー設定
            // Memo: MessageBrokerからMessageBroker呼ぶのもアレなので、リファクタリングを検討
            GlobalMessageBroker.GetSubscriber<int, int>()
                .Subscribe(MessageKey.Player.AddPoint, handler: point =>
                {
                    SceneModel.AddPoint(point);
                    SceneComponent.UpdateView();
                    if (SceneModel.IsClear())
                    {
                        GlobalMessageBroker.GetAsyncPublisher<int, GameStageResult>().Publish(MessageKey.GameStage.Result, GameStageResult.Clear);
                    }
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, int>()
                .Subscribe(MessageKey.Player.HpDamaged, handler: hp =>
                {
                    SceneModel.PlayerHpDamaged(hp);
                    SceneComponent.UpdateView();
                    if (SceneModel.IsFailed())
                    {
                        GlobalMessageBroker.GetAsyncPublisher<int, GameStageResult>().Publish(MessageKey.GameStage.Result, GameStageResult.Failed);
                    }
                })
                .AddTo(SceneComponent);

            // SceneModel.Mp.SubscribeAwait(async (mp, token) => { await Task.CompletedTask; })
            //     .AddTo(SceneComponent);
            // SceneModel.Mp.Value++;

            // プレイヤー爆誕の儀
            var playerStart = GameSceneHelper.GetPlayerStart(_stageSceneInstance.Scene);
            await playerStart.LoadPlayerAsync();

            // Memo: ビューがモデルの変更を検知する方法については賛否あると思われるが一旦は愚直に渡す
            // (MessageBroker、MessagePipe.Request/Response、R3.ReactivePropertyとか疎結合化は後ほど検討する)
            await SceneComponent.Initialize(SceneModel);
            await base.Startup();
        }

        public override Task Ready()
        {
            // ゲーム開始準備OKの合図
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.GameStage.Ready, true);
            return base.Ready();
        }

        public override async Task Terminate()
        {
            await AssetService.UnloadSceneAsync(_stageSceneInstance);
            await base.Terminate();
        }
    }
}