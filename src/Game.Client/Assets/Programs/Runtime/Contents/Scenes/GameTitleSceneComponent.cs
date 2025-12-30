using Game.Core;
using Game.Core.Extensions;
using Game.Core.MessagePipe;
using Game.Core.Scenes;
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
                _startButton.onClick.AddListener(() =>
                {
                    SceneService.TransitionAsync<GameStageScene, GameStageSceneModel, string>("Stage00").Forget();
                    GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Game.Start, true);
                });
            }

            if (_quitButton)
            {
                _quitButton.onClick.AddListener(() => { GlobalMessageBroker.GetPublisher<int, bool>().Publish(MessageKey.Game.Quit, true); });
            }
        }
    }
}