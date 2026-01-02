using System;
using System.Linq;
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

        public void Initialize()
        {
            if (_startButton)
            {
                _startButton
                    .OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(3))
                    .SubscribeAwait(async (_, _) =>
                    {
                        var master = MemoryDatabase.StageMasterTable.All
                            .OrderBy(x => x.Id)
                            .FirstOrDefault();
                        var stageId = master?.Id ?? 1; // 本来はエラーメッセージだして落とす
                        await SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, int>(stageId);

                        GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Game.Start, true);
                    })
                    .AddTo(this);
            }

            if (_quitButton)
            {
                _quitButton
                    .OnClickAsObservable()
                    .ThrottleFirst(TimeSpan.FromSeconds(3))
                    .Subscribe(_ => GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Game.Quit, true))
                    .AddTo(this);
            }
        }
    }
}