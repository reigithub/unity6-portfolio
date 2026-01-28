namespace Game.Shared.Extensions
{
    /// <summary>
    /// UnityEngine.Object用の拡張メソッド
    /// </summary>
    public static class UnityEngineObjectExtensions
    {
        /// <summary>
        /// オブジェクトを安全に破棄する
        /// エディタ実行中はDestroyImmediate、プレイ中はDestroyを使用
        /// </summary>
        /// <param name="obj">破棄するオブジェクト</param>
        public static void SafeDestroy(this UnityEngine.Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(obj);
                return;
            }
#endif
            UnityEngine.Object.Destroy(obj);
        }
    }
}