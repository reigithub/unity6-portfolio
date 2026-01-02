using MasterMemory;
using MessagePack;

namespace Game.Core.MasterData.MemoryTables
{
    [MemoryTable("PlayerMaster"), MessagePackObject(true)]
    public sealed partial class PlayerMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public string Name { get; set; }
        public string AssetName { get; set; }

        public int MaxHp { get; set; }
        public int MaxStamina { get; set; }
        public int StaminaDepleteRate { get; set; }
        public int StaminaRegenRate { get; set; }

        public int WalkSpeed { get; set; }
        public int JogSpeed { get; set; }
        public int RunSpeed { get; set; }
    }
}