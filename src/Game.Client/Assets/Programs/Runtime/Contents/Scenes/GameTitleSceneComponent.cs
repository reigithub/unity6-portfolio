using System;
using System.Linq;
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
                _startButton
                    .OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(3))
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        // await AudioService.PlayRandomAsync(AudioCategory.Voice, AudioPlayTag.GameStart, token);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Start, true, token);

                        // 今のところプレイモードは１つなので
                        var master = MemoryDatabase.StageMasterTable.All
                            .OrderBy(x => x.Id)
                            .FirstOrDefault();
                        var stageId = master?.Id ?? 1; // 本来はエラーメッセージだして落とす
                        await SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, int>(stageId);
                    })
                    .AddTo(this);
            }

            if (_quitButton)
            {
                _quitButton
                    .OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(3))
                    .SubscribeAwait(async (_, token) =>
                    {
                        SetInteractable(false);
                        // await AudioService.PlayRandomAsync(AudioCategory.Voice, AudioPlayTag.GameQuit);
                        await GlobalMessageBroker.GetAsyncPublisher<int, bool>().PublishAsync(MessageKey.Game.Quit, true, token);
                    })
                    .AddTo(this);
            }

            SetInteractable(true);
        }

        public void SetInteractable(bool interactable)
        {
            if (_startButton) _startButton.interactable = interactable;
            if (_quitButton) _quitButton.interactable = interactable;
        }

        public void OnReady()
        {
            if (_animator) _animator.Play("Salute"); // MessageBrokerで起動できるようにする

            GlobalMessageBroker.GetAsyncPublisher<int, bool>().Publish(MessageKey.Game.Ready, true);
        }
    }
}