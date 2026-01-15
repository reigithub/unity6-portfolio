using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivorレベルアップマスター
    /// レベルごとの必要経験値を定義
    /// </summary>
    [MemoryTable("SurvivorLevelUpMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorLevelUpMaster
    {
        /// <summary>レベル（PrimaryKey）</summary>
        [PrimaryKey]
        public int Level { get; set; }

        /// <summary>次のレベルに必要な累計経験値</summary>
        public int RequiredExperience { get; set; }

        /// <summary>レベルアップ時のHP増加量</summary>
        public int HpBonus { get; set; }

        /// <summary>レベルアップ時の攻撃力増加（%）</summary>
        public int DamageBonus { get; set; }

        /// <summary>武器選択肢数</summary>
        public int WeaponChoiceCount { get; set; }
    }
}
