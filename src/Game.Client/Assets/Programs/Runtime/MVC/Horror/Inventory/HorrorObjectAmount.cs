using Game.Shared.Enums;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// 「どのオブジェクトを何個」を表す数量指定。所持数の判定・消費の指定に使う。
    /// スロット位置は持たない（位置を指定する操作は SlotNo を別途受け取る）。
    /// </summary>
    public readonly struct HorrorObjectAmount
    {
        public ObjectCategory Category { get; init; }
        public int Id { get; init; }
        public int Count { get; init; }
    }
}
