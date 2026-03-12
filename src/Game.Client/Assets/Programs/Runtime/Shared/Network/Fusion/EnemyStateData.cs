using Fusion;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// 敵状態の Fusion ネットワーク同期用構造体。
    /// NetworkArray に格納して 500+ 体の敵を一括同期する。
    /// </summary>
    [System.Serializable]
    public struct EnemyStateData : INetworkStruct
    {
        public int NetworkId;
        public int EnemyMasterId;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float VelX;
        public float VelY;
        public float VelZ;
        public int CurrentHp;
        public byte SyncTypeByte;
    }
}
