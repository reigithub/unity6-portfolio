using System;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="UnityServerConfig"/> を遅延初期化で提供する Singleton ホルダー。
    /// DI コンテナ構築時点では Config が存在しないため、
    /// <see cref="UnityServerBootstrap.StartAsync"/> 内で <see cref="Set"/> を呼び出して初期化する。
    /// </summary>
    public sealed class UnityServerConfigProvider
    {
        private UnityServerConfig _config;

        /// <summary>
        /// 現在の設定値を取得する。
        /// <see cref="UnityServerBootstrap.StartAsync"/> 完了前にアクセスすると例外が発生する。
        /// </summary>
        /// <exception cref="InvalidOperationException">Config がまだ初期化されていない場合。</exception>
        public UnityServerConfig Current =>
            _config ?? throw new InvalidOperationException(
                "[UnityServerConfigProvider] Config はまだ初期化されていません。" +
                " WaitForStartupAsync() で起動完了を待ってからアクセスしてください。");

        /// <summary>
        /// 設定値をセットする。<see cref="UnityServerBootstrap.StartAsync"/> からのみ呼び出す。
        /// </summary>
        /// <param name="config">構築済みの設定値。</param>
        internal void Set(UnityServerConfig config)
        {
            _config = config;
        }
    }
}
