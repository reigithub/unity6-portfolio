using Game.Contents.Player;
using Game.Core.MasterData.MemoryTables;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Contents.Enemy
{
    /// <summary>
    /// 簡易的なエネミー追尾システム
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;

        // private Rigidbody _playerRigidbody;
        // private SDUnityChanPlayerController _playerController;
        private NavMeshAgent _navMeshAgent;
        private Animator _animator;

        private float _distance;
        private float _viewAngle;
        private NavMeshHit _navMeshHit;

        public EnemyMaster EnemyMaster { get; private set; }

        public void Initialize(GameObject player, EnemyMaster enemyMaster)
        {
            _player = player;
            EnemyMaster = enemyMaster;

            if (TryGetComponent<NavMeshAgent>(out var navMeshAgent))
            {
                _navMeshAgent = navMeshAgent;
                SetSpeed(enemyMaster.WalkSpeed);
            }

            if (TryGetComponent<Animator>(out var animator))
            {
                _animator = animator;
            }
        }

        private void SetSpeed(float speed)
        {
            if (_navMeshAgent)
            {
                _navMeshAgent.speed = speed;
            }

            if (_animator)
            {
                _animator.SetFloat(Animator.StringToHash("Speed"), speed);
            }
        }

        private void Update()
        {
            if (!_player) return;

            DetectPlayerOrPatrol();
        }

        private void DetectPlayerOrPatrol()
        {
            // TODO: StateMachine化の検討

            _distance = Vector3.Distance(_player.transform.position, transform.position);

            // プレイヤー探索
            bool detected = TryDetectPlayerByVision();
            if (!detected) detected = TryDetectPlayerByAudio();

            if (!detected)
            {
                RandomPatrol();
            }
            else
            {
                ResetPatrolRotation();
            }
        }

        private bool TryDetectPlayerByVision()
        {
            //視覚で感知
            // if (_distance > 5f)
            if (_distance > EnemyMaster.VisualDistance)
                return false;

            Vector3 distance = transform.position - _player.transform.position;
            Vector3 cross = Vector3.Cross(transform.forward, distance);
            _viewAngle = Vector3.Angle(transform.forward, distance) * (cross.y < 0f ? -1f : 1f);
            _viewAngle += 180f;

            if (_viewAngle <= 45f || _viewAngle >= 315f)
            {
                //Rayを飛ばしてプレイヤーとの間に障害物がないか確認する
                Vector3 diff = _player.transform.position - transform.position;
                float maxDistance = diff.magnitude;
                Vector3 direction = diff.normalized;
                Vector3 eyePosition = transform.position + new Vector3(0f, 0.5f, 0f);

                // Debug.DrawRay(eyePosition, transform.forward + Quaternion.Euler(0, _viewAngle, 0) * transform.forward * 5f, Color.yellow);
                // Debug.DrawRay(eyePosition, transform.forward + Quaternion.Euler(0, -_viewAngle, 0) * transform.forward * 5f, Color.yellow);

                // int layerMask = ~(1 << 13);
                // RaycastHit[] raycastHits = Physics.RaycastAll(eyeHeightPos, direction, distance, layerMask);

                RaycastHit[] raycastHitResults = new RaycastHit[1];
                var raycastHitCount = Physics.RaycastNonAlloc(new Ray(eyePosition, direction), raycastHitResults, maxDistance);

                // Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), direction * distance, Color.red);

                if (raycastHitCount > 0)
                {
                    // Debug.Log($"raycastHitCount: {raycastHitCount}");

                    if (raycastHitResults[0].transform.gameObject.CompareTag("Player"))
                    {
                        if (NavMesh.SamplePosition(_player.transform.position, out _navMeshHit, 1f, 1))
                        {
                            SetSpeed(EnemyMaster.RunSpeed);
                            return TrySetDestination(_navMeshHit.position, ignoreDistance: true);
                        }
                    }
                }
            }

            return false;
        }

        private bool TryDetectPlayerByAudio()
        {
            // if (_distance > 3f)
            if (_distance > EnemyMaster.AuditoryDistance)
                return false;

            // if (_playerRigidbody.linearVelocity.magnitude > 0.1f)
            // if (_playerController.IsMoving())
            {
                var distance = EnemyMaster.AuditoryDistance;
                float x = _player.transform.position.x + Random.Range(-distance, distance);
                float z = _player.transform.position.z + Random.Range(-distance, distance);

                if (NavMesh.SamplePosition(new Vector3(x, 0f, z), out _navMeshHit, 1f, 1))
                {
                    var forward = _player.transform.position - transform.position;
                    forward.y = 0f;

                    var lookRotation = Quaternion.LookRotation(forward);
                    var slerp = Quaternion.Slerp(transform.rotation, lookRotation, 5f * Time.deltaTime);
                    transform.rotation = slerp;

                    // if (_playerController.IsMoving())
                    {
                        SetSpeed(EnemyMaster.WalkSpeed);
                        return TrySetDestination(_navMeshHit.position, ignoreDistance: true);
                    }
                }
            }

            return false;
        }

        private float _rotationInterval = 5.0f;
        private float _rotationIntervalCount;
        private float _remainingDistance = 0.5f;

        private void RandomPatrol()
        {
            SetSpeed(EnemyMaster.WalkSpeed);

            _rotationIntervalCount += Time.deltaTime;

            var randomPos = transform.position + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
            TrySetDestination(randomPos, remainingDistance: _remainingDistance);

            // Debug.LogError($"{nameof(EnemyController)} {_enemyMaster.Id} {_enemyMaster.Name} currentPos:{transform.position} speed:{_navMeshAgent.speed}");
            // Debug.DrawRay(transform.position + new Vector3(0, 0.5f, 0), transform.forward * 5f, Color.red);

            if (_rotationIntervalCount > _rotationInterval)
            {
                var forward = new Vector3(0f, Random.Range(0f, 180f), 0f);
                var lookRotation = Quaternion.LookRotation(forward);
                var slerp = Quaternion.Slerp(transform.rotation, lookRotation, 10f * Time.deltaTime);
                transform.rotation = slerp;
                // transform.rotation = Quaternion.Euler(forward);
                ResetPatrolRotation();
            }
        }

        private void ResetPatrolRotation()
        {
            _rotationIntervalCount = 0f;
            _rotationInterval = Random.Range(3f, 8f);
            _remainingDistance = Random.Range(0.3f, 0.8f);
        }

        private bool TrySetDestination(Vector3 position, bool ignoreDistance = false, float remainingDistance = 0.5f)
        {
            if (_navMeshAgent)
            {
                if (_navMeshAgent.pathStatus != NavMeshPathStatus.PathInvalid)
                {
                    if (!ignoreDistance && _navMeshAgent.remainingDistance > remainingDistance)
                        return false;

                    _navMeshAgent.SetDestination(position);
                    return true;
                }
            }

            return false;
        }
    }
}