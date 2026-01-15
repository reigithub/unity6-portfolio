using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// ゲーム固有セーブサービスの基底クラス
    /// 共通の読み書き・ダーティフラグ管理を提供
    /// </summary>
    /// <typeparam name="TData">MemoryPackableなセーブデータ型</typeparam>
    public abstract class SaveServiceBase<TData> where TData : class, new()
    {
        protected readonly ISaveDataStorage _storage;

        private TData _data;
        private bool _isDirty;

        /// <summary>保存キー（ファイル名）</summary>
        protected abstract string SaveKey { get; }

        /// <summary>現在のデータバージョン</summary>
        protected virtual int CurrentVersion => 1;

        /// <summary>現在のセーブデータ</summary>
        public TData Data => _data;

        /// <summary>データが読み込み済みか</summary>
        public bool IsLoaded => _data != null;

        /// <summary>未保存の変更があるか</summary>
        public bool IsDirty => _isDirty;

        protected SaveServiceBase(ISaveDataStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// セーブデータを読み込む
        /// </summary>
        public async UniTask LoadAsync()
        {
            try
            {
                _data = await _storage.LoadAsync<TData>(SaveKey);

                if (_data == null)
                {
                    _data = CreateNewData();
                    Debug.Log($"[{GetType().Name}] Created new save data.");
                }
                else
                {
                    // バージョンチェックとマイグレーション
                    var version = GetDataVersion(_data);
                    if (version < CurrentVersion)
                    {
                        MigrateData(_data, version);
                        _isDirty = true;
                    }

                    OnDataLoaded(_data);
                    Debug.Log($"[{GetType().Name}] Loaded save data.");
                }

                _isDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Failed to load: {e.Message}");
                _data = CreateNewData();
                _isDirty = false;
            }
        }

        /// <summary>
        /// セーブデータを保存する
        /// </summary>
        public async UniTask SaveAsync()
        {
            if (_data == null)
            {
                Debug.LogWarning($"[{GetType().Name}] No data to save.");
                return;
            }

            try
            {
                OnBeforeSave(_data);
                await _storage.SaveAsync(SaveKey, _data);
                _isDirty = false;
                Debug.Log($"[{GetType().Name}] Saved successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// 変更がある場合のみ保存する
        /// </summary>
        public async UniTask SaveIfDirtyAsync()
        {
            if (_isDirty)
            {
                await SaveAsync();
            }
        }

        /// <summary>
        /// セーブデータを削除する
        /// </summary>
        public async UniTask DeleteAsync()
        {
            try
            {
                await _storage.DeleteAsync(SaveKey);
                _data = CreateNewData();
                _isDirty = false;
                Debug.Log($"[{GetType().Name}] Save data deleted.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Failed to delete: {e.Message}");
            }
        }

        /// <summary>
        /// ダーティフラグを設定（派生クラスから呼び出し）
        /// </summary>
        protected void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 新規セーブデータを作成（派生クラスでオーバーライド可能）
        /// </summary>
        protected virtual TData CreateNewData()
        {
            return new TData();
        }

        /// <summary>
        /// データ読み込み後の処理（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void OnDataLoaded(TData data)
        {
        }

        /// <summary>
        /// 保存前の処理（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void OnBeforeSave(TData data)
        {
        }

        /// <summary>
        /// データのバージョンを取得（派生クラスでオーバーライド）
        /// </summary>
        protected virtual int GetDataVersion(TData data)
        {
            return CurrentVersion;
        }

        /// <summary>
        /// データのマイグレーション処理（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void MigrateData(TData data, int fromVersion)
        {
            Debug.Log($"[{GetType().Name}] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
