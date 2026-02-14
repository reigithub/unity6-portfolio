using Game.Shared.Combat;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 各敵GameObjectに付与するMonoBehaviour
    /// ICombatTargetを実装し、既存武器システムからのダメージをECSのDamageEventに橋渡し
    /// </summary>
    public class EcsEnemyProxy : MonoBehaviour, ICombatTarget
    {
        /// <summary>対応するECSエンティティ</summary>
        private Entity _entity;

        /// <summary>ECS Worldの参照</summary>
        private World _world;

        /// <summary>ECS上で死亡済みかどうか</summary>
        private bool _isDead;

        /// <summary>コライダーの参照（CenterPosition用）</summary>
        private Collider _collider;

        /// <summary>対応するECSエンティティ</summary>
        public Entity Entity => _entity;

        /// <summary>
        /// ECSエンティティとWorldを紐付けて初期化
        /// </summary>
        public void Initialize(Entity entity, World world)
        {
            _entity = entity;
            _world = world;
            _isDead = false;
        }

        private void Awake()
        {
            _collider = GetComponentInChildren<Collider>();
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
        }

        /// <summary>
        /// プールに戻すためのリセット
        /// </summary>
        public void ResetForPool()
        {
            _entity = Entity.Null;
            _world = null;
            _isDead = false;
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
