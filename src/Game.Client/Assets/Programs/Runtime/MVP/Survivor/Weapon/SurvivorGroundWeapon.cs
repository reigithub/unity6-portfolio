using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Combat;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 地面設置型武器
    /// ターゲット位置を中心に円形パターンでダメージエリアを生成
    /// 手動発動型（Cooldown > 0）
    /// </summary>
    public class SurvivorGroundWeapon : SurvivorWeaponBase
    {
        private const float PoolDisposeTimeout = 10f;           // プール破棄タイムアウト（秒）
        private const int PoolDisposeCheckInterval = 100;       // プール破棄チェック間隔（ミリ秒）
        private const float AreaSpawnRadiusRatio = 0.3f;        // 発動範囲の半径（射程に対する比率）
        private const float BaseHitboxRadius = 1f;              // ヒットボックス基本半径

        // アセット名ごとのプールを管理
        private readonly Dictionary<string, WeaponObjectPool<SurvivorGroundDamageArea>> _poolsByAssetName = new();
        // ロードしたプレハブを追跡（Dispose時にリリース用）
        private readonly Dictionary<string, GameObject> _loadedPrefabs = new();
        private WeaponObjectPool<SurvivorGroundDamageArea> _currentPool;
        private bool _isInitialized;

        // 発動時の中心位置（TryAttackで使用）
        private Vector3 _attackCenter;

        public SurvivorGroundWeapon(SurvivorWeaponMaster weaponMaster) : base(weaponMaster)
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
            SwitchPoolAsync(oldAssetName, newAssetName).Forget();
        }

        private async UniTask SwitchPoolAsync(string oldAssetName, string newAssetName)
        {
            try
            {
                _currentPool = await GetOrCreatePoolAsync(newAssetName);

                if (!string.IsNullOrEmpty(oldAssetName) && _poolsByAssetName.TryGetValue(oldAssetName, out var oldPool))
                {
                    DisposeOldPoolAsync(oldAssetName, oldPool).Forget();
                }

                Debug.Log($"[SurvivorGroundWeapon] Switched to pool: {newAssetName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorGroundWeapon] SwitchPoolAsync failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async UniTask DisposeOldPoolAsync(string assetName, WeaponObjectPool<SurvivorGroundDamageArea> pool)
        {
            try
            {
                float elapsed = 0f;
                float checkIntervalSec = PoolDisposeCheckInterval / 1000f;

                while (pool.ActiveCount > 0 && elapsed < PoolDisposeTimeout)
                {
                    await UniTask.Delay(PoolDisposeCheckInterval);
                    elapsed += checkIntervalSec;
                }

                pool.Clear();
                _poolsByAssetName.Remove(assetName);

                // ロードしたプレハブをリリース
                if (_loadedPrefabs.TryGetValue(assetName, out var prefab))
                {
                    AssetService.ReleaseAsset(prefab);
                    _loadedPrefabs.Remove(assetName);
                }

                Debug.Log($"[SurvivorGroundWeapon] Disposed old pool: {assetName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorGroundWeapon] DisposeOldPoolAsync failed for {assetName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async UniTask<WeaponObjectPool<SurvivorGroundDamageArea>> GetOrCreatePoolAsync(string assetName)
        {
            if (_poolsByAssetName.TryGetValue(assetName, out var existingPool))
            {
                _currentPool = existingPool;
                return existingPool;
            }

            var prefab = await AssetService.LoadAssetAsync<GameObject>(assetName);
            _loadedPrefabs[assetName] = prefab; // リリース用に追跡

            var pool = new WeaponObjectPool<SurvivorGroundDamageArea>(
                prefab,
                _limit,
                _poolParent,
                area =>
                {
                    area.OnHit += OnAreaHit;
                    area.OnExpired += OnAreaExpired;
                });

            _poolsByAssetName[assetName] = pool;
            _currentPool = pool;

            return pool;
        }

        /// <summary>
        /// ターゲットが射程内かチェック
        /// ターゲットがいない場合はtrue（プレイヤー位置にフォールバック）
        /// </summary>
        protected override bool IsTargetInRange()
        {
            // ロックオンターゲットを取得
            if (!LockOnService.TryGetTarget(out var target))
            {
                // ターゲットなし → プレイヤー位置にフォールバック
                _attackCenter = _owner.position;
                return true;
            }

            // 距離チェック（sqrMagnitudeで高速化）
            float sqrDistance = (_owner.position - target.position).sqrMagnitude;
            if (sqrDistance > _range * _range)
            {
                // 射程外
                return false;
            }

            // 射程内 → ターゲット位置を中心に
            _attackCenter = target.position;
            return true;
        }

        protected override bool TryAttack()
        {
            if (!_isInitialized || _currentPool == null) return false;

            // IsTargetInRangeで_attackCenterが設定済み
            Vector3 center = _attackCenter;
            float angleStep = 360f / _emitCount;
            float radius = _range * AreaSpawnRadiusRatio;

            for (int i = 0; i < _emitCount; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                Vector3 spawnPos = center + offset;

                // EmitDelayがある場合は遅延スポーン
                if (_emitDelay > 0)
                {
                    SpawnAreaWithDelayAsync(spawnPos, i * EmitDelay).Forget();
                }
                else
                {
                    SpawnArea(spawnPos);
                }
            }

            return true;
        }

        private async UniTaskVoid SpawnAreaWithDelayAsync(Vector3 position, float delay)
        {
            try
            {
                await UniTask.Delay((int)(delay * 1000));
                SpawnArea(position);
            }
            catch (OperationCanceledException)
            {
                // 正常なキャンセル（オブジェクト破棄時など）
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorGroundWeapon] SpawnAreaWithDelayAsync failed: {ex.Message}");
            }
        }

        private void SpawnArea(Vector3 position)
        {
            var area = _currentPool.Get();
            if (area == null) return;

            area.transform.position = position;
            area.gameObject.SetActive(true);

            // クリティカル判定
            bool isCritical = RollCritical();
            int finalDamage = isCritical ? CalculateCriticalDamage(Damage) : Damage;

            // ヒットボックスサイズ（HitBoxRateで調整）
            float hitboxRadius = BaseHitboxRadius * (_hitBoxRate / 10000f);

            area.Activate(finalDamage, Duration, Interval, _knockback, hitboxRadius);
        }

        private void OnAreaHit(SurvivorGroundDamageArea area, Collider other)
        {
            // メッシュコライダーが子オブジェクトにある場合に対応
            var target = other.GetComponentInParent<ICombatTarget>();
            if (target == null || target.IsDead) return;

            if (RollProcRate())
            {
                target.TakeDamage(area.Damage);

                // ヒットエフェクト
                if (_vfxSpawner != null && !string.IsNullOrEmpty(_hitEffectAssetName))
                {
                    var hitPos = other.ClosestPoint(area.transform.position);
                    _vfxSpawner.SpawnEffect(_hitEffectAssetName, hitPos, _hitEffectScale);
                }

                // ノックバック
                if (area.Knockback > 0)
                {
                    Vector3 dir = (other.transform.position - area.transform.position).normalized;
                    target.ApplyKnockback(dir * area.Knockback);
                }
            }
        }

        private void OnAreaExpired(SurvivorGroundDamageArea area)
        {
            area.gameObject.SetActive(false);
            _currentPool?.Return(area);
        }

        public override void Dispose()
        {
            if (_isDisposed) return;

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