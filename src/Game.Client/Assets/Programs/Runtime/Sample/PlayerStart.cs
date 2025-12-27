using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.MessagePipe;
using Game.Core.Services;
using UnityEngine;

namespace Sample
{
    /// <summary>
    /// プレイヤー生成地点
    /// </summary>
    public class PlayerStart : MonoBehaviour
    {
        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        public async UniTask LoadPlayerAsync()
        {
            var assetService = GameServiceManager.Instance.GetService<AddressableAssetService>();
            var player = await assetService.InstantiateAsync("Assets/Prefabs/Player_SDUnityChan.prefab", transform);

            // Memo: この辺、もう少しキレイにかけるはず...

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                if (enemy.TryGetComponent<EnemyMovement>(out var enemyMovement))
                {
                    enemyMovement.SetPlayer(player);
                }
            }

            GlobalMessageBroker.GetPublisher<int, GameObject>()
                .Publish(MessageKey.Player.SpawnPlayer, player);
        }
    }
}