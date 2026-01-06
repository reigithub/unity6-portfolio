using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Core.Constants;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.Scenes
{
    public class GameTitleSceneComponent : GameSceneComponent
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _quitButton;

        [SerializeField] private Animator _animator;

        public void Initialize()
        {
            if (_startButton)
            {
                _startButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractiveAllButton(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Start, true, token);

                        // 今のところプレイモードは１つなので
                        var stageId = MemoryDatabase.StageMasterTable.All.Min(x => x.Id);
                        await SceneService.TransitionAsync<GameStageScene, int>(stageId);
                    })
                    .AddTo(this);
            }

            if (_quitButton)
            {
                _quitButton.OnClickAsObservableThrottleFirst()
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractiveAllButton(false);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Quit, true, token);
                    })
                    .AddTo(this);
            }

            SetInteractiveAllButton(true);
        }

        public async Task ReadyAsync()
        {
            GlobalMessageBroker.GetPublisher<int, string>().Publish(MessageKey.Player.PlayAnimation, PlayerConstants.GameTitleSceneAnimatorStateName);
            await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Ready, true);
        }
    }
}