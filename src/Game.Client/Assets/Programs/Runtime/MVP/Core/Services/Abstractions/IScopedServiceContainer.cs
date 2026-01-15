using System;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// 動的なサービスライフサイクル管理インターフェース
    /// GameServiceManager.Add/Remove互換のVContainer版
    /// </summary>
    public interface IScopedServiceContainer
    {
        /// <summary>
        /// サービスを生成・登録（依存注入付き）
        /// </summary>
        T Add<T>() where T : class, new();

        /// <summary>
        /// 登録済みサービスを取得
        /// </summary>
        T Get<T>() where T : class;

        /// <summary>
        /// 登録済みサービスを取得（存在確認付き）
        /// </summary>
        bool TryGet<T>(out T service) where T : class;

        /// <summary>
        /// サービスを破棄・登録解除
        /// </summary>
        void Remove<T>() where T : class;

        /// <summary>
        /// サービスが登録済みか確認
        /// </summary>
        bool Contains<T>() where T : class;
    }
}
