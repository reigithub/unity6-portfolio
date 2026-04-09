using Game.Client.MasterData;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Combat;
using Unity.Profiling;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 自動発射武器
    /// 最も近い敵に向かって自動的に弾を発射
    /// </summary>
    public class SurvivorAutoFireWeapon : SurvivorWeaponBase<SurvivorProjectile>
    {
        // Profiler markers
        private static readonly ProfilerMarker s_fireMarker = new("ProfilerMarker.Weapon.Fire");
        private static readonly ProfilerMarker s_findTargetMarker = new("ProfilerMarker.Weapon.FindTarget");
        private static readonly ProfilerMarker s_processHitMarker = new("ProfilerMarker.Weapon.ProcessHit");
        private static readonly ProfilerMarker s_spawnProjectileMarker = new("ProfilerMarker.Weapon.SpawnProjectile");

        private const float ProjectileSpawnHeight = 1f;         // 弾の発射高さオフセット
        private const int NearbyEnemySearchBufferSize = 50;     // 近くの敵検索バッファサイズ

        // Cache
        private readonly Collider[] _hitBuffer = new Collider[NearbyEnemySearchBufferSize];

        public SurvivorAutoFireWeapon(SurvivorWeaponMaster weaponMaster) : base(weaponMaster)
        {
        }

        protected override void InitializePoolItem(SurvivorProjectile projectile)
        {
            projectile.Initialize(GameState);
            projectile.OnHit += OnProjectileHit;
            projectile.OnLifetimeExpired += ReturnToPool;
        }

        protected override bool TryAttack()
        {
            using (s_fireMarker.Auto())
            {
                if (!IsPoolInitialized || CurrentPool == null) return false;

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
            using (s_findTargetMarker.Auto())
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
        }

        private void FireProjectile(Vector3 direction)
        {
            using (s_spawnProjectileMarker.Auto())
            {
                var projectile = CurrentPool.Get();
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
        }

        private void ReturnToPool(SurvivorProjectile projectile)
        {
            projectile.gameObject.SetActive(false);
            TryReturnToAnyPool(projectile);
        }

        /// <summary>
        /// プロジェクタイル命中処理（SP/MP統一）
        /// ヒット検出とVFX表示を行い、ダメージ処理はScene側のコールバックに委譲する。
        /// </summary>
        private void OnProjectileHit(SurvivorProjectile projectile, Collider other)
        {
            using (s_processHitMarker.Auto())
            {
                // プライマリヒット処理済み → 後続のOnTriggerEnterを無視
                if (projectile.HasPrimaryHitProcessed) return;

                // ヒット対象チェック（SP: ICombatTarget, MP: EnemyProxyTarget）
                if (other.GetComponentInParent<ICombatTarget>() == null
                    && other.GetComponentInParent<EnemyProxyTarget>() == null)
                    return;

                // 射程外のヒットはスキップ（チート対策 + 不要な VFX 防止）
                // 1.2x マージンでフレーム間移動のオーバーシュートを吸収する
                if (_owner != null)
                {
                    float sqrDistance = (_owner.position - projectile.transform.position).sqrMagnitude;
                    if (sqrDistance > _range * _range * 1.2f)
                        return;
                }

                projectile.MarkPrimaryHitProcessed();

                // ヒットVFX（ダメージ計算前に表示 — ProcRate失敗でもプロジェクタイルは当たった）
                if (_vfxSpawner != null && !string.IsNullOrEmpty(_hitEffectAssetName))
                {
                    var hitPosition = other.ClosestPoint(projectile.transform.position);
                    _vfxSpawner.SpawnEffect(_hitEffectAssetName, hitPosition, _hitEffectScale);
                }

                // ダメージ処理をSceneに委譲（SP: ローカルダメージ, MP: RPC送信）
                OnHitCallback?.Invoke(other, WeaponId);

                ReturnToPool(projectile);
            }
        }
    }
}
