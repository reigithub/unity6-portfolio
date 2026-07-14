using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// ダメージ数値ポップアップ。HorrorDamagePopup.prefab のルートにアタッチし、
    /// OverlayCanvas/DamagePopups 配下にプール生成される。演出は DOTween、位置追従は LateUpdate で行う。
    /// </summary>
    public class HorrorDamageView : MonoBehaviour
    {
        [Tooltip("フェード用 CanvasGroup")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("ダメージ数値の表示テキスト")]
        [SerializeField] private TMP_Text _damageText;

        [Tooltip("表示中に上昇するスクリーン距離（px）")]
        [SerializeField] private float _riseHeight = 60f;

        [Tooltip("上昇にかける秒数")]
        [SerializeField] private float _riseSeconds = 0.7f;

        [Tooltip("フェードアウト開始までの遅延秒数")]
        [SerializeField] private float _fadeDelaySeconds = 0.2f;

        [Tooltip("フェードアウト秒数")]
        [SerializeField] private float _fadeOutSeconds = 0.5f;

        // カメラ背後判定時の退避先（画面外へ飛ばして描画を隠す）
        private static readonly Vector3 _offscreenPosition = new(-10000f, -10000f, 0f);

        private RectTransform _rectTransform;
        private Camera _camera;
        private Vector3 _worldPosition;
        private float _riseOffset;
        private Sequence _sequence;
        private Action<HorrorDamageView> _onFinished;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// ダメージポップアップの表示を開始する。プールから取得した直後に呼ぶこと。
        /// 表示中に再度呼び出された場合は前回の演出を破棄し、現在の状態から再生し直す。
        /// </summary>
        /// <param name="camera">スクリーン座標算出に使うカメラ</param>
        /// <param name="worldPosition">ヒット位置（ワールド座標）。LateUpdate で毎フレーム追従する</param>
        /// <param name="damage">表示するダメージ値</param>
        /// <param name="onFinished">演出完了時に呼ばれるコールバック（プール返却に使用）</param>
        public void Play(Camera camera, Vector3 worldPosition, int damage, Action<HorrorDamageView> onFinished)
        {
            _camera = camera;
            _worldPosition = worldPosition;
            _onFinished = onFinished;
            _damageText.SetText("{0}", damage);

            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();

            _riseOffset = 0f;
            _canvasGroup.alpha = 1f;

            // 前回位置での1フレーム表示を防ぐため、演出開始前に即時1回スクリーン位置を反映する
            UpdateScreenPosition();

            _sequence = DOTween.Sequence()
                .Append(DOTween.To(() => _riseOffset, v => _riseOffset = v, _riseHeight, _riseSeconds))
                .Insert(_fadeDelaySeconds, _canvasGroup.DOFade(0f, _fadeOutSeconds))
                .SetLink(gameObject)
                .OnComplete(() => _onFinished?.Invoke(this));
            // SetUpdate(true) は付けない：ポーズ中（timeScale=0）は演出も停止させる仕様
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            UpdateScreenPosition();
        }

        // ワールド座標をスクリーン座標へ変換し RectTransform に反映する。カメラ背後なら画面外へ退避させる。
        private void UpdateScreenPosition()
        {
            var screenPoint = _camera.WorldToScreenPoint(_worldPosition);
            _rectTransform.position = TryCalculateScreenPosition(screenPoint, _riseOffset, out var position)
                ? position
                : _offscreenPosition;
        }

        /// <summary>
        /// スクリーン座標（<see cref="Camera.WorldToScreenPoint(Vector3)"/> の結果）と上昇オフセットから
        /// 表示位置を算出する。screenPoint.z が負（カメラ背後）の場合は非表示とし false を返す。
        /// </summary>
        /// <param name="screenPoint">WorldToScreenPoint で得たスクリーン座標</param>
        /// <param name="riseOffset">上昇演出による y オフセット（px）</param>
        /// <param name="position">算出結果の表示位置（false の場合は既定値）</param>
        /// <returns>表示可能なら true、カメラ背後で非表示にすべきなら false</returns>
        public static bool TryCalculateScreenPosition(Vector3 screenPoint, float riseOffset, out Vector3 position)
        {
            if (screenPoint.z < 0f)
            {
                position = default;
                return false;
            }

            position = new Vector3(screenPoint.x, screenPoint.y + riseOffset, 0f);
            return true;
        }

        private void OnDestroy()
        {
            // SetLink 済みだが、破棄タイミング競合に備えた二重の安全策（Kill では OnComplete は発火しない）
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
        }
    }
}
