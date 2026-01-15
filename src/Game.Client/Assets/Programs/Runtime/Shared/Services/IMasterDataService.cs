using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;

namespace Game.Shared.Services
{
    /// <summary>
    /// マスターデータ管理サービスの共通インターフェース
    /// MVC/MVP両方で使用
    /// </summary>
    public interface IMasterDataService
    {
        MemoryDatabase MemoryDatabase { get; }
        UniTask LoadMasterDataAsync();
    }
}
