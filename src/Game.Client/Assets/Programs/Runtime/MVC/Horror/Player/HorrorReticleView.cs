using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// エイム連動レティクル。OverlayCanvas/Reticle にアタッチし、エイム中はドット・エイム解除はセグメント拡大→保持→フェードの表示を行う。
    /// 演出はフレーム駆動（<see cref="HorrorPlayerController"/> の Update から毎フレーム駆動され、DOTween は使わない）。
    /// フェード途中の再エイムや発砲キックを、その時点の連続量から滑らかに合成するため。
    /// </summary>
    public class HorrorReticleView : MonoBehaviour
    {
        [Tooltip("ドット表示用 CanvasGroup（Dot にアタッチされたもの）")]
        [SerializeField] private CanvasGroup _dotGroup;

        [Tooltip("セグメント表示用 CanvasGroup（Segments にアタッチされたもの）")]
        [SerializeField] private CanvasGroup _segmentsGroup;

        [Tooltip("セグメントの RectTransform。順序固定: 0=上 / 1=下 / 2=左 / 3=右")]
        [SerializeField] private RectTransform[] _segments;

        [Tooltip("エイム開始時にセグメントが中心へ収縮する秒数")]
        [SerializeField] private float _contractSeconds = 0.15f;

        [Tooltip("エイム解除時にセグメントが外側へ拡大する秒数")]
        [SerializeField] private float _spreadSeconds = 0.3f;

        [Tooltip("拡大完了後、不透明のまま保持する秒数")]
        [SerializeField] private float _holdSeconds = 1f;

        [Tooltip("保持後にフェードアウトする秒数")]
        [SerializeField] private float _fadeSeconds = 0.5f;

        [Tooltip("拡大時の中心からのセグメント距離（px）")]
        [SerializeField] private float _expandedDistance = 24f;

        [Tooltip("発砲キックで加算されるセグメント距離（px）")]
        [SerializeField] private float _fireKickDistance = 12f;

        [Tooltip("発砲キックが収まるまでの秒数")]
        [SerializeField] private float _fireRecoverSeconds = 0.25f;

        /// <summary>
        /// レティクルの表示段階。Hidden から Contracting（収縮）→ Dot（構え中）、
        /// 解除で Spreading（拡大）→ Holding（保持）→ Fading（フェード）→ Hidden と巡回する。
        /// </summary>
        private enum Phase
        {
            Hidden,
            Contracting,
            Dot,
            Spreading,
            Holding,
            Fading,
        }

        private Phase _phase = Phase.Hidden;
        private float _spread;
        private float _phaseElapsed;
        private float _kick;

        /// <summary>
        /// 非エイム時もドットを常時表示する（将来オプション設定から反映する拡張点。既定 false）。
        /// </summary>
        public bool AlwaysShowDot { get; set; }

        // _segments の並び 0=上/1=下/2=左/3=右 と対応する方向テーブル
        private static readonly Vector2[] _segmentDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        private void Awake()
        {
            // 初期化前の一瞬の表示を防ぐため、両グループとも非表示から開始する
            if (_dotGroup != null) _dotGroup.alpha = 0f;
            if (_segmentsGroup != null) _segmentsGroup.alpha = 0f;
        }

        /// <summary>
        /// レティクルの表示状態を毎フレーム更新する。<see cref="HorrorPlayerController"/> の Update から
        /// UpdateAimPose と同じタイミングで毎フレーム呼ばれる。
        /// </summary>
        /// <param name="isAiming">現在エイム中か（HOLD 判定の二値）。</param>
        public void UpdatePose(bool isAiming)
        {
            var dt = Time.deltaTime;

            // 段階遷移判定
            if (isAiming && (_phase == Phase.Hidden || _phase == Phase.Spreading || _phase == Phase.Holding || _phase == Phase.Fading))
            {
                if (_phase == Phase.Hidden) _spread = 1f; // 非表示からの構えは拡大位置から収縮を始める
                _phase = Phase.Contracting;
            }
            else if (!isAiming && (_phase == Phase.Contracting || _phase == Phase.Dot))
            {
                _phase = Phase.Spreading;
                _phaseElapsed = 0f;
            }

            // 段階進行（0 除算にならないよう秒数は最小値でガード）
            switch (_phase)
            {
                case Phase.Contracting:
                    _spread = Mathf.MoveTowards(_spread, 0f, dt / Mathf.Max(_contractSeconds, 0.0001f));
                    if (_spread <= 0f) _phase = Phase.Dot;
                    break;
                case Phase.Spreading:
                    _spread = Mathf.MoveTowards(_spread, 1f, dt / Mathf.Max(_spreadSeconds, 0.0001f));
                    if (_spread >= 1f)
                    {
                        _phase = Phase.Holding;
                        _phaseElapsed = 0f;
                    }
                    break;
                case Phase.Holding:
                    _phaseElapsed += dt;
                    if (_phaseElapsed >= _holdSeconds)
                    {
                        _phase = Phase.Fading;
                        _phaseElapsed = 0f;
                    }
                    break;
                case Phase.Fading:
                    _phaseElapsed += dt;
                    if (_phaseElapsed >= _fadeSeconds) _phase = Phase.Hidden;
                    break;
            }

            _kick = Mathf.MoveTowards(_kick, 0f, dt / Mathf.Max(_fireRecoverSeconds, 0.0001f));

            // 反映
            var master = _phase switch
            {
                Phase.Fading => CalculateFadeAlpha(_phaseElapsed, _fadeSeconds),
                Phase.Hidden => 0f,
                _ => 1f,
            };
            var segPhaseActive = _phase == Phase.Contracting || _phase == Phase.Spreading || _phase == Phase.Holding || _phase == Phase.Fading;

            if (_dotGroup != null) _dotGroup.alpha = CalculateDotAlpha(_phase == Phase.Dot, master, AlwaysShowDot);
            if (_segmentsGroup != null) _segmentsGroup.alpha = CalculateSegmentAlpha(segPhaseActive, master, _kick);

            if (_segments == null) return;
            var distance = CalculateSegmentDistance(_spread, _expandedDistance, _kick, _fireKickDistance);
            for (var i = 0; i < _segments.Length && i < _segmentDirections.Length; i++)
            {
                if (_segments[i] != null) _segments[i].anchoredPosition = _segmentDirections[i] * distance;
            }
        }

        /// <summary>
        /// 発砲キックを開始する。<see cref="HorrorPlayerController.Fire"/> から呼ばれ、
        /// セグメントが一瞬外へ開いて素早く戻る。
        /// </summary>
        public void NotifyFired()
        {
            _kick = 1f;
        }

        /// <summary>
        /// セグメントの中心からの距離を、拡散量（spread）と発砲キック量の合成で算出する。
        /// </summary>
        public static float CalculateSegmentDistance(float spread, float expandedDistance, float kick, float kickDistance)
            => spread * expandedDistance + kick * kickDistance;

        /// <summary>
        /// フェード段階の経過時間から不透明度比率（1→0）を算出する。<paramref name="fadeSeconds"/> が
        /// 0 以下ならゼロ除算を避けて 0 を返す。
        /// </summary>
        public static float CalculateFadeAlpha(float elapsed, float fadeSeconds)
            => fadeSeconds <= 0f ? 0f : Mathf.Clamp01(1f - elapsed / fadeSeconds);

        /// <summary>
        /// ドット表示の不透明度を、ドット段階かどうかと常時表示オプションから算出する。
        /// </summary>
        public static float CalculateDotAlpha(bool isDotPhase, float master, bool alwaysShowDot)
            => Mathf.Clamp01((isDotPhase ? master : 0f) + (alwaysShowDot ? 1f : 0f));

        /// <summary>
        /// セグメント表示の不透明度を、セグメント表示段階かどうかと発砲キック量から算出する
        /// （キック中は表示段階に依らず最低限の不透明度を保証する）。
        /// </summary>
        public static float CalculateSegmentAlpha(bool segPhaseActive, float master, float kick)
            => Mathf.Max(segPhaseActive ? master : 0f, kick);
    }
}
