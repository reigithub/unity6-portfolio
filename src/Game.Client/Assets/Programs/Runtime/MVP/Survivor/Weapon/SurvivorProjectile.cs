using System;
using System.Collections.Generic;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// プロジェクタイル（弾）
    /// 貫通、追尾、クリティカル対応
    /// HitCount: 同一敵への最大ヒット回数（-1=無限）
    /// Penetration: 貫通できる敵の数（0=貫通なし）
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class SurvivorProjectile : MonoBehaviour, IPoolableWeaponItem
    {
        // 追尾補間係数（Homing値と掛け合わせて最終的な追尾強度を決定）
        // 値を大きくすると追尾が鋭くなる
        private const float HomingInterpolationFactor = 5f;

        [SerializeField] private TrailRenderer _trailRenderer;

        [Tooltip("弾の当たり判定半径（武器ごとにプレハブで調整可能）")]
        [SerializeField] private float _colliderRadius = 0.3f;

        // State
        private Vector3 _direction;
        private float _speed;
        private float _lifetime;
        private int _damage;
        private int _hitCount;
        private int _pierce;
        private int _remainingPierce;
        private int _homing;
        private bool _isCritical;
        private bool _isActive;
        private Transform _homingTarget;

        // 各敵への残りヒット回数を追跡（enemyInstanceId -> remainingHits）
        private readonly Dictionary<int, int> _hitCountPerEnemy = new();

        public int Damage => _damage;
        public bool IsCritical => _isCritical;

        // Events
        public event Action<SurvivorProjectile, Collider> OnHit;
        public event Action<SurvivorProjectile> OnLifetimeExpired;

        private void Awake()
        {
            // コライダーをTriggerとして設定
            if (TryGetComponent<SphereCollider>(out var sc))
            {
                sc.isTrigger = true;
                sc.radius = _colliderRadius;
            }

            // Rigidbodyを物理演算から除外（トリガー検出のみに使用）
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // タグを設定
            gameObject.tag = "Projectile";
        }

        /// <summary>
        /// プロジェクタイルを発射
        /// </summary>
        /// <param name="direction">発射方向（正規化済み）</param>
        /// <param name="speed">移動速度（units/s）</param>
        /// <param name="damage">ダメージ量</param>
        /// <param name="lifetime">生存時間（秒）</param>
        /// <param name="hitCount">同一敵への最大ヒット回数（-1=無限）</param>
        /// <param name="pierce">貫通できる敵の数（0=貫通なし）</param>
        /// <param name="homing">追尾性能（%）、0=直進、100=完全追尾</param>
        /// <param name="isCritical">クリティカルヒットかどうか</param>
        public void Fire(Vector3 direction, float speed, int damage, float lifetime, int hitCount, int pierce, int homing, bool isCritical)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _hitCount = hitCount;
            _pierce = pierce;
            _remainingPierce = pierce;
            _homing = homing;
            _isCritical = isCritical;
            _isActive = true;
            _homingTarget = null;
            _hitCountPerEnemy.Clear();

            // 向きを設定
            if (_direction.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(_direction);
            }

            // トレイルをリセット
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // 追尾処理
            if (_homing > 0 && _homingTarget != null && _homingTarget.gameObject.activeInHierarchy)
            {
                Vector3 targetDirection = (_homingTarget.position - transform.position).normalized;
                float homingFactor = _homing.ToRate();
                _direction = Vector3.Slerp(_direction, targetDirection, homingFactor * Time.deltaTime * HomingInterpolationFactor).normalized;

                if (_direction.magnitude > 0.1f)
                {
                    transform.rotation = Quaternion.LookRotation(_direction);
                }
            }

            // 移動
            transform.position += _direction * _speed * Time.deltaTime;

            // 寿命チェック
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                _isActive = false;
                OnLifetimeExpired?.Invoke(this);
            }
        }

        /// <summary>
        /// 追尾ターゲットを設定
        /// </summary>
        public void SetHomingTarget(Transform target)
        {
            _homingTarget = target;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            // メッシュコライダーが子オブジェクトにある場合に対応
            // 親を辿ってEnemyタグを確認
            if (other.CompareLayer(LayerConstants.Enemy))
            {
                OnHit?.Invoke(this, other);
            }
        }

        /// <summary>
        /// 敵にヒットした時の処理
        /// </summary>
        /// <param name="enemyInstanceId">敵のインスタンスID</param>
        /// <returns>true: 弾を消す, false: 継続</returns>
        public bool ProcessHit(int enemyInstanceId)
        {
            // HitCount=-1 は無限ヒット（AoE等）
            if (_hitCount < 0)
            {
                // 貫通チェックのみ
                return CheckPierceExpired(enemyInstanceId);
            }

            // この敵への初回ヒット？
            if (!_hitCountPerEnemy.TryGetValue(enemyInstanceId, out int remainingHits))
            {
                remainingHits = _hitCount;
                _hitCountPerEnemy[enemyInstanceId] = remainingHits;
            }

            // ヒット回数を消費
            remainingHits--;
            _hitCountPerEnemy[enemyInstanceId] = remainingHits;

            // この敵へのヒット回数が尽きた場合、貫通をチェック
            if (remainingHits <= 0)
            {
                return CheckPierceExpired(enemyInstanceId);
            }

            return false;
        }

        /// <summary>
        /// 貫通数をチェックして弾を消すか判定
        /// </summary>
        private bool CheckPierceExpired(int enemyInstanceId)
        {
            // Penetration=0 は貫通なし（最初の敵で消える）
            if (_pierce <= 0)
            {
                _isActive = false;
                return true;
            }

            // 新しい敵に当たった場合のみ貫通数を減らす
            // （同一敵への複数ヒットでは減らさない）
            if (!_hitCountPerEnemy.ContainsKey(enemyInstanceId) || _hitCountPerEnemy[enemyInstanceId] == _hitCount - 1)
            {
                _remainingPierce--;
            }

            if (_remainingPierce < 0)
            {
                _isActive = false;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _isActive = false;
            _direction = Vector3.zero;
            _speed = 0f;
            _damage = 0;
            _lifetime = 0f;
            _hitCount = 0;
            _pierce = 0;
            _remainingPierce = 0;
            _homing = 0;
            _isCritical = false;
            _homingTarget = null;
            _hitCountPerEnemy.Clear();

            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        /// <summary>
        /// イベントリスナーをクリア（プール破棄時に呼ばれる）
        /// </summary>
        public void ClearListeners()
        {
            OnHit = null;
            OnLifetimeExpired = null;
        }
    }
}