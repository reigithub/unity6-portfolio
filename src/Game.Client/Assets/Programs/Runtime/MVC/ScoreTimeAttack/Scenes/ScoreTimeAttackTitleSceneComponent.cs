using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Constants;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.Scenes
{
    public class ScoreTimeAttackTitleSceneComponent : GameSceneComponent
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _gameModeButton;
        [SerializeField] private Button _quitButton;

        [SerializeField] private Animator _animator;

        private IAudioService _audioService;
        private IGameSceneService _sceneService;
        private IMasterDataService _masterDataService;
        private IMessagePipeService _messagePipeService;

        public void Initialize()
        {
            _audioService = GameServiceManager.Resolve<IAudioService>();
            _sceneService = GameServiceManager.Resolve<IGameSceneService>();
            _masterDataService = GameServiceManager.Resolve<IMasterDataService>();
            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();

            if (_startButton)
            {
                _startButton.OnClickAsObservable()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        _audioService.StopBgmAsync(token).Forget();
                        await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);

                        // 今のところプレイモードは１つなので
                        var stageId = _masterDataService.MemoryDatabase.ScoreTimeAttackStageMasterTable.All.Min(x => x.Id);
                        await _sceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(stageId);
                    })
                    .AddTo(this);
            }

            if (_gameModeButton != null)
            {
                _gameModeButton.OnClickAsObservable()
                    .SubscribeAwait(async (_, _) =>
                    {
                        SetInteractable(false);
                        await _sceneService.TerminateLastAsync();
                        await ApplicationEvents.RequestReturnToTitleAsync();
                    })
                    .AddTo(this);
            }

            if (_quitButton)
            {
                _quitButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        SetInteractable(false);
                        ApplicationEvents.RequestShutdown();
                    })
                    .AddTo(this);
            }

            SetInteractable(true);
        }

        public async UniTask ReadyAsync()
        {
            _messagePipeService.Publish(MessageKey.Player.PlayAnimation, PlayerConstants.GameTitleSceneAnimatorStateName);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
        }
    }
}
