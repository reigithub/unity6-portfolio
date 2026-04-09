using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// クライアントアイテムプロキシの管理データ。
    /// SurvivorItemView が各アイテムプロキシを追跡するために使用する。
    /// </summary>
    internal class ItemProxyData
    {
        /// <summary>プロキシ GameObjectへの参照</summary>
        public GameObject GameObject;

        /// <summary>ICollectible 実装コンポーネントへの参照</summary>
        public ItemProxyCollectible Collectible;

        /// <summary>アイテムのスケール値</summary>
        public float Scale;
    }
}
