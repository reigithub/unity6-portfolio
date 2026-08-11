using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
using Game.Horror.Enums;
using Game.Horror.Services.Interfaces;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// UI操作効果音の再生と、種別横断の再生規則を所有するサービス
    /// </summary>
    public class HorrorUISoundService : IHorrorUISoundService
    {
        // 高速ホバー等でフレームをまたいで連続する Select を間引く最小発音間隔
        //（秒。unscaled: ポーズ中の画面でも経過する。0.1 は初期値で、実機の体感で調整する前提）
        private const float SelectMinInterval = 0.1f;

        private readonly IAudioService _audioService;
        private CancellationTokenSource _cts;
        private readonly Dictionary<HorrorUISoundType, int> _lastPlayFrames = new(); // 種別ごとの最終再生フレーム。同フレーム同種別の畳み込みと、Select 劣後（非 Select 再生の有無）の判定に使う
        private float _lastSelectPlayTime = float.NegativeInfinity;
        private string _pendingSelectSeAssetName; // 非 null = 同一フレーム内で Select 予約済み

        public HorrorUISoundService(IAudioService audioService)
        {
            _audioService = audioService;
        }

        public void Startup()
        {
            _cts = new CancellationTokenSource();
        }

        public void Shutdown()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Play(HorrorUISoundType type, string seAssetName)
        {
            if (string.IsNullOrEmpty(seAssetName)) return;

            if (type == HorrorUISoundType.Select)
            {
                RequestSelect(seAssetName);
                return;
            }

            // 同一操作が複数経路（ClickMarker と ValueMarker 等）から同種別を要求した場合の重ね録り防止。
            if (_lastPlayFrames.TryGetValue(type, out var lastFrame) && lastFrame == Time.frameCount)
                return;
            _lastPlayFrames[type] = Time.frameCount;

            PlayCore(seAssetName);
        }

        public void PlayCancelSfx()
            => Play(HorrorUISoundType.Cancel, HorrorAudioConstants.UICancelSfx);

        // Select は即時再生せず同一フレーム末尾で確定する。
        // タブ切替では TabGroup.ChangeTab がフォーカス移動（→Select要求）の後に OnTabChanged（→TabChanged要求）を
        // 発火するため、到着順に依存する抑制では Select が先着してすり抜ける。フレーム末尾判定なら順序に依存しない。
        // 同一フレームの Select 重複（ホバー+ナビ等）もここで1回に畳まれる。
        private void RequestSelect(string seAssetName)
        {
            if (_pendingSelectSeAssetName != null) return;
            _pendingSelectSeAssetName = seAssetName;
            FlushSelectAtFrameEnd().Forget();
        }

        private async UniTaskVoid FlushSelectAtFrameEnd()
        {
            var frame = Time.frameCount;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, _cts.Token);

            var seAssetName = _pendingSelectSeAssetName;
            _pendingSelectSeAssetName = null;
            if (HasPriorityPlayAt(frame)) return;

            var now = Time.unscaledTime;
            if (now - _lastSelectPlayTime < SelectMinInterval) return;

            _lastSelectPlayTime = now;
            PlayCore(seAssetName);
        }

        private bool HasPriorityPlayAt(int frame)
        {
            foreach (var playFrame in _lastPlayFrames.Values)
                if (playFrame == frame) return true;
            return false;
        }

        private void PlayCore(string seAssetName)
        {
            _audioService.PlaySfxOneShotAsync(seAssetName, _cts.Token).Forget();
        }
    }
}
