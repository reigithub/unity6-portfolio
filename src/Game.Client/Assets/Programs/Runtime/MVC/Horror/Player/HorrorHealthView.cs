using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Player
{
    /// <summary>
    /// HP HUD。OverlayCanvas/Hp にアタッチし、残 HP をゲージ（Slider）で表示する。
    /// 被弾・発砲・リロード・エイム中に表示し、一定時間後にフェードアウトする。演出はフレーム駆動
    /// （<see cref="HorrorPlayerController"/> の各ステート Update から UpdateAimPose 経由で毎フレーム駆動され、DOTween は使わない）。
    /// 値の反映（UpdateHealth）と表示キック（Notify）は分離されており、起動時の復元は値のみ設定して非表示のまま開始する。
    /// </summary>
    public class HorrorHealthView : MonoBehaviour
    {
        [Tooltip("HUD 全体のフェード用 CanvasGroup")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("HP ゲージの Slider（Hp/HpGauge/Slider を配線）")]
        [SerializeField] private Slider _hpGauge;

        [Tooltip("表示時のフェードイン秒数")]
        [SerializeField] private float _fadeInSeconds = 0.15f;

        [Tooltip("最後の表示キックから不透明を保持する秒数")]
        [SerializeField] private float _holdSeconds = 2f;

        [Tooltip("保持後にフェードアウトする秒数")]
        [SerializeField] private float _fadeOutSeconds = 0.5f;

        private float _holdElapsed;

        private void Awake()
        {
            // 初期化前の一瞬の表示を防ぐため、非表示から開始する
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _holdElapsed = _holdSeconds;
        }

        /// <summary>
        /// HUD の表示状態を毎フレーム更新する。<see cref="HorrorPlayerController"/> の
        /// 各ステート Update から UpdateAimPose 経由で毎フレーム呼ばれる。
        /// </summary>
        /// <param name="keepVisible">エイム中・リロード中の表示維持。</param>
        public void UpdatePose(bool keepVisible)
        {
            if (keepVisible) _holdElapsed = 0f;
            else _holdElapsed += Time.deltaTime;

            if (_canvasGroup == null) return;

            var target = CalculateTargetAlpha(keepVisible, _holdElapsed, _holdSeconds);
            var dt = Time.deltaTime;
            var rate = target > _canvasGroup.alpha
                ? dt / Mathf.Max(_fadeInSeconds, 0.0001f)
                : dt / Mathf.Max(_fadeOutSeconds, 0.0001f);
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, rate);
        }

        /// <summary>被弾・発砲・空撃ち・リロード時の表示キック。保持タイマーをリセットして表示を開始/延長する。</summary>
        public void Notify()
        {
            _holdElapsed = 0f;
        }

        /// <summary>残 HP をゲージへ即時反映する。表示キックはしない（起動時の復元で非表示のまま値を設定できる）。</summary>
        public void UpdateHealth(int current, int max)
        {
            if (_hpGauge != null) _hpGauge.value = CalculateGaugeValue(current, max);
        }

        /// <summary>目標アルファを算出する。表示維持中または保持時間内は 1、それ以外は 0。</summary>
        internal static float CalculateTargetAlpha(bool keepVisible, float holdElapsed, float holdSeconds)
            => keepVisible || holdElapsed < holdSeconds ? 1f : 0f;

        /// <summary>
        /// ゲージ値（0〜1）を算出する。max が 0 以下はゼロ除算を避けて 0、範囲外はクランプする。
        /// </summary>
        internal static float CalculateGaugeValue(int current, int max)
            => max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
    }
}
