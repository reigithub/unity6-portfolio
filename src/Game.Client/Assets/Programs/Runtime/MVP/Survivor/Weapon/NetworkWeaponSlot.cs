using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// サーバー用武器データスロット（純粋C#）。
    /// MonoBehaviour やプール/VFX を持たず、マスターデータから取得した
    /// ダメージ計算に必要なパラメータのみを保持する。
    /// </summary>
    public class NetworkWeaponSlot
    {
        // 基本情報
        public int WeaponId;
        public string Name;
        public string IconAssetName;
        public int Level;
        public int MaxLevel;

        // ダメージ計算パラメータ
        public int Damage;
        public int ProcRate;
        public int CritChance;
        public int CritMultiplier;
        public int Pierce;
        public float Knockback;
        public float Range;

        // 倍率
        public float DamageMultiplier = 1f;

        /// <summary>
        /// 最終ダメージ（Damage × DamageMultiplier）
        /// </summary>
        public int FinalDamage => Mathf.RoundToInt(Damage * DamageMultiplier);
    }
}
