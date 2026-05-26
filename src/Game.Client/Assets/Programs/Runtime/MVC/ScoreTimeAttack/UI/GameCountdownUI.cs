using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using R3;
using TMPro;
using UnityEngine;

namespace Game.ScoreTimeAttack.UI
{
    public class GameCountdownUIDialog : GameDialogScene<GameCountdownUIDialog, GameCountdownUI, bool>, IGameSceneArg<float>
    {
        protected override string AssetPathOrAddress => "GameCountdownUI";

        private float _countdown;

        public UniTask ArgHandle(float countdown)
        {
            _countdown = countdown;
            return UniTask.CompletedTask;
        }

        public static async UniTask<bool> RunAsync(float countdown = 3f)
        {
            bool result;
            var inputService = GameServiceManager.Get<InputSystemService>();
            using (inputService.BlockPlayer())
            {
                var sceneService = GameServiceManager.Get<GameSceneService>();
                result = await sceneService.TransitionDialogAsync<GameCountdownUIDialog, float, bool>(countdown);
            }
            return result;
        }

        public override UniTask Startup()
        {
            ApplicationEvents.PauseTime();
            return base.Startup();
        }

        public override UniTask Ready()
        {
            RunCountdownAsync().Forget();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            return base.Terminate();
        }

        private async UniTaskVoid RunCountdownAsync()
        {
            // 3, 2, 1 のカウントダウン
            for (float i = _countdown; i > 0; i--)
            {
                SceneComponent.SetCountdown(i);
                await UniTask.Delay(1000, DelayType.Realtime);
            }

            SceneComponent.SetGameStart();
            await UniTask.Delay(500, DelayType.Realtime);

            // 完了を通知
            TrySetResult(true);
        }
    }

    public class GameCountdownUI : GameSceneComponent
    {
        [SerializeField]
        private TextMeshProUGUI _countdownText;

        public void SetCountdown(float countdown) => _countdownText.text = countdown.ToString("F0");

        public void SetGameStart() => _countdownText.text = "Game Start!";
    }
}
