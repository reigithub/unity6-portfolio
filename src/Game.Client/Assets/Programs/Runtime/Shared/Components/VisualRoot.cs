using UnityEngine;

namespace Game.Shared.Components
{
    /// <summary>
    /// Visual ルートを示すマーカーコンポーネント。
    /// プレハブの Visual 子 GameObject にアタッチし、
    /// サーバー時に SetActive(false) で一括無効化する対象を識別する。
    /// </summary>
    public class VisualRoot : MonoBehaviour { }
}
