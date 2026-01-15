using Cysharp.Threading.Tasks;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// セーブデータストレージの抽象化インターフェース
    /// MemoryPackを使用したバイナリシリアライズ/デシリアライズを提供
    /// </summary>
    public interface ISaveDataStorage
    {
        string BasePath { get; }

        /// <summary>
        /// セーブデータを読み込む
        /// </summary>
        /// <typeparam name="T">MemoryPackableなデータ型</typeparam>
        /// <param name="key">保存キー（ファイル名として使用）</param>
        /// <returns>読み込んだデータ、存在しない場合はdefault</returns>
        UniTask<T> LoadAsync<T>(string key) where T : class;

        /// <summary>
        /// セーブデータを読み込む（存在しない場合はデフォルト値を返す）
        /// </summary>
        /// <typeparam name="T">MemoryPackableなデータ型</typeparam>
        /// <param name="key">保存キー（ファイル名として使用）</param>
        /// <param name="defaultValue">データが存在しない場合のデフォルト値</param>
        /// <returns>読み込んだデータまたはデフォルト値</returns>
        UniTask<T> LoadAsync<T>(string key, T defaultValue) where T : class;

        /// <summary>
        /// セーブデータを保存する
        /// </summary>
        /// <typeparam name="T">MemoryPackableなデータ型</typeparam>
        /// <param name="key">保存キー（ファイル名として使用）</param>
        /// <param name="data">保存するデータ</param>
        UniTask SaveAsync<T>(string key, T data) where T : class;

        /// <summary>
        /// セーブデータを削除する
        /// </summary>
        /// <param name="key">保存キー</param>
        UniTask DeleteAsync(string key);

        /// <summary>
        /// セーブデータが存在するか確認
        /// </summary>
        /// <param name="key">保存キー</param>
        bool Exists(string key);

        /// <summary>
        /// 保存先のフルパスを取得
        /// </summary>
        /// <param name="key">保存キー</param>
        string GetFullPath(string key);
    }
}