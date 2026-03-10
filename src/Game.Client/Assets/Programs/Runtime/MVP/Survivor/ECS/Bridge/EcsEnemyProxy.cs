using Game.Shared.Combat;
using Game.Shared.Constants;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 各敵GameObjectに付与するMonoBehaviour
    /// ICombatTargetを実装し、既存武器システムからのダメージをECSのDamageEventに橋渡し
    /// またECS AIStateに基づいてアニメーションと攻撃を駆動する
    /// </summary>
    public class EcsEnemyProxy : MonoBehaviour, ICombatTarget
    {
        // Animator hashes（SurvivorEnemyPresenterと同一パラメータ）
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        /// <summary>対応するECSエンティティ</summary>
        private Entity _entity;

        /// <summary>ECS Worldの参照</summary>
        private World _world;

        /// <summary>ECS上で死亡済みかどうか</summary>
        private bool _isDead;

        /// <summary>コライダーの参照（CenterPosition用）</summary>
        private Collider _collider;

        /// <summary>Animator参照</summary>
        private Animator _animator;

        /// <summary>前フレームのAI状態（変化検知用）</summary>
        private EcsEnemyAIStateType _lastAIState;

        /// <summary>攻撃タイマー（ECS AttackStateのクールダウンとは別にProxy側で管理）</summary>
        private float _proxyAttackTimer;

        /// <summary>攻撃ダメージ（ECS EnemyDataから取得）</summary>
        private int _attackDamage;

        /// <summary>攻撃範囲（ECS EnemyDataから取得）</summary>
        private float _attackRange;

        // Physics overlap用バッファ
        private static readonly Collider[] s_overlapBuffer = new Collider[8];

        /// <summary>対応するECSエンティティ</summary>
        public Entity Entity => _entity;

        /// <summary>敵マスターID（プール返却時に使用）</summary>
        public int EnemyId { get; private set; }

        /// <summary>
        /// ECSエンティティとWorldを紐付けて初期化
        /// </summary>
        public void Initialize(Entity entity, World world, int enemyId)
        {
            _entity = entity;
            _world = world;
            _isDead = false;
            _lastAIState = EcsEnemyAIStateType.Chase;
            _proxyAttackTimer = 0f;
            EnemyId = enemyId;

            // コライダーを有効化
            if (_collider != null)
                _collider.enabled = true;

            // EnemyDataからステータスをキャッシュ
            if (IsEntityValid())
            {
                var data = _world.EntityManager.GetComponentData<EnemyData>(_entity);
                _attackDamage = data.AttackDamage;
                _attackRange = data.AttackRange;
            }
        }

        private void Awake()
        {
            _collider = GetComponentInChildren<Collider>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_isDead || !IsEntityValid())
                return;

            var entityManager = _world.EntityManager;
            var aiState = entityManager.GetComponentData<EnemyAIState>(_entity);
            var enemyData = entityManager.GetComponentData<EnemyData>(_entity);

            // AI状態変化を検知してアニメーションをトリガー
            if (aiState.CurrentState != _lastAIState)
            {
                OnAIStateChanged(_lastAIState, aiState.CurrentState);
                _lastAIState = aiState.CurrentState;
            }

            // 状態に応じたUpdate処理
            switch (aiState.CurrentState)
            {
                case EcsEnemyAIStateType.Chase:
                    // 移動速度をAnimatorに反映
                    if (_animator != null)
                    {
                        float speed = enemyData.MoveSpeed > 0 ? 1f : 0f;
                        _animator.SetFloat(SpeedHash, speed);
                    }
                    break;

                case EcsEnemyAIStateType.Attack:
                    // 攻撃タイマー管理
                    _proxyAttackTimer -= Time.deltaTime;
                    if (_proxyAttackTimer <= 0f)
                    {
                        PerformAttack();
                        _proxyAttackTimer = enemyData.AttackCooldown;
                    }
                    break;
            }
        }

        /// <summary>
        /// AI状態が変化した時のアニメーション駆動
        /// </summary>
        private void OnAIStateChanged(EcsEnemyAIStateType oldState, EcsEnemyAIStateType newState)
        {
            if (_animator == null) return;

            switch (newState)
            {
                case EcsEnemyAIStateType.Chase:
                    // Chaseに戻った場合、速度パラメータは Updateで設定
                    break;

                case EcsEnemyAIStateType.Attack:
                    _animator.SetFloat(SpeedHash, 0f);
                    _animator.SetTrigger(AttackHash);
                    _proxyAttackTimer = 0f; // 最初の攻撃は即時
                    break;

                case EcsEnemyAIStateType.HitStun:
                    _animator.SetFloat(SpeedHash, 0f);
                    _animator.SetTrigger(HitHash);
                    break;

                case EcsEnemyAIStateType.Dead:
                    _animator.SetTrigger(DeathHash);
                    break;
            }
        }

        /// <summary>
        /// プレイヤーへの攻撃実行
        /// </summary>
        private void PerformAttack()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _attackRange, s_overlapBuffer, LayerMaskConstants.Player);

            for (int i = 0; i < count; i++)
            {
                var damageable = s_overlapBuffer[i].GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    damageable.TakeDamage(_attackDamage);
                }
            }
        }

        #region ICombatTarget Implementation

        /// <summary>
        /// エンティティの中心位置
        /// </summary>
        public Vector3 CenterPosition => _collider != null
            ? _collider.bounds.center
            : transform.position;

        /// <summary>
        /// 死亡しているかどうか
        /// </summary>
        public bool IsDead => _isDead;

        /// <summary>
        /// ダメージを受ける
        /// 武器システムから呼び出され、ECSのDamageEventコンポーネントに書き込む
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (_isDead || !IsEntityValid())
                return;

            var entityManager = _world.EntityManager;

            // DamageEventコンポーネントにダメージを書き込み
            var currentEvent = entityManager.GetComponentData<DamageEvent>(_entity);
            currentEvent.Damage += damage;
            entityManager.SetComponentData(_entity, currentEvent);
        }

        /// <summary>
        /// ノックバックを適用
        /// </summary>
        public void ApplyKnockback(Vector3 knockback)
        {
            if (_isDead || !IsEntityValid())
                return;

            var entityManager = _world.EntityManager;

            var currentEvent = entityManager.GetComponentData<DamageEvent>(_entity);
            currentEvent.Knockback += new float3(knockback.x, knockback.y, knockback.z);
            entityManager.SetComponentData(_entity, currentEvent);
        }

        #endregion

        /// <summary>
        /// 死亡状態を設定（Bridge経由で呼び出し）
        /// </summary>
        public void SetDead()
        {
            _isDead = true;

            // コライダーを無効化（死亡後のダメージ判定を防止）
            if (_collider != null)
                _collider.enabled = false;
        }

        /// <summary>
        /// プールに戻すためのリセット
        /// </summary>
        public void ResetForPool()
        {
            _entity = Entity.Null;
            _world = null;
            _isDead = false;
            _lastAIState = EcsEnemyAIStateType.Chase;
            _proxyAttackTimer = 0f;

            // Animatorの状態をリセット
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }

            gameObject.SetActive(false);
        }

        private bool IsEntityValid()
        {
            return _world != null && _world.IsCreated &&
                   _entity != Entity.Null &&
                   _world.EntityManager.Exists(_entity);
        }
    }
}
