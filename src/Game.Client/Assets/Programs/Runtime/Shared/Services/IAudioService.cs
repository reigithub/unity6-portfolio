using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;

namespace Game.Shared.Services
{
    /// <summary>
    /// オーディオ再生サービスの共通インターフェース
    /// MVC/MVP両方で使用
    /// </summary>
    public interface IAudioService
    {
        void Startup();
        void Shutdown();

        UniTask PlayBgmAsync(string assetName, CancellationToken token = default);
        UniTask StopBgmAsync();
        UniTask PlayVoiceAsync(string assetName, CancellationToken token = default);
        UniTask PlaySoundEffectAsync(string assetName, CancellationToken token = default);
        UniTask PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default);
        UniTask PlayAsync(int audioId, CancellationToken token = default);
        UniTask PlayAsync(int[] audioIds, CancellationToken token = default);
        UniTask PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default);
        UniTask PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default);
    }
}