using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 自動発射武器（純粋C#）
    /// 最も近い敵に向かって自動的に弾を発射
    /// マスターデータ駆動、レベル毎のアセット対応
    /// </summary>
    public class SurvivorAutoFireWeapon : SurvivorWeaponBase
    {
        [Inject] private readonly IAddressableAssetService _assetService;
        private readonly Transform _poolParent;

        // アセット名ごとのプールを管理（レベルアップ時の再利用のため）
        private readonly Dictionary<string, ProjectilePool> _poolsByAssetName = new();
        private ProjectilePool _currentPool;
        private bool _isInitialized;

        // Cache
        private readonly Collider[] _hitBuffer = new Collider[50];

        public SurvivorAutoFireWeapon(Transform poolParent)
        {
            _poolParent = poolParent;
        }

        public override async UniTask InitializeAsync(
            SurvivorWeaponMaster weaponMaster,
            IReadOnlyList<SurvivorWeaponLevelMaster> levelMasters,
            Transform owner,
            float damageMultiplier,
            SurvivorVfxSpawner vfxSpawner)
        {
            await base.InitializeAsync(weaponMaster, levelMasters, owner, damageMultiplier, vfxSpawner);

            // 初期プールを作成
            if (!string.IsNullOrEmpty(_currentAssetName))
            {
                await GetOrCreatePoolAsync(_currentAssetName);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// アセット名が変更された時（レベルアップで見た目が変わる場合）
        /// </summary>
        protected override void OnAssetNameChanged(string oldAssetName, string newAssetName)
        {
            // 非同期でプールを切り替え、古いプールは破棄
            SwitchPoolAsync(oldAssetName, newAssetName).Forget();
        }

        private async UniTask SwitchPoolAsync(string oldAssetName, string newAssetName)
        {
            // 新しいプールを作成/取得
            _currentPool = await GetOrCreatePoolAsync(newAssetName);

            // 古いプールを破棄（アクティブなプロジェクタイルがなくなってから）
            if (!string.IsNullOrEmpty(oldAssetName) && _poolsByAssetName.TryGetValue(oldAssetName, out var oldPool))
            {
                // 遅延破棄を開始（アクティブなプロジェクタイルが全て戻るまで待つ）
                DisposeOldPoolAsync(oldAssetName, oldPool).Forget();
            }

            Debug.Log($"[SurvivorAutoFireWeapon] Switched to pool: {newAssetName}");
        }

        /// <summary>
        /// 古いプールをアクティブなプロジェクタイルがなくなってから破棄
        /// </summary>
        private async UniTask DisposeOldPoolAsync(string assetName, ProjectilePool pool)
        {
            // アクティブなプロジェクタイルが全て戻るまで待機（最大10秒）
            float timeout = 10f;
            float elapsed = 0f;

            while (pool.ActiveCount > 0 && elapsed < timeout)
            {
                await UniTask.Delay(100);
                elapsed += 0.1f;
            }

            // プールを破棄
            pool.Clear();
            _poolsByAssetName.Remove(assetName);

            Debug.Log($"[SurvivorAutoFireWeapon] Disposed old pool: {assetName}");
        }

        /// <summary>
        /// アセット名に対応するプールを取得または作成
        /// 同じアセット名なら既存プールを再利用
        /// </summary>
        private async UniTask<ProjectilePool> GetOrCreatePoolAsync(string assetName)
        {
            // 既存プールがあれば再利用
            if (_poolsByAssetName.TryGetValue(assetName, out var existingPool))
            {
                _currentPool = existingPool;
                return existingPool;
            }

            // 新しいプールを作成（Limitをプールサイズとして使用）
            var prefab = await _assetService.LoadAssetAsync<GameObject>(assetName);
            var pool = new ProjectilePool(prefab, _limit, _poolParent, OnProjectileHit, ReturnToPool);

            _poolsByAssetName[assetName] = pool;
            _currentPool = pool;

            return pool;
        }

        protected override bool TryAttack()
        {
            if (!_isInitialized || _currentPool == null) return false;

            // ターゲットを取得（ロックオン優先）
            if (!TryGetTarget(out var target)) return false;

            // 発射方向（ターゲットに向かって真っ直ぐ）
            Vector3 baseDirection = (target.position - _owner.position).normalized;
            baseDirection.y = 0f;

            // 全弾を発射
            for (int i = 0; i < _emitCount; i++)
            {
                // 拡散角度を適用
                Vector3 direction = ApplySpread(baseDirection, i);

                // EmitDelayがある場合は遅延発射（簡易実装：ここでは同時発射）
                FireProjectile(direction);
            }

            return true;
        }

        /// <summary>
        /// 拡散角度を適用
        /// </summary>
        private Vector3 ApplySpread(Vector3 baseDirection, int index)
        {
            if (_spread <= 0 || _emitCount <= 1) return baseDirection;

            // 弾を扇状に配置
            float totalSpread = _spread;
            float angleStep = totalSpread / (_emitCount - 1);
            float startAngle = -totalSpread / 2f;
            float angle = startAngle + (angleStep * index);

            return Quaternion.Euler(0f, angle, 0f) * baseDirection;
        }

        /// <summary>
        /// ターゲットを取得（ロックオン優先、なければ最寄りの敵）
        /// </summary>
        protected override bool TryGetTarget(out Transform target)
        {
            // まず基底クラスでロックオンチェック
            if (base.TryGetTarget(out target))
            {
                return true;
            }

            // ロックオンがなければ最寄りの敵を検索
            target = FindNearestEnemy();
            return target != null;
        }

        private Transform FindNearestEnemy()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(_owner.position, _range, _hitBuffer);

            Transform nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var enemy = _hitBuffer[i].GetComponent<SurvivorEnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    float distance = Vector3.Distance(_owner.position, enemy.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = enemy.transform;
                    }
                }
            }

            return nearest;
        }

        private void FireProjectile(Vector3 direction)
        {
            var projectile = _currentPool.Get();
            if (projectile == null) return;

            Vector3 spawnPosition = _owner.position + Vector3.up * 1f;
            projectile.transform.position = spawnPosition;
            projectile.gameObject.SetActive(true);

            // 弾の寿命を計算（Durationが0の場合はRange/MoveSpeedから算出）
            float lifetime = _duration > 0
                ? Duration
                : _range / _moveSpeed;

            // クリティカル判定
            bool isCritical = RollCritical();
            int finalDamage = isCritical ? CalculateCriticalDamage(Damage) : Damage;

            projectile.Fire(direction, _moveSpeed, finalDamage, lifetime, _hitCount, _pierce, _homing, isCritical);
        }

        private void ReturnToPool(SurvivorProjectile projectile)
        {
            projectile.gameObject.SetActive(false);
            // プールに戻す（どのプールに属しているかはProjectilePoolが管理）
            foreach (var pool in _poolsByAssetName.Values)
            {
                if (pool.TryReturn(projectile))
                {
                    return;
                }
            }
        }

        private void OnProjectileHit(SurvivorProjectile projectile, Collider other)
        {
            var enemy = other.GetComponent<SurvivorEnemyController>();
            if (enemy == null || enemy.IsDead) return;

            int enemyInstanceId = enemy.GetInstanceID();

            // ProcRateでダメージ発生判定（100%で常にダメージ）
            if (RollProcRate())
            {
                enemy.TakeDamage(projectile.Damage);

                // ヒットエフェクト生成
                if (_vfxSpawner != null && !string.IsNullOrEmpty(_hitEffectAssetName))
                {
                    var hitPosition = other.ClosestPoint(projectile.transform.position);
                    _vfxSpawner.SpawnEffect(_hitEffectAssetName, hitPosition, _hitEffectScale);
                }

                // ノックバック適用
                if (_knockback > 0)
                {
                    Vector3 knockbackDir = (enemy.transform.position - _owner.position).normalized;
                    enemy.ApplyKnockback(knockbackDir * _knockback);
                }
            }

            // ヒット/貫通チェック
            if (projectile.ProcessHit(enemyInstanceId))
            {
                ReturnToPool(projectile);
            }
        }

        public override void Dispose()
        {
            if (_isDisposed) return;

            // 全プールをクリア
            foreach (var pool in _poolsByAssetName.Values)
            {
                pool.Clear();
            }

            _poolsByAssetName.Clear();

            base.Dispose();
        }
    }

    /// <summary>
    /// プロジェクタイル用オブジェクトプール
    /// アセット名ごとに独立して管理
    /// </summary>
    internal class ProjectilePool
    {
        private readonly Queue<SurvivorProjectile> _pool = new();
        private readonly HashSet<SurvivorProjectile> _activeProjectiles = new();
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly System.Action<SurvivorProjectile, Collider> _onHit;
        private readonly System.Action<SurvivorProjectile> _onLifetimeExpired;

        /// <summary>
        /// 現在アクティブ（使用中）のプロジェクタイル数
        /// </summary>
        public int ActiveCount => _activeProjectiles.Count;

        public ProjectilePool(
            GameObject prefab,
            int initialSize,
            Transform parent,
            System.Action<SurvivorProjectile, Collider> onHit,
            System.Action<SurvivorProjectile> onLifetimeExpired)
        {
            _prefab = prefab;
            _parent = parent;
            _onHit = onHit;
            _onLifetimeExpired = onLifetimeExpired;

            // 初期プールを作成
            for (int i = 0; i < initialSize; i++)
            {
                var projectile = CreateProjectile();
                projectile.gameObject.SetActive(false);
                _pool.Enqueue(projectile);
            }
        }

        private SurvivorProjectile CreateProjectile()
        {
            var instance = Object.Instantiate(_prefab, _parent);
            var projectile = instance.GetComponent<SurvivorProjectile>();

            if (projectile == null)
            {
                projectile = instance.AddComponent<SurvivorProjectile>();
            }

            projectile.OnHit += _onHit;
            projectile.OnLifetimeExpired += _onLifetimeExpired;

            return projectile;
        }

        public SurvivorProjectile Get()
        {
            SurvivorProjectile projectile = null;

            while (_pool.Count > 0)
            {
                projectile = _pool.Dequeue();
                if (projectile != null)
                {
                    break;
                }
            }

            if (projectile == null)
            {
                projectile = CreateProjectile();
            }

            _activeProjectiles.Add(projectile);
            return projectile;
        }

        public bool TryReturn(SurvivorProjectile projectile)
        {
            if (!_activeProjectiles.Contains(projectile))
            {
                return false;
            }

            _activeProjectiles.Remove(projectile);
            _pool.Enqueue(projectile);
            return true;
        }

        public void Clear()
        {
            foreach (var projectile in _pool)
            {
                if (projectile != null)
                {
                    Object.Destroy(projectile.gameObject);
                }
            }

            _pool.Clear();

            foreach (var projectile in _activeProjectiles)
            {
                if (projectile != null)
                {
                    Object.Destroy(projectile.gameObject);
                }
            }

            _activeProjectiles.Clear();
        }
    }
}