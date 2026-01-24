using UnityEngine;

namespace Game.Shared.Events
{
    /// <summary>
    /// 死亡イベントのデータ
    /// </summary>
    public struct DeathEventData
    {
        /// <summary>
        /// 死亡位置
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// アイテムドロップグループID（0=ドロップなし）
        /// </summary>
        public int ItemDropGroupId { get; set; }

        /// <summary>
        /// 経験値ドロップグループID（0=ドロップなし）
        /// </summary>
        public int ExpDropGroupId { get; set; }

        public DeathEventData(Vector3 position, int itemDropGroupId, int expDropGroupId)
        {
            Position = position;
            ItemDropGroupId = itemDropGroupId;
            ExpDropGroupId = expDropGroupId;
        }
    }
}
