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

        // 発射パラメータ
        public int ProcInterval;       // 攻撃間隔（ミリ秒）
        public int EmitCount = 1;      // 同時発射数

        // サーバー側ヒットレート検証用
        public float LastHitTime;      // 最後にヒットを処理した時刻
        public int HitCountInWindow;   // 現在のウィンドウ内ヒット数

        // 倍率
        public float DamageMultiplier = 1f;

        /// <summary>
        /// 最終ダメージ（Damage × DamageMultiplier）
        /// </summary>
        public int FinalDamage => Mathf.RoundToInt(Damage * DamageMultiplier);

        /// <summary>
        /// 武器の発射レートを検証する。
        /// ProcInterval と EmitCount に基づき、ウィンドウ内のヒット数が許容範囲内か確認する。
        /// </summary>
        /// <param name="currentTime">現在のゲーム時間（秒）</param>
        /// <returns>ヒットを受け入れる場合 true</returns>
        public bool ValidateFireRate(float currentTime)
        {
            if (ProcInterval <= 0) return true;

            float windowSec = ProcInterval / 1000f;
            int maxHitsPerWindow = EmitCount * (1 + Pierce);
            int allowedHits = Mathf.CeilToInt(maxHitsPerWindow * 1.5f);

            if (currentTime - LastHitTime > windowSec)
            {
                LastHitTime = currentTime;
                HitCountInWindow = 1;
                return true;
            }

            HitCountInWindow++;
            return HitCountInWindow <= allowedHits;
        }
    }
}
