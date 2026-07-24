using TMPro;
using UnityEngine;

namespace Game.Horror.Player
{
    public enum HorrorAmmoViewMode
    {
        Hidden,
        MagazineAndReserve,
        CountOnly,
    }

    /// <summary>
    /// 残弾 HUD。OverlayCanvas/Ammo にアタッチし、装備中武器の弾倉残弾/予備弾数（弾薬を使わない武器は所持数のみ）を表示する。
    /// 発砲・リロード・エイム中に表示し、一定時間後にフェードアウトする。演出はフレーム駆動
    /// （<see cref="HorrorPlayerController"/> の各ステート Update から UpdateAimPose 経由で毎フレーム駆動され、DOTween は使わない）。
    /// </summary>
    public class HorrorAmmoView : MonoBehaviour
    {
        [Tooltip("HUD 全体のフェード用 CanvasGroup")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("弾倉残弾（CountOnly では所持数）の表示テキスト")]
        [SerializeField] private TMP_Text _magazineText;

        [Tooltip("区切り「/」の表示テキスト")]
        [SerializeField] private TMP_Text _separatorText;

        [Tooltip("予備弾数の表示テキスト")]
        [SerializeField] private TMP_Text _reserveText;

        [Tooltip("表示時のフェードイン秒数")]
        [SerializeField] private float _fadeInSeconds = 0.15f;

        [Tooltip("最後の表示キックから不透明を保持する秒数")]
        [SerializeField] private float _holdSeconds = 2f;

        [Tooltip("保持後にフェードアウトする秒数")]
        [SerializeField] private float _fadeOutSeconds = 0.5f;

        [Tooltip("通常の文字色")]
        [SerializeField] private Color _normalColor = Color.white;

        [Tooltip("弾倉満タン時の弾倉側文字色")]
        [SerializeField] private Color _fullMagazineColor = Color.green;

        [Tooltip("予備 0 時の予備側文字色")]
        [SerializeField] private Color _emptyReserveColor = Color.red;

        private float _holdElapsed;

        // 直近反映済みの表示内容（変化検出用。未初期化を検出できるよう bool で管理する）
        private bool _initialized;
        private HorrorAmmoViewMode _lastMode;
        private int _lastMagazine = int.MinValue;
        private int _lastMagazineSize = int.MinValue;
        private int _lastReserve = int.MinValue;

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
        public void UpdatePose(HorrorAmmoViewMode mode, bool keepVisible, int magazine, int magazineSize, int reserve)
        {
            if (keepVisible) _holdElapsed = 0f;
            else _holdElapsed += Time.deltaTime;

            var target = CalculateTargetAlpha(mode, keepVisible, _holdElapsed, _holdSeconds);

            if (_canvasGroup != null)
            {
                var dt = Time.deltaTime;
                var rate = target > _canvasGroup.alpha
                    ? dt / Mathf.Max(_fadeInSeconds, 0.0001f)
                    : dt / Mathf.Max(_fadeOutSeconds, 0.0001f);
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, target, rate);
            }

            if (_initialized && mode == _lastMode && magazine == _lastMagazine && magazineSize == _lastMagazineSize && reserve == _lastReserve) return;

            _initialized = true;
            _lastMode = mode;
            _lastMagazine = magazine;
            _lastMagazineSize = magazineSize;
            _lastReserve = reserve;

            if (_magazineText != null)
            {
                _magazineText.SetText("{0}", magazine);
                _magazineText.color = CalculateMagazineColor(mode, magazine, magazineSize, _fullMagazineColor, _normalColor);
            }

            var showReserve = mode == HorrorAmmoViewMode.MagazineAndReserve;
            if (_separatorText != null) _separatorText.gameObject.SetActive(showReserve);
            if (_reserveText != null)
            {
                _reserveText.gameObject.SetActive(showReserve);
                if (showReserve)
                {
                    _reserveText.SetText("{0}", reserve);
                    _reserveText.color = CalculateReserveColor(reserve, _emptyReserveColor, _normalColor);
                }
            }
        }

        /// <summary>発砲・空撃ち・リロード時の表示キック。保持タイマーをリセットして表示を開始/延長する。</summary>
        public void Notify()
        {
            _holdElapsed = 0f;
        }

        /// <summary>装備状態と弾薬設定から HUD の表示内容を解決する。未装備は None、弾薬を使わない武器は所持数のみ。</summary>
        internal static HorrorAmmoViewMode ResolveViewMode(bool hasWeapon, int ammoItemId)
        {
            if (!hasWeapon) return HorrorAmmoViewMode.Hidden;
            return ammoItemId > 0 ? HorrorAmmoViewMode.MagazineAndReserve : HorrorAmmoViewMode.CountOnly;
        }

        /// <summary>目標アルファを算出する。None は常に 0、表示維持中または保持時間内は 1、それ以外は 0。</summary>
        internal static float CalculateTargetAlpha(HorrorAmmoViewMode mode, bool keepVisible, float holdElapsed, float holdSeconds)
        {
            if (mode == HorrorAmmoViewMode.Hidden) return 0f;
            return keepVisible || holdElapsed < holdSeconds ? 1f : 0f;
        }

        /// <summary>弾倉側の文字色を算出する（満タンで強調色）。CountOnly（所持数表示）は常に通常色。</summary>
        internal static Color CalculateMagazineColor(HorrorAmmoViewMode mode, int magazine, int magazineSize, Color full, Color normal)
        {
            return mode == HorrorAmmoViewMode.MagazineAndReserve && magazineSize > 0 && magazine >= magazineSize ? full : normal;
        }

        /// <summary>予備側の文字色を算出する（予備切れで警告色）。</summary>
        internal static Color CalculateReserveColor(int reserve, Color empty, Color normal)
            => reserve <= 0 ? empty : normal;
    }
}
