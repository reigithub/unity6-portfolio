using System;

namespace Game.Shared.Services
{
    /// <summary>
    /// ゲーム起動時に生成される永続オブジェクトを保持・提供するインターフェース
    /// DontDestroyOnLoadなオブジェクトやシングルトン的なコンポーネントを管理
    /// </summary>
    public interface IPersistentObjectProvider
    {
        /// <summary>
        /// 永続オブジェクトを登録
        /// </summary>
        void Register<T>(T instance) where T : class;

        /// <summary>
        /// 永続オブジェクトを取得（未登録の場合は例外）
        /// </summary>
        T Get<T>() where T : class;

        /// <summary>
        /// 永続オブジェクトの取得を試行
        /// </summary>
        bool TryGet<T>(out T instance) where T : class;

        /// <summary>
        /// 永続オブジェクトの登録を解除
        /// </summary>
        void Unregister<T>() where T : class;

        /// <summary>
        /// 全ての永続オブジェクトをクリア
        /// </summary>
        void Clear();
    }
}
