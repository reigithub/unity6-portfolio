using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorカウントダウンダイアログ（Presenter）
    /// ゲーム開始前のカウントダウン表示を管理
    /// </summary>
    public class SurvivorCountdownDialog : GameDialogScene<SurvivorCountdownDialog, SurvivorCountdownDialogComponent, bool>
    {
        protected override string AssetPathOrAddress => "SurvivorCountdownDialog";

        private const float CountdownInterval = 1f;
        private const int StartCount = 3;

        public static UniTask<bool> RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorCountdownDialog, bool>();
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            // カウントダウンを開始（時間停止中でも動作するようにrealtime使用）
            RunCountdownAsync().Forget();
        }

        private async UniTaskVoid RunCountdownAsync()
        {
            // 3, 2, 1 のカウントダウン
            for (int i = StartCount; i > 0; i--)
            {
                SceneComponent.ShowCount(i);
                await UniTask.Delay((int)(CountdownInterval * 1000), DelayType.Realtime);
            }

            // GO! 表示
            SceneComponent.ShowGo();
            await UniTask.Delay(500, DelayType.Realtime);

            // 完了を通知
            TrySetResult(true);
        }
    }
}
