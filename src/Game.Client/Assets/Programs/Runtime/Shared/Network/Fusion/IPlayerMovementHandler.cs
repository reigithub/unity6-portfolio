namespace Game.Shared.Network.Fusion
{
    public interface IPlayerMovementHandler
    {
        /// <summary>
        /// Fusion tick ごとに呼ばれる。入力消費 → ステート更新 → 物理移動 → スナップショット返却。
        /// </summary>
        PlayerPhysicsSnapshot ProcessTick(PlayerNetworkInput input, float deltaTime);
    }

    public struct PlayerPhysicsSnapshot
    {
        public UnityEngine.Vector3 Position;
        public float RotationY;
        public float Speed;
        public int Health;
        public int MaxHealth;
        public int Stamina;
        public int MaxStamina;
        public bool IsInvincible;
    }
}
