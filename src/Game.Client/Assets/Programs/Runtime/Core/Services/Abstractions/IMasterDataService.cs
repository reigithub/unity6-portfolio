using System.Threading.Tasks;
using Game.Core.MasterData;

namespace Game.Core.Services
{
    /// <summary>
    /// マスターデータ管理サービスのインターフェース
    /// </summary>
    public interface IMasterDataService : IGameService
    {
        MemoryDatabase MemoryDatabase { get; }
        Task LoadMasterDataAsync();
    }
}
