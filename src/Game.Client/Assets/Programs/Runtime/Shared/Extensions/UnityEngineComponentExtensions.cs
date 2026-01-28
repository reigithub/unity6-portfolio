namespace Game.Shared.Extensions
{
    /// <summary>
    /// UnityEngine.Component用の拡張メソッド
    /// </summary>
    public static class UnityEngineComponentExtensions
    {
        /// <summary>
        /// コンポーネントのGameObjectが指定レイヤーに属しているか判定する
        /// </summary>
        /// <param name="component">判定対象のコンポーネント</param>
        /// <param name="layer">比較するレイヤー番号</param>
        /// <returns>レイヤーが一致する場合true</returns>
        public static bool CompareLayer(this UnityEngine.Component component, int layer)
        {
            if (component == null)
                return false;

            return component.gameObject.layer == layer;
        }
    }
}