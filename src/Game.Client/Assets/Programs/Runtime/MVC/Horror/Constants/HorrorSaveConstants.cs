namespace Game.Horror.Constants
{
    public static class HorrorSaveConstants
    {
        /// <summary>セーブスロット数上限。</summary>
        public const int MaxSaveSlotCount = 10;

        /// <summary>現行のセーブデータバージョン。スキーマ変更時にここだけを上げる。</summary>
        public const int SaveDataLatestVersion = 1;
    }
}
