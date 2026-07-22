using System.Collections.Generic;

namespace Game.Horror.Constants
{
    public static class HorrorEquipmentConstants
    {
        /// <summary>ショートカットスロット数（D-Pad 1〜4）。</summary>
        public const int MaxEquipmentSlotCount = 4;

        // スロットINDEXをD-pad入力方向に変換
        public static readonly Dictionary<int, string> SlotInputDirections = new()
        {
            { 1, "Up" },
            { 3, "Down" },
            { 0, "Left" },
            { 2, "Right" }
        };
    }
}
