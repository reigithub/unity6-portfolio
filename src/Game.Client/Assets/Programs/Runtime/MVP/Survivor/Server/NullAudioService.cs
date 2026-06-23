using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.Shared.Services;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用オーディオサービス（全メソッドno-op）
    /// </summary>
    public class NullAudioService : IAudioService
    {
        public UniTask LoadAsync() => UniTask.CompletedTask;
        public void Unload() { }
        public UniTask PlayBgmAsync(string assetName, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask StopBgmAsync(CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayVoiceAsync(string assetName, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlaySoundEffectAsync(string assetName, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayAsync(int audioId, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayAsync(int[] audioIds, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default) => UniTask.CompletedTask;
        public UniTask PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default) => UniTask.CompletedTask;
        public void SetVolume(float bgm, float voice, float sfx) { }
    }
}
