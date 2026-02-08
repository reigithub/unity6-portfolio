using System;
using Cysharp.Threading.Tasks;
using Game.Shared.SaveData;
using Game.Shared.Services;
using Game.Shared.Services.RemoteAsset;

namespace Game.App.Services
{
    /// <summary>
    /// アプリケーションレベルのサービスプロバイダーインターフェース
    /// タイトル画面などで使用する共通サービスを提供
    /// </summary>
    public interface IAppServiceProvider : IDisposable
    {
        IMasterDataService MasterDataService { get; }
        IAddressableAssetService AddressableAssetService { get; }
        IAudioService AudioService { get; }
        IAudioSaveService AudioSaveService { get; }
        IRemoteAssetDownloadService RemoteAssetDownloadService { get; }

        /// <summary>
        /// サービスを初期化
        /// </summary>
        UniTask InitializeAsync();
    }
}
