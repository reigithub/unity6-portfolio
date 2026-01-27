using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Shared.Services
{
    /// <summary>
    /// Addressablesアセット読み込みサービスの共通インターフェース
    /// MVC/MVP両方で使用
    /// </summary>
    public interface IAddressableAssetService
    {
        /// <summary>
        /// Addressablesからアセットを非同期で読み込む
        /// </summary>
        /// <typeparam name="T">読み込むアセットの型（UnityEngine.Object派生型）</typeparam>
        /// <param name="address">Addressablesアドレス（アセットのキー）</param>
        /// <returns>読み込まれたアセット</returns>
        /// <remarks>使用後はReleaseAssetで解放すること</remarks>
        UniTask<T> LoadAssetAsync<T>(string address) where T : Object;

        /// <summary>
        /// Addressablesからプレハブを読み込んでインスタンス化する
        /// </summary>
        /// <param name="address">Addressablesアドレス（プレハブのキー）</param>
        /// <param name="parent">親トランスフォーム（nullの場合はルートに配置）</param>
        /// <returns>生成されたGameObjectインスタンス</returns>
        /// <remarks>使用後はReleaseInstanceで解放すること</remarks>
        UniTask<GameObject> InstantiateAsync(string address, Transform parent = null);

        /// <summary>
        /// Addressablesからシーンを非同期で読み込む
        /// </summary>
        /// <param name="sceneName">シーン名またはAddressablesアドレス</param>
        /// <param name="loadSceneMode">読み込みモード（Additive: 追加読み込み、Single: 置き換え）</param>
        /// <param name="activateOnLoad">読み込み完了時に自動でアクティブ化するか</param>
        /// <returns>読み込まれたシーンインスタンス</returns>
        /// <remarks>使用後はUnloadSceneAsyncでアンロードすること</remarks>
        UniTask<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode loadSceneMode = LoadSceneMode.Additive, bool activateOnLoad = true);

        /// <summary>
        /// LoadSceneAsyncで読み込んだシーンをアンロードする
        /// </summary>
        /// <param name="sceneInstance">アンロードするシーンインスタンス</param>
        UniTask UnloadSceneAsync(SceneInstance sceneInstance);

        /// <summary>
        /// LoadAssetAsyncで読み込んだアセットを解放する
        /// </summary>
        /// <typeparam name="T">アセットの型</typeparam>
        /// <param name="asset">解放するアセット</param>
        /// <remarks>参照カウントが0になるとメモリから解放される</remarks>
        void ReleaseAsset<T>(T asset) where T : Object;

        /// <summary>
        /// InstantiateAsyncで生成したインスタンスを解放する
        /// </summary>
        /// <param name="instance">解放するGameObjectインスタンス</param>
        /// <remarks>GameObjectの破棄と参照カウントの減少を行う</remarks>
        void ReleaseInstance(GameObject instance);
    }
}
