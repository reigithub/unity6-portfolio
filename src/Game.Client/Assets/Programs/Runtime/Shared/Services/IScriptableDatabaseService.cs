using Cysharp.Threading.Tasks;
using Game.Shared.Scriptable.Database;
using Game.Shared.Services.Interfaces;

namespace Game.Shared.Services
{
    /// <summary>
    /// ScriptableTable コンテナ（<see cref="ScriptableDatabase"/>）をロードして提供するサービスの共通インターフェース。
    /// MasterMemory の <c>IMasterDataService</c>（MemoryDatabase）に対応する ScriptableObject 版。
    /// </summary>
    public interface IScriptableDatabaseService : IGameService
    {
        /// <summary>ロード済みのテーブルコンテナ。</summary>
        ScriptableDatabase Database { get; }

        /// <summary>コンテナ資産を非同期でロードする。</summary>
        UniTask LoadAsync();
    }
}
