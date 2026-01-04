using System.Linq;
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

        private void Start()
        {
            // Memo: 時すでに遅し、ここしかなかったんや…もちろん後で修正検討
            var buttons = gameObject.GetComponentsInChildren<Button>();
            if (buttons.Length > 0)
            {
                buttons.Select(x => x.OnClickAsObservable())
                    .Merge()
                    .Subscribe(_ => { AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton).Forget(); })
                    .AddTo(this);
            }
        }
    }
}