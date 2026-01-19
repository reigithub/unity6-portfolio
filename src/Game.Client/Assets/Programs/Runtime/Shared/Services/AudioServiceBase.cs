using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Library.Shared.Enums;
using Game.Library.Shared.MasterData;
using Game.Shared.Extensions;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// オーディオ再生サービスの共通基底クラス
    /// MVC/MVP両方で継承して使用
    /// </summary>
    public abstract class AudioServiceBase : IAudioService
    {
        private GameObject _audioServiceObject;
        private AudioSource _bgmSource;
        private AudioSource _voiceSource;
        private AudioSource _sfxSource;

        private readonly float _bgmVolume = 0.3f;
        private readonly float _bgmFadeDuration = 0.25f;
        private readonly float _voiceVolume = 1f;
        private readonly float _voiceFadeDuration = 0.1f;
        private readonly float _sfxVolume = 0.7f;
        private readonly float _sfxFadeDuration = 0.1f;

        /// <summary>
        /// マスターデータベースを取得（派生クラスで実装）
        /// </summary>
        protected abstract MemoryDatabase MemoryDatabase { get; }

        /// <summary>
        /// オーディオクリップを読み込む（派生クラスで実装）
        /// </summary>
        protected abstract UniTask<AudioClip> LoadAudioClipAsync(string assetName);

        public void Startup()
        {
            _audioServiceObject = new GameObject("AudioService");
            _audioServiceObject.AddComponent<AudioListener>();
            _bgmSource = new GameObject("BgmSource").AddComponent<AudioSource>();
            _voiceSource = new GameObject("VoiceSource").AddComponent<AudioSource>();
            _sfxSource = new GameObject("SfxSource").AddComponent<AudioSource>();

            _bgmSource.transform.SetParent(_audioServiceObject.transform);
            _voiceSource.transform.SetParent(_audioServiceObject.transform);
            _sfxSource.transform.SetParent(_audioServiceObject.transform);

            UnityEngine.Object.DontDestroyOnLoad(_audioServiceObject);
        }

        public void Shutdown()
        {
            _bgmSource.SafeDestroy();
            _bgmSource = null;
            _voiceSource.SafeDestroy();
            _voiceSource = null;
            _sfxSource.SafeDestroy();
            _sfxSource = null;
            _audioServiceObject.SafeDestroy();
            _audioServiceObject = null;
        }

        public async UniTask PlayBgmAsync(string assetName, CancellationToken token = default)
        {
            if (_bgmSource == null)
                return;

            var audioClip = await LoadAudioClipAsync(assetName);

            if (_bgmSource.isPlaying)
            {
                await _bgmSource.DOFade(0f, 0.5f).SetUpdate(true).ToUniTask(cancellationToken: token);
            }

            _bgmSource.Stop();
            _bgmSource.clip = audioClip;
            _bgmSource.volume = 0f;
            _bgmSource.mute = false;
            _bgmSource.loop = true;
            _bgmSource.Play();
            await _bgmSource.DOFade(_bgmVolume, _bgmFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
        }

        public async UniTask StopBgmAsync(CancellationToken token = default)
        {
            if (_bgmSource.isPlaying)
            {
                await _bgmSource.DOFade(0f, _bgmFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
            }

            _bgmSource.Stop();
        }

        public async UniTask PlayVoiceAsync(string assetName, CancellationToken token = default)
        {
            if (_voiceSource == null)
                return;

            var audioClip = await LoadAudioClipAsync(assetName);

            if (_voiceSource.isPlaying)
                await _voiceSource.DOFade(0f, _voiceFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);

            _voiceSource.Stop();
            _voiceSource.volume = _voiceVolume;
            _voiceSource.mute = false;
            _voiceSource.loop = false;
            _voiceSource.PlayOneShot(audioClip);
            await UniTask.Delay(TimeSpan.FromSeconds(audioClip.length), DelayType.Realtime, cancellationToken: token);
        }

        public async UniTask PlaySoundEffectAsync(string assetName, CancellationToken token = default)
        {
            if (_sfxSource == null)
                return;

            var audioClip = await LoadAudioClipAsync(assetName);

            if (_sfxSource.isPlaying)
            {
                await _sfxSource.DOFade(0f, _sfxFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
            }

            _sfxSource.Stop();
            _sfxSource.volume = _sfxVolume;
            _sfxSource.mute = false;
            _sfxSource.loop = false;
            _sfxSource.PlayOneShot(audioClip);
            await UniTask.Delay(TimeSpan.FromSeconds(audioClip.length), DelayType.Realtime, cancellationToken: token);
        }

        public UniTask PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default)
        {
            switch (audioCategory)
            {
                case AudioCategory.Bgm:
                    return PlayBgmAsync(audioName, token);
                case AudioCategory.Voice:
                    return PlayVoiceAsync(audioName, token);
                case AudioCategory.SoundEffect:
                    return PlaySoundEffectAsync(audioName, token);
            }

            return UniTask.CompletedTask;
        }

        public UniTask PlayAsync(int audioId, CancellationToken token = default)
        {
            var audioMaster = MemoryDatabase.AudioMasterTable.FindById(audioId);
            var audioCategory = (AudioCategory)audioMaster.AudioCategory;
            var audioName = audioMaster.AssetName;
            return PlayAsync(audioCategory, audioName, token);
        }

        public async UniTask PlayAsync(int[] audioIds, CancellationToken token = default)
        {
            foreach (var audioId in audioIds)
            {
                await PlayAsync(audioId, token);
            }
        }

        public async UniTask PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default)
        {
            var categories = Enum.GetValues(typeof(AudioCategory)).Cast<int>().ToHashSet();
            var byCategory = MemoryDatabase.AudioPlayTagsMasterTable.FindByAudioPlayTag((int)audioPlayTag)
                .Select(x =>
                {
                    if (!MemoryDatabase.AudioMasterTable.TryFindById(x.AudioId, out var audioMaster))
                        return (0, null);

                    if (!categories.Contains(audioMaster.AudioCategory))
                        return (0, null);

                    return (audioMaster.AudioCategory, audioMaster.AssetName);
                })
                .Where(x => x.AudioCategory > 0)
                .OrderBy(x => x.AudioCategory)
                .GroupBy(x => x.AudioCategory, x => x.AssetName)
                .ToDictionary(x => x.Key, x => x.ToArray());
            if (byCategory.Count <= 0)
                return;

            foreach (var (audioCategory, audioNames) in byCategory)
            {
                var index = UnityEngine.Random.Range(0, audioNames.Length);
                var audioName = audioNames[index];
                await PlayAsync((AudioCategory)audioCategory, audioName, token);
            }
        }

        public UniTask PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default)
        {
            var audioNames = MemoryDatabase.AudioPlayTagsMasterTable.FindByAudioPlayTag((int)audioPlayTag)
                .Select(x =>
                {
                    if (!MemoryDatabase.AudioMasterTable.TryFindById(x.AudioId, out var audioMaster))
                        return null;

                    if (audioMaster.AudioCategory != (int)audioCategory)
                        return null;

                    return audioMaster.AssetName;
                })
                .Where(x => x != null)
                .ToArray();
            if (audioNames.Length <= 0)
                return UniTask.CompletedTask;

            var index = UnityEngine.Random.Range(0, audioNames.Length);
            var audioName = audioNames[index];
            return PlayAsync(audioCategory, audioName, token);
        }
    }
}