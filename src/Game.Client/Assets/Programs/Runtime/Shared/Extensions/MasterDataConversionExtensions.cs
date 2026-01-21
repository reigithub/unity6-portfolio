namespace Game.Shared.Extensions
{
    /// <summary>
    /// マスタデータの整数値をゲーム内のfloat値に変換する拡張メソッド
    ///
    /// 【マスタデータ設計規約】
    /// - マスタデータはすべてint型で定義
    /// - floatが必要な値は以下の倍率で格納:
    ///   - 割合(%): 万分率 (10000 = 100%)
    ///   - 時間: ミリ秒 (1000 = 1秒)
    ///   - 距離/速度/サイズ: 1000倍値 (1000 = 1.0)
    ///   - スケール/倍率: 100倍値 (100 = 1.0倍)
    /// </summary>
    public static class MasterDataConversionExtensions
    {
        #region 割合 (万分率 / Basis Points)

        /// <summary>
        /// 万分率 → float (10000 = 1.0 = 100%)
        /// 使用例: ダメージ倍率、確率、割合
        /// </summary>
        public static float ToRate(this int basisPoints) => basisPoints * 0.0001f;

        /// <summary>
        /// 万分率 → パーセント表示用 (10000 = 100)
        /// UI表示用
        /// </summary>
        public static float ToPercent(this int basisPoints) => basisPoints * 0.01f;

        /// <summary>
        /// 万分率で確率判定
        /// </summary>
        public static bool RollChance(this int basisPoints)
            => UnityEngine.Random.Range(0, 10000) < basisPoints;

        #endregion

        #region 時間 (ミリ秒)

        /// <summary>
        /// ミリ秒 → 秒 (1000 = 1.0秒)
        /// 使用例: クールダウン、持続時間、無敵時間
        /// </summary>
        public static float ToSeconds(this int milliseconds) => milliseconds * 0.001f;

        #endregion

        #region 距離・速度・サイズ (1000倍値)

        /// <summary>
        /// 1000倍値 → float (1000 = 1.0)
        /// 使用例: 移動速度、射程、範囲、距離
        /// </summary>
        public static float ToUnit(this int thousandths) => thousandths * 0.001f;

        #endregion

        #region スケール・倍率 (100倍値)

        /// <summary>
        /// 100倍値 → float (100 = 1.0)
        /// 使用例: スケール、サイズ倍率
        /// </summary>
        public static float ToScale(this int hundredths) => hundredths * 0.01f;

        #endregion
    }
}
