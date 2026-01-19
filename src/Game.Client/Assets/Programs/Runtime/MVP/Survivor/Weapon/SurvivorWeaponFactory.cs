using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 武器タイプ
    /// SurvivorWeaponMaster.WeaponTypeIdに対応
    /// </summary>
    public enum SurvivorWeaponType
    {
        None = 0,
        AutoFire = 1,  // 自動発射型
        Homing = 2,    // 追尾型
        Piercing = 3,  // 貫通型
        Explosive = 4, // 爆発型
        Melee = 5,     // 近接型
        Spinning = 6,  // 回転型
        Aura = 7,      // オーラ型
        Ground = 8,    // 地面設置型
        Orbital = 9,   // 軌道型
        Summon = 10,   // 召喚型
        Passive = 11   // パッシブ型
    }

    /// <summary>
    /// 武器ファクトリー（純粋C#クラス生成）
    /// WeaponTypeIdに基づいて適切なSurvivorWeaponBase派生クラスを生成
    /// </summary>
    public static class SurvivorWeaponFactory
    {
        /// <summary>
        /// マスターデータから武器を生成
        /// </summary>
        /// <param name="weaponMaster">武器マスター</param>
        /// <param name="assetService">アセットサービス</param>
        /// <param name="poolParent">プールの親Transform</param>
        /// <returns>生成された武器インスタンス</returns>
        public static SurvivorWeaponBase Create(
            SurvivorWeaponMaster weaponMaster,
            IAddressableAssetService assetService,
            Transform poolParent)
        {
            var weaponType = (SurvivorWeaponType)weaponMaster.WeaponType;

            return weaponType switch
            {
                SurvivorWeaponType.AutoFire => new SurvivorAutoFireWeapon(assetService, poolParent),
                SurvivorWeaponType.Homing => CreateHomingWeapon(assetService, poolParent),
                SurvivorWeaponType.Piercing => CreatePiercingWeapon(assetService, poolParent),
                SurvivorWeaponType.Explosive => CreateExplosiveWeapon(assetService, poolParent),
                SurvivorWeaponType.Melee => CreateMeleeWeapon(assetService, poolParent),
                SurvivorWeaponType.Spinning => CreateSpinningWeapon(assetService, poolParent),
                SurvivorWeaponType.Aura => CreateAuraWeapon(assetService, poolParent),
                SurvivorWeaponType.Ground => CreateGroundWeapon(assetService, poolParent),
                SurvivorWeaponType.Orbital => CreateOrbitalWeapon(assetService, poolParent),
                SurvivorWeaponType.Summon => CreateSummonWeapon(assetService, poolParent),
                SurvivorWeaponType.Passive => CreatePassiveWeapon(assetService, poolParent),
                _ => DefaultWeapon(weaponMaster.WeaponType, assetService, poolParent)
            };
        }

        private static SurvivorWeaponBase DefaultWeapon(int weaponTypeId, IAddressableAssetService assetService, Transform poolParent)
        {
            Debug.LogWarning($"[SurvivorWeaponFactory] Unknown weapon type: {weaponTypeId}, defaulting to AutoFire");
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #region Projectile Weapons

        private static SurvivorWeaponBase CreateHomingWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorHomingWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        private static SurvivorWeaponBase CreatePiercingWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorPiercingWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        private static SurvivorWeaponBase CreateExplosiveWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorExplosiveWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #endregion

        #region Melee Weapons

        private static SurvivorWeaponBase CreateMeleeWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorMeleeWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        private static SurvivorWeaponBase CreateSpinningWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorSpinningWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #endregion

        #region Area Weapons

        private static SurvivorWeaponBase CreateAuraWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorAuraWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        private static SurvivorWeaponBase CreateGroundWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorGroundWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #endregion

        #region Summon Weapons

        private static SurvivorWeaponBase CreateOrbitalWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorOrbitalWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        private static SurvivorWeaponBase CreateSummonWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorSummonWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #endregion

        #region Passive Weapons

        private static SurvivorWeaponBase CreatePassiveWeapon(IAddressableAssetService assetService, Transform poolParent)
        {
            // TODO: SurvivorPassiveWeapon実装後に差し替え
            return new SurvivorAutoFireWeapon(assetService, poolParent);
        }

        #endregion

        #region Utility

        /// <summary>
        /// 武器タイプを取得
        /// </summary>
        public static SurvivorWeaponType GetWeaponType(int typeId)
        {
            return (SurvivorWeaponType)typeId;
        }

        /// <summary>
        /// 武器タイプの表示名を取得
        /// </summary>
        public static string GetWeaponTypeName(SurvivorWeaponType type)
        {
            return type switch
            {
                SurvivorWeaponType.AutoFire => "自動発射",
                SurvivorWeaponType.Homing => "追尾",
                SurvivorWeaponType.Piercing => "貫通",
                SurvivorWeaponType.Explosive => "爆発",
                SurvivorWeaponType.Melee => "近接",
                SurvivorWeaponType.Spinning => "回転",
                SurvivorWeaponType.Aura => "オーラ",
                SurvivorWeaponType.Ground => "地面設置",
                SurvivorWeaponType.Orbital => "軌道",
                SurvivorWeaponType.Summon => "召喚",
                SurvivorWeaponType.Passive => "パッシブ",
                _ => "不明"
            };
        }

        #endregion
    }
}