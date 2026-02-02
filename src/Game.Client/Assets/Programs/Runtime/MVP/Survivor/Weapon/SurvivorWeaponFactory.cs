using System;
using Game.Client.MasterData;
using UnityEngine;
using VContainer;

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
    /// 武器ファクトリー
    /// WeaponTypeIdに基づいて適切なSurvivorWeaponBase派生クラスを生成
    /// </summary>
    public static class SurvivorWeaponFactory
    {
        /// <summary>
        /// マスターデータから武器を生成
        /// </summary>
        /// <param name="resolver"></param>
        /// <param name="weaponMaster">武器マスター</param>
        /// <returns>生成された武器インスタンス</returns>
        public static SurvivorWeaponBase Create(
            IObjectResolver resolver,
            SurvivorWeaponMaster weaponMaster)
        {
            SurvivorWeaponBase weapon = (SurvivorWeaponType)weaponMaster.WeaponType switch
            {
                SurvivorWeaponType.AutoFire => new SurvivorAutoFireWeapon(weaponMaster),
                SurvivorWeaponType.Ground => new SurvivorGroundWeapon(weaponMaster),
                _ => throw new NotImplementedException()
            };
            resolver.Inject(weapon);
            return weapon;
        }
    }
}