using System;
using System.Collections.Generic;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// プロジェクタイル（弾）
    /// 貫通、追尾、クリティカル対応
    /// HitCount: 同一敵への最大ヒット回数（-1=無限）
    /// Penetration: 貫通できる敵の数（0=貫通なし）
    /// ヒット検出: SphereCastNonAlloc（OnTriggerEnter 非使用 — KCC 環境で信頼性がないため）
    /// </summary>
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

        // プライマリヒット処理済みフラグ（SP/MP共通）
        private bool _hasPrimaryHitProcessed;

        // ポーズ参照（生成元マネージャーが設定）
        private SurvivorFusionGameState _gameState;

        // Runner サービス（PhysicsScene・DeltaTime 取得用）
        private IFusionRunnerService _runnerService;

        // SphereCast ヒット検出バッファ
        private readonly RaycastHit[] _sphereCastHits = new RaycastHit[10];

        public int Damage => _damage;
        public bool IsCritical => _isCritical;
        public bool HasPrimaryHitProcessed => _hasPrimaryHitProcessed;

        // Events
        public event Action<SurvivorProjectile, Collider> OnHit;
        public event Action<SurvivorProjectile> OnLifetimeExpired;

        private void Awake()
        {
            // SphereCast ベースのヒット検出では自前コライダー不要
            if (TryGetComponent<Collider>(out var col)) col.enabled = false;

            gameObject.tag = "Projectile";
        }

        /// <summary>
        /// プロジェクタイルを初期化する
        /// </summary>
        /// <param name="gameState">ゲーム状態（ポーズチェック用）</param>
        /// <param name="runnerService">Fusion Runner サービス（PhysicsScene・DeltaTime 取得用）</param>
        public void Initialize(SurvivorFusionGameState gameState, IFusionRunnerService runnerService = null)
        {
            _gameState = gameState;
            _runnerService = runnerService;
        }

        /// <summary>
        /// プロジェクタイルを発射
        /// </summary>
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
            _hasPrimaryHitProcessed = false;

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
            if (_gameState != null && _gameState.IsEffectivelyPaused) return;

            float deltaTime = _runnerService.GetRenderDeltaTime();

            // 追尾処理
            if (_homing > 0 && _homingTarget != null && _homingTarget.gameObject.activeInHierarchy)
            {
                Vector3 targetDirection = (_homingTarget.position - transform.position).normalized;
                float homingFactor = _homing.ToRate();
                _direction = Vector3.Slerp(_direction, targetDirection, homingFactor * deltaTime * HomingInterpolationFactor).normalized;

                if (_direction.magnitude > 0.1f)
                {
                    transform.rotation = Quaternion.LookRotation(_direction);
                }
            }

            // ヒット検出: 移動パス上の SphereCast（貫通対応）
            float moveDistance = _speed * deltaTime;
            if (moveDistance > 0f)
            {
                var physicsScene = _runnerService.GetPhysicsSceneOrDefault();
                int hitCount = physicsScene.SphereCast(
                    transform.position, _colliderRadius, _direction,
                    _sphereCastHits, moveDistance, LayerMaskConstants.Enemy,
                    QueryTriggerInteraction.Collide);

                for (int i = 0; i < hitCount; i++)
                {
                    var hitCollider = _sphereCastHits[i].collider;
                    if (hitCollider.CompareLayer(LayerConstants.Enemy))
                    {
                        OnHit?.Invoke(this, hitCollider);
                        if (!_isActive) break; // プール返却された場合
                    }
                }
            }

            if (!_isActive) return;

            // 移動（SphereCast で検出済みのため物理同期不要）
            transform.position += _direction * moveDistance;

            // 寿命チェック
            _lifetime -= deltaTime;
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

        /// <summary>
        /// サーバーへのヒット報告済みとしてマーク（MPクライアント用）
        /// </summary>
        public void MarkPrimaryHitProcessed()
        {
            _hasPrimaryHitProcessed = true;
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
            _hasPrimaryHitProcessed = false;

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
