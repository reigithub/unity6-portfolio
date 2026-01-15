using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Core.Services;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器マネージャー
    /// プレイヤーの武器をマスターデータに基づいて管理
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private int _maxWeaponSlots = 6;

        // DI
        [Inject] private IMasterDataService _masterDataService;
        [Inject] private IAddressableAssetService _assetService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // 装備中の武器
        private readonly List<WeaponBase> _weapons = new();
        private readonly Dictionary<int, GameObject> _weaponPrefabs = new();

        // State
        private Transform _owner;
        private float _damageMultiplier = 1f;

        // Events
        private readonly Subject<WeaponBase> _onWeaponAdded = new();
        private readonly Subject<WeaponBase> _onWeaponUpgraded = new();

        public Observable<WeaponBase> OnWeaponAdded => _onWeaponAdded;
        public Observable<WeaponBase> OnWeaponUpgraded => _onWeaponUpgraded;

        public IReadOnlyList<WeaponBase> Weapons => _weapons;
        public int MaxWeaponSlots => _maxWeaponSlots;
        public bool HasEmptySlot => _weapons.Count < _maxWeaponSlots;

        /// <summary>
        /// 初期化
        /// </summary>
        public async UniTask InitializeAsync(Transform owner, int startingWeaponId, float damageMultiplier = 1f)
        {
            _owner = owner;
            _damageMultiplier = damageMultiplier;

            // 全武器プレハブを事前読み込み
            var allWeapons = MemoryDatabase.SurvivorWeaponMasterTable.All;
            foreach (var weaponMaster in allWeapons)
            {
                if (!_weaponPrefabs.ContainsKey(weaponMaster.Id) && !string.IsNullOrEmpty(weaponMaster.AssetName))
                {
                    var prefab = await _assetService.LoadAssetAsync<GameObject>(weaponMaster.AssetName);
                    _weaponPrefabs[weaponMaster.Id] = prefab;
                }
            }

            // 初期武器を追加
            if (startingWeaponId > 0)
            {
                await AddWeaponAsync(startingWeaponId);
            }
        }

        /// <summary>
        /// 従来の初期化メソッド（後方互換用）
        /// </summary>
        public void Initialize(Transform owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 武器を追加（マスターデータ版）
        /// </summary>
        public async UniTask<bool> AddWeaponAsync(int weaponId)
        {
            // 既に持っている場合はアップグレード
            var existing = _weapons.Find(w => w.WeaponId == weaponId);
            if (existing != null)
            {
                return UpgradeWeapon(weaponId);
            }

            // スロットが空いていない場合
            if (!HasEmptySlot)
            {
                return false;
            }

            // マスターデータ取得
            if (!MemoryDatabase.SurvivorWeaponMasterTable.TryFindById(weaponId, out var weaponMaster))
            {
                Debug.LogError($"[WeaponManager] Weapon master not found: {weaponId}");
                return false;
            }

            // レベル1のステータスを取得
            var levelMaster = MemoryDatabase.SurvivorWeaponLevelMasterTable
                .FindByWeaponId(weaponId)
                .FirstOrDefault(l => l.Level == 1);

            if (levelMaster == null)
            {
                Debug.LogError($"[WeaponManager] Weapon level master not found: weaponId={weaponId}, level=1");
                return false;
            }

            // 武器を生成
            WeaponBase weapon;
            if (_weaponPrefabs.TryGetValue(weaponId, out var prefab) && prefab != null)
            {
                var weaponObj = Instantiate(prefab, transform);
                weapon = weaponObj.GetComponent<WeaponBase>();
            }
            else
            {
                // プレハブがない場合はAutoFireWeaponをデフォルトで生成
                var weaponObj = new GameObject(weaponMaster.Name);
                weaponObj.transform.SetParent(transform);
                weapon = weaponObj.AddComponent<AutoFireWeapon>();
            }

            // マスターデータから初期化
            await weapon.Initialize(weaponMaster, levelMaster, _owner, _damageMultiplier);

            _weapons.Add(weapon);
            _onWeaponAdded.OnNext(weapon);

            Debug.Log($"[WeaponManager] Added weapon: {weaponMaster.Name} Lv.1");
            return true;
        }

        /// <summary>
        /// 武器をアップグレード
        /// </summary>
        public bool UpgradeWeapon(int weaponId)
        {
            var weapon = _weapons.Find(w => w.WeaponId == weaponId);
            if (weapon == null)
            {
                return false;
            }

            int nextLevel = weapon.Level + 1;

            // 次のレベルのマスターデータを取得
            var levelMaster = MemoryDatabase.SurvivorWeaponLevelMasterTable
                .FindByWeaponId(weaponId)
                .FirstOrDefault(l => l.Level == nextLevel);

            if (levelMaster == null)
            {
                Debug.LogWarning($"[WeaponManager] Already max level: weaponId={weaponId}, level={weapon.Level}");
                return false;
            }

            weapon.LevelUp(levelMaster);
            _onWeaponUpgraded.OnNext(weapon);

            Debug.Log($"[WeaponManager] Upgraded weapon: {weaponId} to Lv.{nextLevel}");
            return true;
        }

        /// <summary>
        /// ダメージ倍率を更新
        /// </summary>
        public void UpdateDamageMultiplier(float multiplier)
        {
            _damageMultiplier = multiplier;
            foreach (var weapon in _weapons)
            {
                weapon.SetDamageMultiplier(multiplier);
            }
        }

        /// <summary>
        /// 全武器を有効/無効化
        /// </summary>
        public void SetAllWeaponsEnabled(bool enabled)
        {
            foreach (var weapon in _weapons)
            {
                weapon.SetEnabled(enabled);
            }
        }

        /// <summary>
        /// レベルアップ時の選択肢を取得（マスターデータ版）
        /// </summary>
        public List<WeaponUpgradeOption> GetUpgradeOptions(int count = 3)
        {
            var options = new List<WeaponUpgradeOption>();

            // 既存武器のアップグレード（最大レベル未満のみ）
            foreach (var weapon in _weapons)
            {
                if (!MemoryDatabase.SurvivorWeaponMasterTable.TryFindById(weapon.WeaponId, out var weaponMaster))
                    continue;

                if (weapon.Level < weaponMaster.MaxLevel)
                {
                    options.Add(new WeaponUpgradeOption
                    {
                        WeaponId = weapon.WeaponId,
                        WeaponName = weaponMaster.Name,
                        IsNewWeapon = false,
                        CurrentLevel = weapon.Level,
                        Description = $"{weaponMaster.Name} Lv.{weapon.Level} → Lv.{weapon.Level + 1}"
                    });
                }
            }

            // 新規武器（空きスロットがある場合）
            if (HasEmptySlot)
            {
                var allWeapons = MemoryDatabase.SurvivorWeaponMasterTable.All;
                foreach (var weaponMaster in allWeapons)
                {
                    // まだ持っていない武器のみ
                    if (_weapons.Any(w => w.WeaponId == weaponMaster.Id))
                        continue;

                    options.Add(new WeaponUpgradeOption
                    {
                        WeaponId = weaponMaster.Id,
                        WeaponName = weaponMaster.Name,
                        IsNewWeapon = true,
                        CurrentLevel = 0,
                        Description = $"New: {weaponMaster.Name}"
                    });
                }
            }

            // ランダムに選択
            var result = new List<WeaponUpgradeOption>();
            while (result.Count < count && options.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, options.Count);
                result.Add(options[index]);
                options.RemoveAt(index);
            }

            return result;
        }

        /// <summary>
        /// 選択結果を適用
        /// </summary>
        public async UniTask ApplyUpgradeOptionAsync(WeaponUpgradeOption option)
        {
            if (option.IsNewWeapon)
            {
                await AddWeaponAsync(option.WeaponId);
            }
            else
            {
                UpgradeWeapon(option.WeaponId);
            }
        }

        private void OnDestroy()
        {
            _onWeaponAdded.Dispose();
            _onWeaponUpgraded.Dispose();
        }
    }

    /// <summary>
    /// 武器アップグレード選択肢
    /// </summary>
    public class WeaponUpgradeOption
    {
        public int WeaponId { get; set; }
        public string WeaponName { get; set; }
        public bool IsNewWeapon { get; set; }
        public int CurrentLevel { get; set; }
        public string Description { get; set; }
    }
}