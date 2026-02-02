using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Constants;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Client.MasterData;
using Game.MVC.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Extensions;
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

        private IGameSceneService _sceneService;
        private IGameSceneService SceneService => _sceneService ??= GameServiceManager.Get<GameSceneService>();

        private IMasterDataService _masterDataService;
        private IMasterDataService MasterDataService => _masterDataService ??= GameServiceManager.Get<MasterDataService>();
        private MemoryDatabase MemoryDatabase => MasterDataService.MemoryDatabase;

        private IMessagePipeService _messagePipeService;
        private IMessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        public void Initialize()
        {
            if (_startButton)
            {
                _startButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractables(false);
                        AudioService.StopBgmAsync(token).Forget();
                        await AudioService.PlayRandomOneAsync(AudioPlayTag.GameStart, token);

                        // 今のところプレイモードは１つなので
                        var stageId = MemoryDatabase.ScoreTimeAttackStageMasterTable.All.Min(x => x.Id);
                        await SceneService.TransitionAsync<ScoreTimeAttackStageScene, int>(stageId);
                    })
                    .AddTo(this);
            }

            if (_gameModeButton != null)
            {
                _gameModeButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, _) =>
                    {
                        await SceneService.TerminateLastAsync();
                        await ApplicationEvents.RequestReturnToTitleAsync();
                    })
                    .AddTo(this);
            }

            if (_quitButton)
            {
                _quitButton.OnClickAsObservableThrottleFirst()
                    .Subscribe(_ =>
                    {
                        SetInteractables(false);
                        ApplicationEvents.RequestShutdown();
                    })
                    .AddTo(this);
            }

            SetInteractables(true);
        }

        public async UniTask ReadyAsync()
        {
            MessagePipeService.Publish(MessageKey.Player.PlayAnimation, PlayerConstants.GameTitleSceneAnimatorStateName);
            await AudioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
        }
    }
}