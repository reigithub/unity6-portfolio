using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器マネージャー
    /// プレイヤーの武器をマスターデータに基づいて管理
    /// ファクトリーパターンで武器を生成
    /// </summary>
    public class SurvivorWeaponManager : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private int _maxWeaponSlots = 6;

        [Header("VFX")]
        [SerializeField] private SurvivorVfxSpawner _vfxSpawner;

        // DI
        [Inject] private IObjectResolver _resolver;
        [Inject] private IMasterDataService _masterDataService;
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private ILockOnService _lockOnService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // 装備中の武器
        private readonly List<SurvivorWeaponBase> _weapons = new();

        // State
        private Transform _owner;
        private float _damageMultiplier = 1f;

        // Events
        private readonly Subject<SurvivorWeaponBase> _onWeaponAdded = new();
        private readonly Subject<SurvivorWeaponBase> _onWeaponUpgraded = new();

        public Observable<SurvivorWeaponBase> OnWeaponAdded => _onWeaponAdded;
        public Observable<SurvivorWeaponBase> OnWeaponUpgraded => _onWeaponUpgraded;

        public IReadOnlyList<SurvivorWeaponBase> Weapons => _weapons;
        public int MaxWeaponSlots => _maxWeaponSlots;
        public bool HasEmptySlot => _weapons.Count < _maxWeaponSlots;

        /// <summary>
        /// 初期化
        /// </summary>
        public async UniTask InitializeAsync(Transform owner, int startingWeaponId, float damageMultiplier = 1f)
        {
            _owner = owner;
            _damageMultiplier = damageMultiplier;

            // 初期武器を追加
            if (startingWeaponId > 0)
            {
                await AddWeaponAsync(startingWeaponId);
            }
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
                Debug.LogError($"[SurvivorWeaponManager] Weapon master not found: {weaponId}");
                return false;
            }

            // 全レベルのマスターを取得
            var levelMasters = MemoryDatabase.SurvivorWeaponLevelMasterTable
                .FindByWeaponId(weaponId);
            if (levelMasters.Count == 0)
            {
                Debug.LogError($"[SurvivorWeaponManager] Weapon level masters not found: weaponId={weaponId}");
                return false;
            }

            // ファクトリーで武器を生成（純粋C#クラス）
            var weapon = SurvivorWeaponFactory.Create(_resolver, weaponMaster, transform);

            // マスターデータから初期化（全レベル分を渡す）
            await weapon.InitializeAsync(weaponMaster, levelMasters, _owner, _damageMultiplier, _vfxSpawner);

            _weapons.Add(weapon);
            _onWeaponAdded.OnNext(weapon);

            Debug.Log($"[SurvivorWeaponManager] Added weapon: {weaponMaster.Name} Lv.1");
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

            if (!weapon.LevelUp())
            {
                return false;
            }

            _onWeaponUpgraded.OnNext(weapon);
            Debug.Log($"[SurvivorWeaponManager] Upgraded weapon: {weaponId} to Lv.{weapon.Level}");
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
        public List<SurvivorWeaponUpgradeOption> GetUpgradeOptions(int count = 3)
        {
            var options = new List<SurvivorWeaponUpgradeOption>();

            // 既存武器のアップグレード（最大レベル未満のみ）
            foreach (var weapon in _weapons)
            {
                // 武器自身がMaxLevelを知っている
                if (weapon.Level >= weapon.MaxLevel)
                    continue;

                // 次のレベルのマスターを取得
                if (!MemoryDatabase.SurvivorWeaponLevelMasterTable.TryFindByWeaponIdAndLevel((weapon.WeaponId, weapon.Level + 1), out var nextLevelMaster))
                    continue;

                // 武器マスターを取得
                MemoryDatabase.SurvivorWeaponMasterTable.TryFindById(weapon.WeaponId, out var weaponMaster);

                options.Add(new SurvivorWeaponUpgradeOption
                {
                    WeaponId = weapon.WeaponId,
                    WeaponName = weapon.Name,
                    IsNewWeapon = false,
                    CurrentLevel = weapon.Level,
                    Description = weaponMaster?.Description,
                    UpgradeEffect = nextLevelMaster.Description,
                    IconAssetName = weaponMaster?.IconAssetName
                });
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

                    options.Add(new SurvivorWeaponUpgradeOption
                    {
                        WeaponId = weaponMaster.Id,
                        WeaponName = weaponMaster.Name,
                        IsNewWeapon = true,
                        CurrentLevel = 0,
                        Description = weaponMaster.Description,
                        UpgradeEffect = null,
                        IconAssetName = weaponMaster.IconAssetName
                    });
                }
            }

            // ランダムに選択
            var result = new List<SurvivorWeaponUpgradeOption>();
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
        public async UniTask ApplyUpgradeOptionAsync(SurvivorWeaponUpgradeOption option)
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

        /// <summary>
        /// 毎フレーム全武器を更新
        /// </summary>
        private void Update()
        {
            float deltaTime = Time.deltaTime;
            foreach (var weapon in _weapons)
            {
                weapon.UpdateWeapon(deltaTime);
            }
        }

        private void OnDestroy()
        {
            // 全武器を破棄
            foreach (var weapon in _weapons)
            {
                weapon.Dispose();
            }

            _weapons.Clear();

            _onWeaponAdded.Dispose();
            _onWeaponUpgraded.Dispose();
        }
    }

    /// <summary>
    /// 武器アップグレード選択肢
    /// </summary>
    public class SurvivorWeaponUpgradeOption
    {
        public int WeaponId { get; set; }
        public string WeaponName { get; set; }
        public bool IsNewWeapon { get; set; }
        public int CurrentLevel { get; set; }
        /// <summary>武器の基本説明</summary>
        public string Description { get; set; }
        /// <summary>レベルアップ時の追加性能テキスト</summary>
        public string UpgradeEffect { get; set; }
        /// <summary>アイコンアセット名</summary>
        public string IconAssetName { get; set; }
    }
}