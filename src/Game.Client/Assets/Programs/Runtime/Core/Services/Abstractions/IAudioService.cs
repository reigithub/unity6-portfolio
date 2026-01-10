using System.Threading;
using System.Threading.Tasks;
using Game.Core.Enums;

namespace Game.Core.Services
{
    /// <summary>
    /// オーディオ再生サービスのインターフェース
    /// </summary>
    public interface IAudioService : IGameService
    {
        Task PlayBgmAsync(string assetName);
        void StopBgm();
        Task PlayVoiceAsync(string assetName, CancellationToken token = default);
        Task PlaySoundEffectAsync(string assetName, CancellationToken token = default);
        Task PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default);
        Task PlayAsync(int audioId, CancellationToken token = default);
        Task PlayAsync(int[] audioIds, CancellationToken token = default);
        Task PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default);
        Task PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default);
    }
}
