using System;
using System.Linq;
using System.Threading.Tasks;
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
    public abstract class GameSceneComponent : MonoBehaviour
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
            // Memo: 時すでに遅し、ここしかなかったんや…もちろん後で修正検討
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

        public virtual Task Sleep()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            return Task.CompletedTask;
        }

        public virtual Task Restart()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                SetInteractiveAllButton(true);
            }

            return Task.CompletedTask;
        }
    }
}