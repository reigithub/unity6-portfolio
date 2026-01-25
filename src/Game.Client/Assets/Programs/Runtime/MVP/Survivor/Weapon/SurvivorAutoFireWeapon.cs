using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Combat;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 自動発射武器
    /// 最も近い敵に向かって自動的に弾を発射
    /// </summary>
    public class SurvivorAutoFireWeapon : SurvivorWeaponBase
    {
        private const float ProjectileSpawnHeight = 1f;         // 弾の発射高さオフセット
        private const float PoolDisposeTimeout = 10f;           // プール破棄タイムアウト（秒）
        private const int PoolDisposeCheckInterval = 100;       // プール破棄チェック間隔（ミリ秒）
        private const int NearbyEnemySearchBufferSize = 50;     // 近くの敵検索バッファサイズ

        // アセット名ごとのプールを管理（レベルアップ時の再利用のため）
        private readonly Dictionary<string, WeaponObjectPool<SurvivorProjectile>> _poolsByAssetName = new();
        // ロードしたプレハブを追跡（Dispose時にリリース用）
        private readonly Dictionary<string, GameObject> _loadedPrefabs = new();
        private WeaponObjectPool<SurvivorProjectile> _currentPool;
        private bool _isInitialized;

        // Cache
        private readonly Collider[] _hitBuffer = new Collider[NearbyEnemySearchBufferSize];

        public SurvivorAutoFireWeapon(SurvivorWeaponMaster weaponMaster) : base(weaponMaster)
        {
        }

        public override async UniTask InitializeAsync(
            Transform poolParent,
            Transform owner,
            float damageMultiplier,
            SurvivorVfxSpawner vfxSpawner)
        {
            await base.InitializeAsync(poolParent, owner, damageMultiplier, vfxSpawner);

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
            try
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
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorAutoFireWeapon] SwitchPoolAsync failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 古いプールをアクティブなプロジェクタイルがなくなってから破棄
        /// </summary>
        private async UniTask DisposeOldPoolAsync(string assetName, WeaponObjectPool<SurvivorProjectile> pool)
        {
            try
            {
                // アクティブなプロジェクタイルが全て戻るまで待機
                float elapsed = 0f;
                float checkIntervalSec = PoolDisposeCheckInterval / 1000f;

                while (pool.ActiveCount > 0 && elapsed < PoolDisposeTimeout)
                {
                    await UniTask.Delay(PoolDisposeCheckInterval);
                    elapsed += checkIntervalSec;
                }

                // プールを破棄
                pool.Clear();
                _poolsByAssetName.Remove(assetName);

                // ロードしたプレハブをリリース
                if (_loadedPrefabs.TryGetValue(assetName, out var prefab))
                {
                    AssetService.ReleaseAsset(prefab);
                    _loadedPrefabs.Remove(assetName);
                }

                Debug.Log($"[SurvivorAutoFireWeapon] Disposed old pool: {assetName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorAutoFireWeapon] DisposeOldPoolAsync failed for {assetName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// アセット名に対応するプールを取得または作成
        /// 同じアセット名なら既存プールを再利用
        /// </summary>
        private async UniTask<WeaponObjectPool<SurvivorProjectile>> GetOrCreatePoolAsync(string assetName)
        {
            // 既存プールがあれば再利用
            if (_poolsByAssetName.TryGetValue(assetName, out var existingPool))
            {
                _currentPool = existingPool;
                return existingPool;
            }

            // 新しいプールを作成（Limitをプールサイズとして使用）
            var prefab = await AssetService.LoadAssetAsync<GameObject>(assetName);
            _loadedPrefabs[assetName] = prefab; // リリース用に追跡

            var pool = new WeaponObjectPool<SurvivorProjectile>(
                prefab,
                _limit,
                _poolParent,
                projectile =>
                {
                    projectile.OnHit += OnProjectileHit;
                    projectile.OnLifetimeExpired += ReturnToPool;
                });

            _poolsByAssetName[assetName] = pool;
            _currentPool = pool;

            return pool;
        }

        protected override bool TryAttack()
        {
            if (!_isInitialized || _currentPool == null) return false;

            // ターゲットを取得（ロックオン優先）
            if (!TryGetTarget(out var target)) return false;

            // ICombatTargetからCenterPositionを取得
            var combatTarget = target.GetComponentInParent<ICombatTarget>();
            Vector3 targetCenter = combatTarget?.CenterPosition ?? target.position;

            // 発射位置と発射方向（ターゲットの中心に向かって）
            Vector3 spawnPosition = _owner.position + Vector3.up * ProjectileSpawnHeight;
            Vector3 baseDirection = (targetCenter - spawnPosition).normalized;

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
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                // メッシュコライダーが子オブジェクトにある場合に対応
                var target = _hitBuffer[i].GetComponentInParent<ICombatTarget>();
                if (target != null && !target.IsDead)
                {
                    // CenterPositionを使用して距離計算（sqrMagnitudeで高速化）
                    float sqrDistance = (_owner.position - target.CenterPosition).sqrMagnitude;
                    if (sqrDistance < nearestSqrDistance)
                    {
                        nearestSqrDistance = sqrDistance;
                        nearest = (target as MonoBehaviour)?.transform ?? _hitBuffer[i].transform;
                    }
                }
            }

            return nearest;
        }

        private void FireProjectile(Vector3 direction)
        {
            var projectile = _currentPool.Get();
            if (projectile == null) return;

            Vector3 spawnPosition = _owner.position + Vector3.up * ProjectileSpawnHeight;
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
            // メッシュコライダーが子オブジェクトにある場合に対応
            var target = other.GetComponentInParent<ICombatTarget>();
            if (target == null || target.IsDead) return;

            // MonoBehaviourとしてのインスタンスIDを取得（ヒットカウント用）
            int targetInstanceId = (target as MonoBehaviour)?.GetInstanceID() ?? other.GetInstanceID();

            // ProcRateでダメージ発生判定（100%で常にダメージ）
            if (RollProcRate())
            {
                target.TakeDamage(projectile.Damage);

                // ヒットエフェクト生成
                if (_vfxSpawner != null && !string.IsNullOrEmpty(_hitEffectAssetName))
                {
                    var hitPosition = other.ClosestPoint(projectile.transform.position);
                    _vfxSpawner.SpawnEffect(_hitEffectAssetName, hitPosition, _hitEffectScale);
                }

                // ノックバック適用
                if (_knockback > 0)
                {
                    Vector3 knockbackDir = (other.transform.position - _owner.position).normalized;
                    target.ApplyKnockback(knockbackDir * _knockback);
                }
            }

            // ヒット/貫通チェック
            if (projectile.ProcessHit(targetInstanceId))
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

            // ロードしたプレハブをリリース
            foreach (var prefab in _loadedPrefabs.Values)
            {
                AssetService.ReleaseAsset(prefab);
            }
            _loadedPrefabs.Clear();

            base.Dispose();
        }
    }
}