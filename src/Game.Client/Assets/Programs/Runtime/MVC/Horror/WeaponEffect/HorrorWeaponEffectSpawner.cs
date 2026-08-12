using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.WeaponEffect
{
    /// <summary>
    /// 武器効果（投擲物と、そこから展開される効果フィールド）のスポナー。
    /// 投擲物プレハブの Addressables ロード/解放・生成・生成物追跡・シーン終了時の破棄を一元的に担い、
    /// アクティブな効果集合（<see cref="HorrorWeaponEffectRegistry"/>）を所有する。
    /// ステージシーンが生成し、Terminate で <see cref="Dispose"/> する（HorrorEnemySpawner と同じ寿命規約）。
    /// </summary>
    public class HorrorWeaponEffectSpawner
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IScriptableDatabaseService _dbService;

        // WeaponMasterId → ロード済み投擲物プレハブ（Dispose で Release）
        private readonly Dictionary<int, GameObject> _projectilePrefabs = new();

        // 生成した投擲物・効果フィールド。自然消滅（起爆・持続終了）した要素は null になるため Dispose 時に無視する
        private readonly List<GameObject> _spawnedObjects = new();

        /// <summary>アクティブな武器効果の集合（読み手は敵知覚）</summary>
        public HorrorWeaponEffectRegistry Registry { get; } = new();

        public HorrorWeaponEffectSpawner(IAddressableAssetService assetService, IScriptableDatabaseService dbService)
        {
            _assetService = assetService;
            _dbService = dbService;
        }

        /// <summary>
        /// 全 Throwable 武器の投擲物プレハブを事前ロードする。シーン起動時に1回呼ぶ
        /// （投擲は同期処理のため、装備経路を問わず投擲時点でロード済みであることを保証する）。
        /// </summary>
        public async UniTask InitializeAsync()
        {
            foreach (var weapon in _dbService.Database.HorrorWeaponMasterTable.All)
            {
                if (weapon.WeaponType != HorrorWeaponType.Throwable) continue;
                if (string.IsNullOrEmpty(weapon.ProjectileAssetName)) continue; // バリデータで編集時検出済み。ここは防御
                if (_projectilePrefabs.ContainsKey(weapon.Id)) continue;

                var prefab = await _assetService.LoadAssetAsync<GameObject>(weapon.ProjectileAssetName);
                if (prefab == null)
                {
                    Debug.LogError($"[{nameof(HorrorWeaponEffectSpawner)}] 投擲物プレハブのロードに失敗しました (WeaponId={weapon.Id}, Asset={weapon.ProjectileAssetName})");
                    continue;
                }

                _projectilePrefabs[weapon.Id] = prefab;
            }
        }

        /// <summary>指定武器の投擲物を生成できるか（プレハブがロード済みか）。</summary>
        public bool CanSpawnProjectile(HorrorWeaponMaster master)
            => master != null && _projectilePrefabs.ContainsKey(master.Id);

        /// <summary>
        /// 投擲物を生成して射出する。プレハブ未ロードは不変条件違反（呼び出し側が
        /// <see cref="CanSpawnProjectile"/> で検証済みのはず）として LogError で顕在化する。
        /// </summary>
        /// <param name="master">射出元武器のマスター</param>
        /// <param name="origin">射出位置（ワールド座標）</param>
        /// <param name="velocity">初速ベクトル</param>
        /// <param name="ignoreColliders">衝突を無効化する射手側コライダー</param>
        public void SpawnProjectile(HorrorWeaponMaster master, Vector3 origin, Vector3 velocity, Collider[] ignoreColliders)
        {
            if (!_projectilePrefabs.TryGetValue(master.Id, out var prefab))
            {
                Debug.LogError($"[{nameof(HorrorWeaponEffectSpawner)}] 投擲物プレハブが未ロードです (WeaponId={master.Id})");
                return;
            }

            var instance = Object.Instantiate(prefab, origin, prefab.transform.rotation);
            if (!instance.TryGetComponent<HorrorProjectile>(out var projectile))
            {
                Debug.LogError($"[{nameof(HorrorWeaponEffectSpawner)}] 投擲物プレハブに {nameof(HorrorProjectile)} がありません (WeaponId={master.Id})");
                instance.SafeDestroy();
                return;
            }

            projectile.Launch(velocity, master, this, ignoreColliders);
            _spawnedObjects.Add(instance);
        }

        /// <summary>
        /// 効果フィールドを生成して追跡する（ペイロード生成の追跡付き単一入口）。
        /// プレハブの回転を維持して生成する（GroundFog の焼き込み回転を潰さない）。
        /// 初期化（Initialize 等）は呼び出し側が型付きで行う。
        /// </summary>
        public TEffect SpawnEffect<TEffect>(TEffect prefab, Vector3 position) where TEffect : Component
        {
            var instance = Object.Instantiate(prefab, position, prefab.transform.rotation);
            _spawnedObjects.Add(instance.gameObject);
            return instance;
        }

        /// <summary>
        /// 残存する生成物（飛翔中の投擲物・持続中の効果フィールド）を破棄し、プレハブハンドルを解放する。
        /// ステージシーンのアンロード前（HorrorStageScene.Terminate）に呼ぶ。
        /// </summary>
        public void Dispose()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                    obj.SafeDestroy();
            }
            _spawnedObjects.Clear();

            foreach (var prefab in _projectilePrefabs.Values)
                _assetService.Release(prefab);
            _projectilePrefabs.Clear();
        }
    }
}
