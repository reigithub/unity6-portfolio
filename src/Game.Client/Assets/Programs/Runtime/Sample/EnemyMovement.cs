using UnityEngine;
using UnityEngine.AI;

namespace Sample
{
    public class EnemyMovement : MonoBehaviour
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
            if (_player is not null)
            {
                if (_navMeshAgent && _navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid)
                {
                    _navMeshAgent.SetDestination(_player.position);
                }
            }
        }
    }
}