using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using Game.Core.Services;
using UnityEngine;

namespace Game.Contents.Player
{
    /// <summary>
    /// プレイヤー生成地点
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        public async UniTask<GameObject> LoadPlayerAsync()
        {
            var assetService = GameServiceManager.Instance.GetService<AddressableAssetService>();
            var player = await assetService.InstantiateAsync("Assets/Prefabs/Player_SDUnityChan.prefab", transform);

            GlobalMessageBroker.GetPublisher<int, GameObject>().Publish(MessageKey.Player.SpawnPlayer, player);

            return player;
        }
    }
}