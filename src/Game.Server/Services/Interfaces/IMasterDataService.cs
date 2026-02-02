using Game.Server.MasterData;
using MasterMemory;

namespace Game.Server.Services.Interfaces;

public interface IMasterDataService
{
    MemoryDatabase MemoryDatabase { get; }
}
