using UnityEngine;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// GameObject 用の拡張メソッド。
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// GameObject とその全子孫の Layer を再帰的に設定する。
        /// プレハブ側 Layer 設定漏れの保険としてランタイムで強制適用する用途。
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
