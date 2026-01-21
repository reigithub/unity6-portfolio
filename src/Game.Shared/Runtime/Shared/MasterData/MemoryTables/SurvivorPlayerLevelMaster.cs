using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorプレイヤーレベルマスター
    /// プレイヤーキャラクターのレベル毎のパラメータを定義
    /// </summary>
    [MemoryTable("SurvivorPlayerLevelMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorPlayerLevelMaster
    {
        /// <summary>プレイヤーID</summary>
        [PrimaryKey(0)]
        public int PlayerId { get; set; }

        /// <summary>レベル</summary>
        [PrimaryKey(1)]
        public int Level { get; set; }

        /// <summary>次のレベルに必要な累計経験値</summary>
        public int RequiredExp { get; set; }

        /// <summary>最大HP</summary>
        public int MaxHp { get; set; }

        /// <summary>最大スタミナ</summary>
        public int MaxStamina { get; set; }

        /// <summary>スタミナ減少量（1秒毎）</summary>
        public int StaminaDepleteRate { get; set; }

        /// <summary>スタミナ回復量（1秒毎）</summary>
        public int StaminaRegenRate { get; set; }

        /// <summary>移動速度（1000倍値、ToUnit()で変換）</summary>
        public int MoveSpeed { get; set; }

        /// <summary>ダッシュ速度（1000倍値、ToUnit()で変換）</summary>
        public int RunSpeed { get; set; }

        /// <summary>アイテム拾得範囲（1000倍値、ToUnit()で変換）</summary>
        public int PickupRange { get; set; }

        /// <summary>クリティカル率（万分率、ToRate()で変換）</summary>
        public int CritRate { get; set; }

        /// <summary>クリティカルダメージ倍率（万分率、ToRate()で変換）</summary>
        public int CritDamage { get; set; }

        /// <summary>無敵時間（ms、ToSeconds()で変換）</summary>
        public int InvincibilityDuration { get; set; }

        /// <summary>アイテム吸引距離（1000倍値、ToUnit()で変換）</summary>
        public int ItemAttractDistance { get; set; }

        /// <summary>アイテム吸引速度（1000倍値、ToUnit()で変換）</summary>
        public int ItemAttractSpeed { get; set; }

        /// <summary>アイテム収集判定距離（1000倍値、ToUnit()で変換）</summary>
        public int ItemCollectDistance { get; set; }

        /// <summary>攻撃力ボーナス（万分率、ToRate()で変換）</summary>
        public int DamageBonus { get; set; }

        /// <summary>武器選択肢数</summary>
        public int WeaponChoiceCount { get; set; }
    }
}
