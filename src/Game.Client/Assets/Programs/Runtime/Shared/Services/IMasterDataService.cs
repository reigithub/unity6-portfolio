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
        /// <summary>
        /// インメモリデータベース（MasterMemory）
        /// マスターデータへのクエリアクセスを提供
        /// </summary>
        MemoryDatabase MemoryDatabase { get; }

        /// <summary>
        /// マスターデータを非同期でロードする
        /// Addressables経由でバイナリデータを取得し、MemoryDatabaseを構築する
        /// </summary>
        /// <returns>ロード完了を待機するタスク</returns>
        UniTask LoadMasterDataAsync();
    }
}
