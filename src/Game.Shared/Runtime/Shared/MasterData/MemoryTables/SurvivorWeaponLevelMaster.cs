using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    /// <summary>
    /// Survivor武器レベルマスター
    /// 武器のレベルごとのパラメータを定義
    /// </summary>
    [MemoryTable("SurvivorWeaponLevelMaster"), MessagePackObject(true)]
    public sealed partial class SurvivorWeaponLevelMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        /// <summary>武器ID（SurvivorWeaponMasterを参照）</summary>
        [SecondaryKey(0), NonUnique]
        [SecondaryKey(1, keyOrder: 0)]
        public int WeaponId { get; set; }

        #region 基本情報

        /// <summary>レベル</summary>
        [SecondaryKey(1, keyOrder: 1)]
        public int Level { get; set; }

        /// <summary>このレベルで使用するアセット名（プロジェクタイル、エフェクト等）</summary>
        public string AssetName { get; set; }

        /// <summary>このレベルでの効果説明（レベルアップ時に表示）</summary>
        public string Description { get; set; }

        #endregion

        #region 基本パラメータ

        /// <summary>ダメージ量</summary>
        public int Damage { get; set; }

        /// <summary>射程、敵/ターゲット検索範囲（1000倍値、ToUnit()で変換）</summary>
        public int Range { get; set; }

        /// <summary>生成物の移動速度（1000倍値、ToUnit()で変換）</summary>
        public int Speed { get; set; }

        /// <summary>発生時間（ms、ToSeconds()で変換）</summary>
        public int Duration { get; set; }

        /// <summary>再度発動できるまでのクールダウン時間（ms、ToSeconds()で変換）</summary>
        public int Cooldown { get; set; }

        /// <summary>ダメージ発生確率（万分率、ToRate()で変換）</summary>
        public int ProcRate { get; set; }

        /// <summary>発動の間隔（ms、ToSeconds()で変換）</summary>
        public int ProcInterval { get; set; }

        /// <summary>1回の発動で生成する数</summary>
        public int EmitCount { get; set; }

        /// <summary>複数生成時の間隔（ms、ToSeconds()で変換）、0=同時</summary>
        public int EmitDelay { get; set; }

        /// <summary>同時存在上限</summary>
        public int EmitLimit { get; set; }

        /// <summary>生成物のヒット回数（-1=AoE/無限）</summary>
        public int HitCount { get; set; }

        /// <summary>生成物の当たり判定サイズ（万分率、ToRate()で変換、10000=等倍）</summary>
        public int HitBoxRate { get; set; }

        /// <summary>クリティカル発生確率（万分率、ToRate()で変換）</summary>
        public int CritHitRate { get; set; }

        /// <summary>クリティカル倍率（万分率、ToRate()で変換、15000=1.5倍）</summary>
        public int CritHitMultiplier { get; set; }

        #endregion

        #region 武器の軌道・物理特性パラメータ

        /// <summary>ノックバック力（1000倍値、ToUnit()で変換）</summary>
        public int Knockback { get; set; }

        /// <summary>引き寄せ力（1000倍値、ToUnit()で変換）</summary>
        public int Vacuum { get; set; }

        /// <summary>回転/周回速度（度/s）</summary>
        public int Spin { get; set; }

        /// <summary>貫通数（何体の敵を貫通するか）、0=貫通なし</summary>
        public int Penetration { get; set; }

        /// <summary>バウンド回数、0=バウンドなし</summary>
        public int Bounce { get; set; }

        /// <summary>チェイン回数、0=チェインなし</summary>
        public int Chain { get; set; }

        /// <summary>追尾性能（万分率、ToRate()で変換、0=直進、10000=完全追尾）</summary>
        public int Homing { get; set; }

        /// <summary>拡散角度（度）、0=集中</summary>
        public int Spread { get; set; }

        #endregion
    }
}