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
    /// 自動発射武器
    /// 最も近い敵に向かって自動的に弾を発射
    /// マスターデータ駆動
    /// </summary>
    public class AutoFireWeapon : WeaponBase
    {
        [Header("Projectile Settings")]
        [SerializeField] private string _projectileAssetAddress = "SurvivorProjectile";

        [SerializeField] private float _spreadAngle = 15f;

        [Header("Pool")]
        [SerializeField] private int _poolSize = 20;

        // DI
        [Inject] private IAddressableAssetService _assetService;

        // Pool
        private readonly Queue<SurvivorProjectile> _projectilePool = new();
        private GameObject _projectilePrefab;
        private bool _isInitialized;

        // Cache
        private readonly Collider[] _hitBuffer = new Collider[50];

        public override async UniTask Initialize(
            SurvivorWeaponMaster weaponMaster,
            SurvivorWeaponLevelMaster levelMaster,
            Transform owner,
            float damageMultiplier = 1f)
        {
            await base.Initialize(weaponMaster, levelMaster, owner, damageMultiplier);

            // アセット名をマスターデータから取得
            if (!string.IsNullOrEmpty(weaponMaster.AssetName))
            {
                _projectileAssetAddress = weaponMaster.AssetName;
            }

            await InitializePoolAsync();
        }

        private async UniTask InitializePoolAsync()
        {
            if (_isInitialized) return;

            // IAddressableAssetService経由でプロジェクタイル読み込み
            _projectilePrefab = await _assetService.LoadAssetAsync<GameObject>(_projectileAssetAddress);

            // プール初期化
            for (int i = 0; i < _poolSize; i++)
            {
                var projectile = CreateProjectile();
                projectile.gameObject.SetActive(false);
                _projectilePool.Enqueue(projectile);
            }

            _isInitialized = true;
        }

        private SurvivorProjectile CreateProjectile()
        {
            var instance = Instantiate(_projectilePrefab, transform);
            var projectile = instance.GetComponent<SurvivorProjectile>();

            if (projectile == null)
            {
                projectile = instance.AddComponent<SurvivorProjectile>();
            }

            projectile.OnHit += OnProjectileHit;
            projectile.OnLifetimeExpired += ReturnToPool;

            return projectile;
        }

        protected override bool TryAttack()
        {
            if (!_isInitialized) return false;

            // 範囲内の敵を検索
            var target = FindNearestEnemy();
            if (target == null) return false;

            // 発射方向
            Vector3 direction = (target.position - _owner.position).normalized;
            direction.y = 0f;

            // 複数弾の場合は扇状に発射
            float startAngle = -_spreadAngle * (_projectileCount - 1) / 2f;

            for (int i = 0; i < _projectileCount; i++)
            {
                float angle = startAngle + _spreadAngle * i;
                Vector3 rotatedDirection = Quaternion.Euler(0f, angle, 0f) * direction;

                FireProjectile(rotatedDirection);
            }

            return true;
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
            var projectile = GetFromPool();
            if (projectile == null)
            {
                projectile = CreateProjectile();
            }

            Vector3 spawnPosition = _owner.position + Vector3.up * 1f;
            projectile.transform.position = spawnPosition;
            projectile.gameObject.SetActive(true);

            float lifetime = _range / _projectileSpeed;
            projectile.Fire(direction, _projectileSpeed, Damage, lifetime, _pierce);
        }

        private SurvivorProjectile GetFromPool()
        {
            while (_projectilePool.Count > 0)
            {
                var projectile = _projectilePool.Dequeue();
                if (projectile != null)
                {
                    return projectile;
                }
            }

            return null;
        }

        private void ReturnToPool(SurvivorProjectile projectile)
        {
            projectile.gameObject.SetActive(false);
            _projectilePool.Enqueue(projectile);
        }

        private void OnProjectileHit(SurvivorProjectile projectile, Collider other)
        {
            var enemy = other.GetComponent<SurvivorEnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(projectile.Damage);
            }

            // 貫通数チェック
            if (projectile.DecrementPierce())
            {
                ReturnToPool(projectile);
            }
        }
    }
}