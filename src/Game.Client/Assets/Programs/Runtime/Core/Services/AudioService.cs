using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MasterData;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Core.Services
{
    public class AudioService : GameService
    {
        private GameServiceReference<MasterDataService> _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.Reference.MemoryDatabase;

        private GameObject _audioService;
        private AudioSource _bgmSource;
        private AudioSource _voiceSource;
        private AudioSource _sfxSource;

        private readonly float _bgmVolume = 0.3f;
        private readonly float _bgmFadeDuration = 0.25f;
        private readonly float _voiceVolume = 1f;
        private readonly float _voiceFadeDuration = 0.1f;
        private readonly float _sfxVolume = 0.7f;
        private readonly float _sfxFadeDuration = 0.1f;

        protected internal override void Startup()
        {
            _audioService = new GameObject(nameof(AudioService));
            _bgmSource = new GameObject("BgmSource").AddComponent<AudioSource>();
            _voiceSource = new GameObject("VoiceSource").AddComponent<AudioSource>();
            _sfxSource = new GameObject("SfxSource").AddComponent<AudioSource>();

            _bgmSource.transform.SetParent(_audioService.transform);
            _voiceSource.transform.SetParent(_audioService.transform);
            _sfxSource.transform.SetParent(_audioService.transform);

            UnityEngine.Object.DontDestroyOnLoad(_audioService);

            base.Startup();
        }

        protected internal override void Shutdown()
        {
            _bgmSource.SafeDestroy();
            _bgmSource = null;
            _voiceSource.SafeDestroy();
            _voiceSource = null;
            _sfxSource.SafeDestroy();
            _sfxSource = null;
            _audioService.SafeDestroy();
            _audioService = null;

            base.Shutdown();
        }

        protected internal override bool AllowResidentOnMemory => true;

        public async Task PlayBgmAsync(string assetName)
        {
            var audioClip = await Addressables.LoadAssetAsync<AudioClip>(assetName);
            if (_bgmSource.isPlaying)
                _bgmSource.DOFade(0f, 0.5f).onComplete += () => { PlayBgmCore(); };
            else
                PlayBgmCore();

            return;

            // TODO: マスターボリュームなどを設定できるオプション画面
            void PlayBgmCore()
            {
                _bgmSource.Stop();
                _bgmSource.clip = audioClip;
                _bgmSource.volume = 0f;
                _bgmSource.mute = false;
                _bgmSource.loop = true;
                _bgmSource.Play();
                _bgmSource.DOFade(_bgmVolume, _bgmFadeDuration); // 一旦うるさいので50%
            }
        }

        public void StopBgm()
        {
            if (_bgmSource.isPlaying)
            {
                _bgmSource.DOFade(0f, _bgmFadeDuration).onComplete += () => { _bgmSource.Stop(); };
            }
        }

        // public void PauseBgm()
        // {
        //     _bgmSource.DOFade(0.1f, 0.1f).onComplete += () => { _bgmSource.Pause(); };
        // }
        //
        // public void ResumeBgm()
        // {
        //     _bgmSource.DOFade(_bgmVolume, 0.1f).onComplete += () => { _bgmSource.UnPause(); };
        // }

        // public IDisposable BgmVolumeFade()
        // {
        //     if (_bgmSource.isPlaying)
        //     {
        //         _bgmSource.DOFade(_bgmVolume / 3f, 0.1f);
        //     }
        //
        //     return Disposable.Create(() =>
        //     {
        //         if (_bgmSource.isPlaying)
        //             _bgmSource.DOFade(_bgmVolume, 0.1f);
        //         else
        //             _bgmSource.DOFade(_bgmVolume, 0.1f).OnComplete(() => _bgmSource.UnPause());
        //     });
        // }

        public async Task PlayVoiceAsync(string assetName, CancellationToken token = default)
        {
            var audioClip = await Addressables.LoadAssetAsync<AudioClip>(assetName);

            // Memo: 今のところ、前回のボイスはフェードアウトして再生するとしているが
            // →再生リクエスト間隔が短いとうるさくなるため、一括で再生間隔を設定する方法を検討
            if (_voiceSource.isPlaying)
                _voiceSource.DOFade(0f, _voiceFadeDuration).onComplete += () => { PlayVoiceCore(); };
            else
                PlayVoiceCore();

            await Task.Delay(TimeSpan.FromSeconds(audioClip.length), token);
            return;

            // Memo: 複数リクエスト、ループなどの制御を検討
            void PlayVoiceCore()
            {
                _voiceSource.Stop();
                _voiceSource.volume = _voiceVolume;
                _voiceSource.mute = false;
                _voiceSource.loop = false;
                _voiceSource.PlayOneShot(audioClip);
            }
        }

        public async Task PlaySoundEffectAsync(string assetName, CancellationToken token = default)
        {
            var audioClip = await Addressables.LoadAssetAsync<AudioClip>(assetName);

            if (_sfxSource.isPlaying)
                _sfxSource.DOFade(0f, _sfxFadeDuration).onComplete += () => { PlaySoundEffectCore(); };
            else
                PlaySoundEffectCore();

            await Task.Delay(TimeSpan.FromSeconds(audioClip.length), token);
            return;

            // Memo: ループなどの制御を検討
            void PlaySoundEffectCore()
            {
                _sfxSource.Stop();
                _sfxSource.volume = _sfxVolume;
                _sfxSource.mute = false;
                _sfxSource.loop = false;
                _sfxSource.PlayOneShot(audioClip);
            }
        }

        public Task PlayAsync(AudioCategory audioCategory, string audioName, CancellationToken token = default)
        {
            switch (audioCategory)
            {
                case AudioCategory.Bgm:
                    return PlayBgmAsync(audioName);
                case AudioCategory.Voice:
                    return PlayVoiceAsync(audioName, token);
                case AudioCategory.SoundEffect:
                    return PlaySoundEffectAsync(audioName, token);
            }

            return Task.CompletedTask;
        }

        public Task PlayAsync(int audioId, CancellationToken token = default)
        {
            var audioMaster = MemoryDatabase.AudioMasterTable.FindById(audioId);
            var audioCategory = (AudioCategory)audioMaster.AudioCategory;
            var audioName = audioMaster.AssetName;
            return PlayAsync(audioCategory, audioName, token);
        }

        public async Task PlayAsync(int[] audioIds, CancellationToken token = default)
        {
            foreach (var audioId in audioIds)
            {
                await PlayAsync(audioId, token);
            }
        }

        /// <summary>
        /// 全カテゴリで再生できるものを流す
        /// </summary>
        public async Task PlayRandomOneAsync(AudioPlayTag audioPlayTag, CancellationToken token = default)
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

        /// <summary>
        /// 特定カテゴリで再生できるものを流す
        /// </summary>
        public Task PlayRandomOneAsync(AudioCategory audioCategory, AudioPlayTag audioPlayTag, CancellationToken token = default)
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
                return Task.CompletedTask;

            var index = UnityEngine.Random.Range(0, audioNames.Length);
            var audioName = audioNames[index];
            return PlayAsync(audioCategory, audioName, token);
        }
    }
}