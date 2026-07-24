using DG.Tweening;
using Game.Core.Services;
using Game.Horror.Signals;
using R3;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// 被弾時の全画面赤フラッシュ。OverlayCanvas/DamageFlash にアタッチし、
    /// MessagePipe で <see cref="HorrorSignals.Player.Damaged"/> を購読して自律駆動する（HorrorDamageSpawner と同型）。
    /// 死亡直後の PauseTime（GameOverDialog）中もフェードアウトが止まらないよう SetUpdate(true) で再生する。
    /// </summary>
    public class HorrorDamageFlashView : MonoBehaviour
    {
        [Tooltip("フェード用 CanvasGroup（全画面赤 Image と同一オブジェクトに配線）")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("フラッシュのピーク時アルファ")]
        [SerializeField] private float _peakAlpha = 0.35f;

        [SerializeField] private float _fadeInSeconds = 0.05f;
        [SerializeField] private float _fadeOutSeconds = 0.4f;

        private Sequence _sequence;

        private void Awake()
        {
            // 初期化前の一瞬の表示を防ぐ
            _canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            // 被弾イベントを購読（GameObject 破棄時に自動解放）
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            messagePipeService.Subscribe<HorrorSignals.Player.Damaged>(OnDamaged).AddTo(this);
        }

        private void OnDamaged(HorrorSignals.Player.Damaged e)
        {
            // 連続被弾は Kill して作り直す（現在 alpha から滑らかに再フラッシュ。EquipmentsView.Show と同イディオム）
            if (_sequence != null && _sequence.IsActive()) _sequence.Kill();

            _sequence = DOTween.Sequence()
                .Append(_canvasGroup.DOFade(_peakAlpha, _fadeInSeconds))
                .Append(_canvasGroup.DOFade(0f, _fadeOutSeconds))
                .SetUpdate(true);
        }

        private void OnDestroy()
        {
            if (_sequence != null && _sequence.IsActive()) _sequence.Kill();
        }
    }
}
