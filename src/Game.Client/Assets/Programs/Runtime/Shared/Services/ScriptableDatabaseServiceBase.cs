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
                    throw new MasterDataLoadException(nameof(ScriptableDatabase), "ScriptableDatabase asset returned null");
                }

                // テーブル結線（オーサリング時に確定する構成）はここで一括検査し、欠落したまま起動させない。
                // 編集時/CI の検証（ScriptableDatabaseSchema）が第一層で、ここはビルド資産の齟齬に対する最終防衛
                if (database.HasUnassignedTable())
                {
                    throw new MasterDataLoadException(nameof(ScriptableDatabase),
                        "テーブル資産が未結線です。ScriptableDatabaseWindow の Register を実行してください。");
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
                throw new MasterDataLoadException(nameof(ScriptableDatabase), $"Failed to load ScriptableDatabase: {ex.Message}", ex);
            }
        }
    }
}
