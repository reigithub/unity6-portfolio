using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using Game.Core.Services;
using TMPro;
using UnityEngine;

namespace Game.Contents.UI
{
    public class GameCountdownUIDialog : GameDialogScene<GameCountdownUIDialog, GameCountdownUI, bool>
    {
        protected override string AssetPathOrAddress => "GameCountdownUI";

        public static UniTask<bool> RunAsync(float countdown = 3f)
        {
            var sceneService = GameServiceManager.Instance.GetService<GameSceneService>();
            return sceneService.TransitionDialogAsync<GameCountdownUIDialog, GameCountdownUI, bool>(
                initializer: (component, result) =>
                {
                    component.Initialize(result, countdown);
                    return UniTask.CompletedTask;
                }
            );
        }

        public override UniTask Startup()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, false);
            return base.Startup();
        }

        public override UniTask Ready()
        {
            SceneComponent.CountdownStart();
            return base.Ready();
        }

        public override UniTask Terminate()
        {
            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.System.TimeScale, true);
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