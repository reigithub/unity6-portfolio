using System;
using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Combat;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 地面設置型武器
    /// ターゲット位置を中心に円形パターンでダメージエリアを生成
    /// 手動発動型（Cooldown > 0）
    /// </summary>
    public class SurvivorGroundWeapon : SurvivorWeaponBase<SurvivorGroundDamageArea>
    {
        private const float AreaSpawnRadiusRatio = 0.3f;        // 発動範囲の半径（射程に対する比率）
        private const float BaseHitboxRadius = 1f;              // ヒットボックス基本半径

        // 発動時の中心位置（TryAttackで使用）
        private Vector3 _attackCenter;

        public SurvivorGroundWeapon(SurvivorWeaponMaster weaponMaster) : base(weaponMaster)
        {
        }

        protected override void InitializePoolItem(SurvivorGroundDamageArea area)
        {
            area.Initialize(GameState);
            area.OnHit += OnAreaHit;
            area.OnExpired += OnAreaExpired;
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
            if (!IsPoolInitialized || CurrentPool == null) return false;

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
            var area = CurrentPool.Get();
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

        /// <summary>
        /// エリアダメージ命中処理（SP/MP統一）
        /// VFX表示を行い、ダメージ処理はScene側のコールバックに委譲する。
        /// </summary>
        private void OnAreaHit(SurvivorGroundDamageArea area, Collider other)
        {
            // ヒット対象チェック（SP: ICombatTarget, MP: EnemyProxyTarget）
            if (other.GetComponentInParent<ICombatTarget>() == null
                && other.GetComponentInParent<EnemyProxyTarget>() == null)
                return;

            // ヒットVFX
            if (_vfxSpawner != null && !string.IsNullOrEmpty(_hitEffectAssetName))
            {
                var hitPos = other.ClosestPoint(area.transform.position);
                _vfxSpawner.SpawnEffect(_hitEffectAssetName, hitPos, _hitEffectScale);
            }

            // ダメージ処理をSceneに委譲（ProcRate/Crit計算はSurvivorNetworkWeaponManagerが行う）
            OnHitCallback?.Invoke(other, WeaponId);
        }

        private void OnAreaExpired(SurvivorGroundDamageArea area)
        {
            area.gameObject.SetActive(false);
            CurrentPool?.Return(area);
        }
    }
}
