namespace Game.Shared.Network.Survivor
{
    public interface ISurvivorPlayerMovementHandler
    {
        /// <summary>
        /// Fusion tick ごとに呼ばれる。入力消費 → ステート更新 → 物理移動 → スナップショット返却。
        /// </summary>
        SurvivorPlayerPhysicsSnapshot ProcessTick(SurvivorPlayerNetworkInput input, float deltaTime);
    }

    public struct SurvivorPlayerPhysicsSnapshot
    {
        public float Speed;
        public int Health;
        public int MaxHealth;
        public int Stamina;
        public int MaxStamina;
        public bool IsInvincible;
    }
}
