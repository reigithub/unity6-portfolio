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

        public static UniTask<bool> RunAsync(float countdown = 3f)
        {
            var sceneService = GameServiceManager.Get<GameSceneService>();
            return sceneService.TransitionDialogAsync<GameCountdownUIDialog, float, bool>(countdown);
        }

        public override UniTask Startup()
        {
            ApplicationEvents.PauseTime();
            SceneComponent.Initialize(this, _countdown);
            return base.Startup();
        }

        public override UniTask Ready()
        {
            SceneComponent.CountdownStart();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();
            return base.Terminate();
        }
    }

    public class GameCountdownUI : GameSceneComponent
    {
        [SerializeField]
        private TextMeshProUGUI _countdownText;

        private IGameSceneResult<bool> _result;
        private float _countdown;
        private bool _countdownStart;

        public void Initialize(IGameSceneResult<bool> result, float countdown)
        {
            _result = result;
            _countdown = countdown;
            _countdownText.text = countdown.ToString("F0");
        }

        public void CountdownStart()
        {
            _countdownStart = true;
        }

        private void Update()
        {
            if (!_countdownStart) return;

            if (_countdown < 0f)
            {
                _result.TrySetResult(true);
                return;
            }

            _countdown -= Time.unscaledDeltaTime;
            _countdownText.text = _countdown <= 1f
                ? "Game Start!"
                : _countdown.ToString("F0");
        }
    }
}
