using System;
using System.Threading.Tasks;
using Game.Contents.Enemy;
using Game.Contents.Player;
using Game.Contents.UI;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using MessagePipe;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using Random = System.Random;

namespace Game.Contents.Scenes
{
    public class GameStageScene : GamePrefabScene<GameStageScene, GameStageSceneComponent>, IGameSceneModel<GameStageSceneModel>, IGameSceneArg<int>
    {
        protected override string AssetPathOrAddress => "Assets/Prefabs/GameStageScene.prefab";

        public GameStageSceneModel SceneModel { get; set; }

        private int _stageId;
        private SceneInstance _stageSceneInstance;

        private PlayerStart _playerStart;

        public Task PreInitialize(int stageId)
        {
            _stageId = stageId;
            SceneModel.Initialize(stageId);
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
            // ゲーム開始準備OKの合図
            SceneModel.StageState = GameStageState.Ready;
            //カウントダウンしてスタート
            await GameCountdownUIDialog.RunAsync();
            SceneModel.StageState = GameStageState.Start;
            SceneComponent.DoFadeIn();
            _playerStart.PlayerHUD.DoFadeIn();
            await base.Ready();
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
                    TryShowResultDialogAsync().Forget();
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
            GlobalMessageBroker.GetSubscriber<int, Collider>()
                .Subscribe(MessageKey.Player.OnTriggerEnter, handler: other =>
                {
                    if (!other.gameObject.CompareTag("StageItem"))
                        return;

                    // 今はとりあえず一番近いやつでOK
                    var itemMaster = MemoryDatabase.StageItemMasterTable.FindClosestByAssetName(other.name);
                    var point = itemMaster?.Point ?? 1;

                    other.gameObject.SafeDestroy();

                    SceneModel.AddPoint(point);

                    TryShowResultDialogAsync().Forget();
                })
                .AddTo(SceneComponent);
            GlobalMessageBroker.GetSubscriber<int, Collision>()
                .Subscribe(MessageKey.Player.OnCollisionEnter, handler: other =>
                {
                    if (!other.gameObject.CompareTag("Enemy"))
                        return;

                    if (!other.transform.parent.TryGetComponent<EnemyController>(out var enemyController))
                        return;

                    var hpDamage = enemyController.EnemyMaster.HpAttack;

                    other.transform.parent.SafeDestroy();

                    // Memo: エネミーに応じてダメージを変更できるマスタを用意（EnemyMaster）
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
            // Debug.LogError($"Stage Result: {result}");
            await GameResultUIDialog.RunAsync(SceneModel.CreateStageResult());
        }
    }
}