using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MasterData;
using Game.Core.MessagePipe;
using Game.Core.Services;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Scenes
{
    public interface IGameSceneComponent
    {
        public UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }
    }

    public abstract class GameSceneComponent : MonoBehaviour, IGameSceneComponent
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        protected AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<AudioService> _audioService;
        protected AudioService AudioService => _audioService.Reference;

        private GameServiceReference<GameSceneService> _sceneService;
        protected GameSceneService SceneService => _sceneService.Reference;

        private GameServiceReference<MasterDataService> _masterDataService;
        protected MasterDataService MasterDataService => _masterDataService.Reference;
        protected MemoryDatabase MemoryDatabase => MasterDataService.MemoryDatabase;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        protected MessageBrokerService MessageBrokerService => _messageBrokerService.Reference;
        protected GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private Button[] _buttons = Array.Empty<Button>();

        private void Start()
        {
            _buttons = gameObject.GetComponentsInChildren<Button>();
            if (_buttons.Length > 0)
            {
                _buttons.Select(x => x.OnClickAsObservable())
                    .Merge()
                    .Subscribe(_ => { AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton).Forget(); })
                    .AddTo(this);
            }
        }

        public virtual void SetInteractiveAllButton(bool interactive)
        {
            foreach (var button in _buttons)
            {
                button.interactable = interactive;
            }
        }

        public virtual UniTask Sleep()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                SetInteractiveAllButton(true);
            }

            return UniTask.CompletedTask;
        }
    }
}