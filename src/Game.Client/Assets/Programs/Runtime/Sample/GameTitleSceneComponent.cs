using Game.Core;
using Game.Core.Extensions;
using Game.Core.Scenes;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class GameTitleSceneComponent : GameSceneComponent
    {
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _quitButton;

        private GameServiceReference<AddressableAssetService> _assetService;
        private GameServiceReference<GameSceneService> _sceneService;

        public void Initialize()
        {
            if (_startButton)
            {
                _startButton.onClick.AddListener(() => { _sceneService.Reference.TransitionAsync<GameStageScene, string>("Stage00").Forget(); });
            }

            if (_quitButton)
            {
                _quitButton.onClick.AddListener(() => { GameManager.Instance.GameExit(); });
            }
        }
    }
}