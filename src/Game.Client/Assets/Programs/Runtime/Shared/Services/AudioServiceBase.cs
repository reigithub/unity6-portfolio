using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Library.Shared.Enums;
using Game.Shared.Extensions;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Shared.Services
{
    /// <summary>
    /// オーディオ再生サービスの共通基底クラス
    /// MVC/MVP両方で継承して使用
    /// </summary>
    public abstract class AudioServiceBase : IAudioService
    {
        private GameObject _audioServiceObject;
        private AudioMixer _audioMixer;
        private AudioSource _bgmSource;
        private AudioSource _voiceSource;
        private AudioSource _sfxSource;

        // AudioMixer - Volume(Db)
        private float _masterVolume;
        private float _bgmVolume;
        private float _voiceVolume;
        private float _sfxVolume;

        // AudioMixer - ExposedParameters
        private const string MasterVolume = "MasterVolume";
        private const string BgmVolume = "BGMVolume";
        private const string VoiceVolume = "VoiceVolume";
        private const string SeVolume = "SEVolume";

        private const float DefaultBgmFadeDuration = 0.25f;
        private const float DefaultVoiceFadeDuration = 0.1f;
        private const float DefaultSfxFadeDuration = 0.1f;

        private const int MaxLoadBgmCount = 2;
        private const int MaxLoadVoiceCount = 10;
        private const int MaxLoadSfxCount = 10;

        private readonly Dictionary<string, AudioClip> _bgmClips = new(MaxLoadBgmCount);
        private readonly Dictionary<string, AudioClip> _voiceClips = new(MaxLoadVoiceCount);
        private readonly Dictionary<string, AudioClip> _sfxClips = new(MaxLoadSfxCount);

        private bool _unloaded;

        protected abstract IAddressableAssetService AssetService { get; }
        protected abstract IMasterDataService MasterDataService { get; }

        public async UniTask LoadAsync()
        {
            _unloaded = false;

            var audioService = await AssetService.InstantiateAsync("AudioService");
            if (audioService == null) return;

            _audioServiceObject = audioService;

            if (audioService.TryGetComponent<AudioServiceComponent>(out var component))
            {
                _audioMixer = component.AudioMixer;
                _bgmSource = component.BgmSource;
                _voiceSource = component.VoiceSource;
                _sfxSource = component.SeSource;
            }

            UnityEngine.Object.DontDestroyOnLoad(_audioServiceObject);
        }

        public void Unload()
        {
            _unloaded = true;
            _bgmSource = null;
            _voiceSource = null;
            _sfxSource = null;
            foreach (var clip in _bgmClips.Values) AssetService.Release(clip);
            _bgmClips.Clear();
            foreach (var clip in _voiceClips.Values) AssetService.Release(clip);
            _voiceClips.Clear();
            foreach (var clip in _sfxClips.Values) AssetService.Release(clip);
            _sfxClips.Clear();
            _audioServiceObject.SafeDestroy();
            _audioServiceObject = null;
        }

        #region Load AudioClip

        private async UniTask<AudioClip> LoadBgmAsync(string assetName, CancellationToken token = default)
        {
            if (_bgmClips.TryGetValue(assetName, out var cached))
                return cached;

            // LoadAndCacheBgmAsync に token を渡さず、待機だけをキャンセルする（2メソッド構成は意図的）。
            // 1メソッドに統合してロードの await にキャンセルを掛けると、キャンセル時に以降の
            // 登録・相殺 Release が実行されず、完走したロードの参照カウントがリークする。
            // 内側を必ず完走させることで、クリップの所有権を常にキャッシュへ移す。
            return await LoadAndCacheBgmAsync(assetName).AttachExternalCancellation(token);
        }

        private async UniTask<AudioClip> LoadAndCacheBgmAsync(string assetName)
        {
            var clip = await AssetService.LoadAssetAsync<AudioClip>(assetName);

            if (_unloaded)
            {
                AssetService.Release(clip);
                throw new OperationCanceledException();
            }

            if (!_bgmClips.TryAdd(assetName, clip))
            {
                AssetService.Release(clip); // 並行ロードで先着があった分の参照カウントを返す
                return _bgmClips[assetName];
            }

            if (_bgmClips.Count > MaxLoadBgmCount)
            {
                (string name, AudioClip old) = _bgmClips.First();
                _bgmClips.Remove(name);
                AssetService.Release(old);
            }

            return clip;
        }

        private async UniTask<AudioClip> LoadVoiceAsync(string assetName, CancellationToken token = default)
        {
            if (_voiceClips.TryGetValue(assetName, out var cached))
                return cached;

            // ロードと登録は中断させず、待機だけをキャンセルする（クリップの所有権は常にキャッシュへ移る）
            return await LoadAndCacheVoiceAsync(assetName).AttachExternalCancellation(token);
        }

        private async UniTask<AudioClip> LoadAndCacheVoiceAsync(string assetName)
        {
            var clip = await AssetService.LoadAssetAsync<AudioClip>(assetName);

            if (_unloaded)
            {
                AssetService.Release(clip);
                throw new OperationCanceledException();
            }

            if (!_voiceClips.TryAdd(assetName, clip))
            {
                AssetService.Release(clip); // 並行ロードで先着があった分の参照カウントを返す
                return _voiceClips[assetName];
            }

            if (_voiceClips.Count > MaxLoadVoiceCount)
            {
                (string name, AudioClip old) = _voiceClips.First();
                _voiceClips.Remove(name);
                AssetService.Release(old);
            }

            return clip;
        }

        private async UniTask<AudioClip> LoadSfxAsync(string assetName, CancellationToken token = default)
        {
            if (_sfxClips.TryGetValue(assetName, out var cached))
                return cached;

            // ロードと登録は中断させず、待機だけをキャンセルする（クリップの所有権は常にキャッシュへ移る）
            return await LoadAndCacheSfxAsync(assetName).AttachExternalCancellation(token);
        }

        private async UniTask<AudioClip> LoadAndCacheSfxAsync(string assetName)
        {
            var clip = await AssetService.LoadAssetAsync<AudioClip>(assetName);

            if (_unloaded)
            {
                AssetService.Release(clip);
                throw new OperationCanceledException();
            }

            if (!_sfxClips.TryAdd(assetName, clip))
            {
                AssetService.Release(clip); // 並行ロードで先着があった分の参照カウントを返す
                return _sfxClips[assetName];
            }

            if (_sfxClips.Count > MaxLoadSfxCount)
            {
                (string name, AudioClip old) = _sfxClips.First();
                _sfxClips.Remove(name);
                AssetService.Release(old);
            }

            return clip;
        }

        #endregion

        public async UniTask PlayBgmAsync(string assetName, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(assetName) || _bgmSource == null)
                return;

            var audioClip = await LoadBgmAsync(assetName, token);

            if (_bgmSource.isPlaying)
                await _audioMixer.DOSetFloat(BgmVolume, 0f, DefaultBgmFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);

            _bgmSource.Stop();
            _bgmSource.clip = audioClip;
            _bgmSource.volume = 1f;
            _bgmSource.mute = false;
            _bgmSource.loop = true;
            _bgmSource.Play();
            await _audioMixer.DOSetFloat(BgmVolume, _bgmVolume, DefaultBgmFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);
        }

        public async UniTask StopBgmAsync(CancellationToken token = default)
        {
            if (_bgmSource.isPlaying)
                await _audioMixer.DOSetFloat(BgmVolume, 0f, DefaultBgmFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);

            _bgmSource.Stop();
        }

        public async UniTask PlayVoiceAsync(string assetName, CancellationToken token = default)
        {
            if (_voiceSource == null)
                return;

            var audioClip = await LoadVoiceAsync(assetName, token);

            if (_voiceSource.isPlaying)
                await _audioMixer.DOSetFloat(VoiceVolume , 0f, DefaultVoiceFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);

            _voiceSource.Stop();
            _voiceSource.volume = 1f;
            _voiceSource.mute = false;
            _voiceSource.loop = false;
            _voiceSource.PlayOneShot(audioClip);
            await UniTask.Delay(TimeSpan.FromSeconds(audioClip.length), DelayType.Realtime, cancellationToken: token);
        }

        public async UniTask PlaySfxAsync(string assetName, CancellationToken token = default)
        {
            if (_sfxSource == null)
                return;

            var audioClip = await LoadSfxAsync(assetName, token);

            if (_sfxSource.isPlaying)
                await _audioMixer.DOSetFloat(SeVolume, 0f, DefaultSfxFadeDuration).SetUpdate(true).ToUniTask(cancellationToken: token);

            _sfxSource.Stop();
            _sfxSource.volume = 1f;
            _sfxSource.mute = false;
            _sfxSource.loop = false;
            _sfxSource.PlayOneShot(audioClip);
            await UniTask.Delay(TimeSpan.FromSeconds(audioClip.length), DelayType.Realtime, cancellationToken: token);
        }

        public async UniTask PlaySfxOneShotAsync(string assetName, CancellationToken token = default)
        {
            if (_sfxSource == null || string.IsNullOrEmpty(assetName))
                return;

            AudioClip clip = await LoadSfxAsync(assetName, token);

            if (_sfxSource == null || clip == null)
                return;

            _sfxSource.PlayOneShot(clip);
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
                    return PlaySfxAsync(audioName, token);
            }

            return UniTask.CompletedTask;
        }

        public UniTask PlayAsync(int audioId, CancellationToken token = default)
        {
            var audioMaster = MasterDataService.MemoryDatabase.AudioMasterTable.FindById(audioId);
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
            var byCategory = MasterDataService.MemoryDatabase.AudioPlayTagsMasterTable.FindByAudioPlayTag((int)audioPlayTag)
                .Select(x =>
                {
                    if (!MasterDataService.MemoryDatabase.AudioMasterTable.TryFindById(x.AudioId, out var audioMaster))
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
            var audioNames = MasterDataService.MemoryDatabase.AudioPlayTagsMasterTable.FindByAudioPlayTag((int)audioPlayTag)
                .Select(x =>
                {
                    if (!MasterDataService.MemoryDatabase.AudioMasterTable.TryFindById(x.AudioId, out var audioMaster))
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

        public void SetVolume(float master, float bgm, float voice, float sfx)
        {
            _masterVolume = PaToDb(master / 10f);
            _bgmVolume = PaToDb(bgm / 10f);
            _voiceVolume = PaToDb(voice / 10f);
            _sfxVolume = PaToDb(sfx / 10f);

            if (_audioMixer != null)
            {
                _audioMixer.SetFloat(MasterVolume, _masterVolume);
                _audioMixer.SetFloat(BgmVolume, _bgmVolume);
                _audioMixer.SetFloat(VoiceVolume, _voiceVolume);
                _audioMixer.SetFloat(SeVolume, _sfxVolume);
            }
        }

        /// <summary>
        /// デシベル変換
        /// 0, 1, 10の音圧→-80, 0, 20のデシベル
        /// </summary>
        private float PaToDb(float volume)
        {
            var clamped = Mathf.Clamp(volume, 0.0001f, 10f);
            return 20f * Mathf.Log10(clamped);
        }

        /// <summary>
        /// 音圧変換
        /// -80, 0, 20のデシベル→0, 1, 10の音圧
        /// </summary>
        private float DbToPa(float db)
        {
            var clamped = Mathf.Clamp(db, -80f, 20f);
            return Mathf.Pow(10f, clamped / 20f);
        }
    }
}
