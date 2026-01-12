using MasterMemory;
using MessagePack;

namespace Game.Library.Shared.MasterData.MemoryTables
{
    [MemoryTable("AudioPlayTagsMaster"), MessagePackObject(true)]
    public sealed partial class AudioPlayTagsMaster
    {
        [PrimaryKey]
        public int Id { get; set; }

        public int AudioId { get; set; }

        [SecondaryKey(0), NonUnique]
        public int AudioPlayTag { get; set; }
    }
}