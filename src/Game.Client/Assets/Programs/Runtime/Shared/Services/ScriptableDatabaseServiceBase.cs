using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Scriptable.Database;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// ScriptableDatabase ロードサービスの共通基底。
    /// <c>MasterDataServiceBase</c>（binary → MemoryDatabase）に対応し、SO コンテナ資産をロードして保持する。
    /// </summary>
    public abstract class ScriptableDatabaseServiceBase : IScriptableDatabaseService
    {
        public ScriptableDatabase Database { get; private set; }

        /// <summary>コンテナ資産を読み込む（派生クラスでロード機構を実装）。</summary>
        protected abstract UniTask<ScriptableDatabase> LoadDatabaseAssetAsync();

        public async UniTask LoadAsync()
        {
            try
            {
                var database = await LoadDatabaseAssetAsync();
                if (database == null)
                {
                    throw new MasterDataLoadException(
                        nameof(ScriptableDatabase),
                        "ScriptableDatabase asset returned null");
                }

                Database = database;
                Debug.Log($"[{GetType().Name}] ScriptableDatabase loaded successfully.");
            }
            catch (MasterDataLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MasterDataLoadException(
                    nameof(ScriptableDatabase),
                    $"Failed to load ScriptableDatabase: {ex.Message}",
                    ex);
            }
        }
    }
}
