using UnityEngine;
using UnityEngine.AI;

namespace Sample
{
    /// <summary>
    /// 簡易的なエネミー追尾システム
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private Transform _player;

        private NavMeshAgent _navMeshAgent;

        private void Start()
        {
            if (TryGetComponent<NavMeshAgent>(out var navMeshAgent))
            {
                _navMeshAgent = navMeshAgent;
            }
        }

        private void Update()
        {
            if (_player)
            {
                if (_navMeshAgent && _navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid)
                {
                    _navMeshAgent.SetDestination(_player.position);
                }
            }
        }

        public void SetPlayer(GameObject player)
        {
            _player = player.transform;
        }

        // TODO: StateMachineでプレイヤー探索～追尾～ロスト～配置に戻るを表現する
    }
}