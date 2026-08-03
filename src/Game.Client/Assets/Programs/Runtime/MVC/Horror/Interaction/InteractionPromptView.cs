using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// インタラクト対象の提示をスクリーン空間（Overlay Canvas）で表示するプール貸出プロンプト。
    /// <see cref="InteractionPromptPool"/> が生成・管理し、貸出（<see cref="Bind"/>）～返却（<see cref="Unbind"/>）の
    /// 間だけ特定の対象（<see cref="InteractableBase"/>）の表示を担う。
    /// Overlay Canvas 上で <see cref="Camera.WorldToScreenPoint(Vector3)"/> による投影のみを行い、
    /// ワールド空間実装（<see cref="WorldSpaceInteractionPromptView"/>）で必要だったビルボード回転・距離比例スケールは不要
    /// （ScreenSpace は Canvas の性質上、表示サイズが距離に依らず自動的に一定になるため）。
    /// Discoverable はカメラ背後（投影 z&lt;=0）で CanvasGroup の alpha を 0 にして非表示にする。
    /// Actionable は視界外・カメラ背後でも取得可能（検出器の近接フォールバック）なため、
    /// 画面端へのクランプと方向矢印（<see cref="_clampArrow"/>）で常時表示する。
    /// （貸出中でない間に通知が届いても <see cref="_master"/> が null のため無視される）。
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("表示/非表示のフェード制御用 CanvasGroup（カメラ背後判定時の非表示にも使用）")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Tooltip("実行可能（インタラクトできる）状態の見た目")]
        [SerializeField] private GameObject _actionableView;

        [Tooltip("発見可能（対象だと分かる）状態の見た目")]
        [SerializeField] private GameObject _discoverableView;

        [Header("Input Controls")]
        [SerializeField] private TextMeshProUGUI _interactionText;
        [SerializeField] private TextMeshProUGUI _inputBindingText;
        [SerializeField] private Image _inputActionIcon;

        [SerializeField] private GameObject _inputTypeRoot;
        [SerializeField] private TextMeshProUGUI _inputTypeText;

        [Tooltip("Hold 長押しの進捗を示す円形ゲージ（Image: Type=Filled / FillMethod=Radial360）。押下中のみ表示する")]
        [SerializeField] private Image _holdGauge;

        [Header("Target Info")]
        [SerializeField] private GameObject _targetInfoRoot;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _countText;

        [Header("Offscreen Clamp")]
        [Tooltip("画面端クランプ時の方向矢印（上向きスプライト基準）。未配線でも動作する（矢印なしでクランプのみ）")]
        [SerializeField] private RectTransform _clampArrow;

        [Tooltip("画面端クランプの余白（デザイン px）。CanvasScaler の scaleFactor は実行時に乗算される")]
        [SerializeField] private Vector2 _clampMargin = new(140f, 120f);

        [Tooltip("矢印のプロンプト中心からのオフセット（デザイン px）")]
        [SerializeField] private float _arrowOffset = 90f;

        private IInputSystemService _inputService;
        private IInputActionIconService _inputActionIconService;
        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;

        private RectTransform _rectTransform;
        private HorrorInteractionMaster _master;
        private Transform _anchor;
        private Camera _viewCamera;
        private bool _interactionToggle;
        private InteractionTargetInfo _targetInfo;
        private InteractionState _state;
        private Canvas _rootCanvas;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        /// <summary>
        /// プールがインスタンス生成直後に1回だけ呼ぶ。ロケール変更・入力バインド変更の購読をここで常時張り、
        /// 貸出中（<see cref="_master"/> が非 null）の間のみ反映する。
        /// </summary>
        public void Initialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inputActionIconService = GameServiceManager.Resolve<IInputActionIconService>();
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();

            // クランプ余白のデザイン px → 実ピクセル換算用。プールが Canvas 配下へ生成するためここで解決できる
            //（Canvas なし環境＝テスト雛形では null のまま等倍フォールバック）
            _rootCanvas = GetComponentInParent<Canvas>();

            _localizationService.OnLocaleChanged
                .Subscribe(_ => { SetInteractionText(); SetTargetNameText(); })
                .AddTo(this);
            _inputService.OnControlSchemeChanged.Subscribe(_ => SetInputBinding()).AddTo(this);
            _inputService.OnDeviceChanged.Subscribe(_ => SetInputBinding()).AddTo(this);
            _inputService.OnBindingChanged
                .Where(x => x == _inputService.Player.Interact)
                .Subscribe(_ => SetInputBinding())
                .AddTo(this);
        }

        /// <summary>
        /// プールからの貸出時に呼ぶ。マスターデータ・アンカー・再インタラクト表示の反映に加え、
        /// Hold ゲージを 0 にリセットして表示を開始する。
        /// 位置の反映は視点カメラを受け取る <see cref="SetState"/> が担う（Bind 直後に同フレームで呼ばれる契約）。
        /// </summary>
        /// <param name="master">表示するインタラクト定義</param>
        /// <param name="anchor">追従先のワールド座標アンカー</param>
        /// <param name="interactionToggle">再インタラクト表示（動詞切替）の初期状態。<see cref="InteractableBase"/> 側のキャッシュから復元される</param>
        public void Bind(HorrorInteractionMaster master, Transform anchor, bool interactionToggle)
        {
            _master = master;
            _anchor = anchor;
            _interactionToggle = interactionToggle;

            gameObject.SetActive(true);

            SetInteractionText();
            SetInputBinding();

            if (_inputTypeRoot != null)
                _inputTypeRoot.SetActive(master.InputType == InteractionInputType.Hold);

            SetHoldProgress(0f);
        }

        /// <summary>
        /// プールへの返却時に呼ぶ。参照をすべて解放し非アクティブ化する。以後の通知購読は <see cref="_master"/> が
        /// null のため無反応になる（再 Bind までクロストークしない）。
        /// </summary>
        public void Unbind()
        {
            _master = null;
            _anchor = null;
            _viewCamera = null;
            _state = InteractionState.Hidden;

            gameObject.SetActive(false);
        }

        public void SetTargetInfo(InteractionTargetInfo info)
        {
            _targetInfo = info;

            bool active = info.ObjectCategory > 0 && info.Id > 0;
            _targetInfoRoot.SetActive(active);
            if (!active) return;

            var sprite = _iconService.GetSprite(info.IconAssetName);
            _icon.gameObject.SetActive(sprite != null);
            _icon.sprite = sprite;

            SetTargetNameText();
            _countText.gameObject.SetActive(info.Count > 1);
            _countText.text = "(" + info.Count + ")";
        }

        private void SetTargetNameText()
        {
            if (!_targetInfoRoot.activeSelf) return;

            string localizeKey = _targetInfo.Name;
            _nameText.text = _localizationService.GetStringByPropTexts(localizeKey);
        }

        private void SetInteractionText()
        {
            if (_master == null) return;

            _interactionText.text = !_interactionToggle
                ? _localizationService.GetStringByContextActions(_master.InteractionVerb)
                : _localizationService.GetStringByContextActions(_master.ReinteractionVerb);
        }

        private void SetInputBinding()
        {
            if (_master == null) return;

            // 入力キーアイコンを優先して表示し、なければ入力バインドをテキストで表示
            var info = _inputService.GetBindingInfo(_inputService.Player.Interact);
            var sprite = _inputActionIconService.GetSprite(info);
            bool existsIcon = sprite != null;

            _inputBindingText.gameObject.SetActive(!existsIcon);
            _inputBindingText.text = info.DisplayName;

            _inputActionIcon.gameObject.SetActive(existsIcon);
            _inputActionIcon.sprite = sprite;
        }

        /// <summary>
        /// 再インタラクト表示（動詞切替）の状態を反映する。トグル型対象（扉の開閉等）の見た目更新に使う。
        /// </summary>
        public void SetInteractionToggle(bool isOn)
        {
            _interactionToggle = isOn;
            SetInteractionText();
        }

        /// <summary>
        /// 提示状態を反映する。Discoverable/Actionable で対応する見た目だけを出し、視点カメラを更新して位置投影に用いる。
        /// </summary>
        public void SetState(InteractionState state, Camera viewCamera)
        {
            _state = state;
            _viewCamera = viewCamera;

            bool discoverable = state == InteractionState.Discoverable;
            bool actionable = state == InteractionState.Actionable;

            if (_discoverableView != null) _discoverableView.SetActive(discoverable);
            if (_actionableView != null) _actionableView.SetActive(actionable);

            // 呼び出しフェーズ（現状は検出器の Update）に依存せず、このフレームから正しい位置で
            // 表示を開始するための即時反映。以降の追従は LateUpdate が担う。
            UpdatePosition();
        }

        /// <summary>
        /// Hold 長押しの進捗（0→1）を円形ゲージへ反映する。0 超で表示・充填、0 以下で非表示。
        /// 押下中に毎フレーム呼ばれ、中断・完了時は 0 を受けて即座に消える。
        /// </summary>
        public void SetHoldProgress(float progress01)
        {
            if (_holdGauge == null) return;

            bool active = progress01 > 0f;
            if (_holdGauge.gameObject.activeSelf != active)
                _holdGauge.gameObject.SetActive(active);

            _holdGauge.fillAmount = Mathf.Clamp01(progress01);
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        // アンカーのワールド座標をスクリーン座標へ投影し、CanvasGroup の表示可否と RectTransform 位置へ反映する。
        // Actionable は視界外・カメラ背後でも取得可能なため、画面端クランプ+方向矢印で常時可視化する。
        // Discoverable は従来通り画面内のみ表示（カメラ背後は alpha 0、位置は前回値を据え置く）。
        // カメラ・アンカーが未設定（未 Bind）の間は何もしない。
        private void UpdatePosition()
        {
            if (_viewCamera == null || _anchor == null) return;

            var screenPoint = _viewCamera.WorldToScreenPoint(_anchor.position);

            if (_state == InteractionState.Actionable)
            {
                // スクリーンサイズは投影に使ったカメラの pixel サイズと厳密に一致させる
                var screenSize = new Vector2(_viewCamera.pixelWidth, _viewCamera.pixelHeight);
                var margin = _clampMargin * CanvasScaleFactor();
                var position = CalculateClampedPosition(screenPoint, screenSize, margin, out var arrow);

                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
                _rectTransform.position = position;
                ApplyArrow(arrow);
                return;
            }

            bool inFront = IsInFrontOfCamera(screenPoint);

            if (_canvasGroup != null) _canvasGroup.alpha = inFront ? 1f : 0f;

            if (inFront) _rectTransform.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
            ApplyArrow(InteractionPromptArrow.None);
        }

        // CanvasScaler による拡縮率。デザイン px 指定の余白を実ピクセルへ換算する
        private float CanvasScaleFactor() => _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;

        // 矢印の表示・配置を反映する。未配線（テスト雛形・矢印なしプレハブ）では何もしない
        private void ApplyArrow(InteractionPromptArrow arrow)
        {
            if (_clampArrow == null) return;

            bool active = arrow != InteractionPromptArrow.None;
            if (_clampArrow.gameObject.activeSelf != active)
                _clampArrow.gameObject.SetActive(active);
            if (!active) return;

            GetArrowPlacement(arrow, _arrowOffset, out var anchoredPosition, out var zRotation);
            _clampArrow.anchoredPosition = anchoredPosition;
            _clampArrow.localEulerAngles = new Vector3(0f, 0f, zRotation);
        }

        /// <summary>
        /// スクリーン座標変換結果がカメラ前方（表示可能）かを判定する純関数。<see cref="Camera.WorldToScreenPoint(Vector3)"/> の
        /// z 成分はカメラ前方への射影深度で、0 以下はカメラ背後（背面に回り込んだ）ことを意味する。
        /// </summary>
        internal static bool IsInFrontOfCamera(Vector3 screenPoint) => screenPoint.z > 0f;

        /// <summary>
        /// <see cref="Camera.WorldToScreenPoint(Vector3)"/> の結果から表示位置（z=0）と矢印方向を算出する純関数。
        /// クランプ矩形は [margin, screenSize - margin]。矩形内（境界含む）ならクランプせず実位置を返す。
        /// 矩形外は軸別クランプで、はみ出していない軸の座標は保持される（例: 足元対象は下辺上を左右追従でスライド）。
        /// カメラ背後（z&lt;=0）では射影が画面中心の点対称に反転しているため反転補正し、
        /// 中心から対象方向のレイと矩形境界の交点へ配置する（常にクランプ扱い）。
        /// </summary>
        /// <param name="screenPoint">WorldToScreenPoint の結果（スクリーンピクセル座標）</param>
        /// <param name="screenSize">スクリーンサイズ（ピクセル）。投影に使ったカメラの pixelWidth/pixelHeight と一致させる</param>
        /// <param name="margin">画面端からの余白（ピクセル。CanvasScaler 使用時は scaleFactor 乗算済みの値を渡す）</param>
        /// <param name="arrow">クランプされた辺に対応する矢印方向。非クランプ時は <see cref="InteractionPromptArrow.None"/></param>
        internal static Vector3 CalculateClampedPosition(Vector3 screenPoint, Vector2 screenSize, Vector2 margin, out InteractionPromptArrow arrow)
        {
            var center = screenSize * 0.5f;
            var point = new Vector2(screenPoint.x, screenPoint.y);
            var min = margin;
            var max = screenSize - margin;

            if (IsInFrontOfCamera(screenPoint))
            {
                float clampedX = Mathf.Clamp(point.x, min.x, max.x);
                float clampedY = Mathf.Clamp(point.y, min.y, max.y);
                float overshootX = Mathf.Abs(point.x - clampedX);
                float overshootY = Mathf.Abs(point.y - clampedY);

                if (overshootX <= 0f && overshootY <= 0f)
                {
                    arrow = InteractionPromptArrow.None;
                    return new Vector3(point.x, point.y, 0f);
                }

                // はみ出しの大きい軸の辺を矢印に採用する。同値は主用途（足元=下方向）が勝つよう垂直を優先
                arrow = overshootY >= overshootX
                    ? (point.y < min.y ? InteractionPromptArrow.Down : InteractionPromptArrow.Up)
                    : (point.x < min.x ? InteractionPromptArrow.Left : InteractionPromptArrow.Right);
                return new Vector3(clampedX, clampedY, 0f);
            }

            // カメラ背後では clip 空間 w<0 の除算により x/y が画面中心の点対称へ反転するため、両軸同時に戻す
            //（軸単独の反転は対角ケースで破綻する）
            point = 2f * center - point;

            var direction = point - center;
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.down; // 真後ろの既定は下辺（足元・背後が主用途）

            // 反転後の点が矩形内に入りうる（ほぼ真後ろ）ため軸別クランプでは辺に届かない。
            // 中心から対象方向へ伸ばしたレイと矩形境界の交点に置くことで、方向比を保ったまま必ず辺上に載せる
            var halfExtent = center - margin;
            float travelX = direction.x != 0f ? halfExtent.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float travelY = direction.y != 0f ? halfExtent.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
            float travel = Mathf.Min(travelX, travelY);
            var edgePoint = center + direction * travel;

            // 先に境界へ達した軸がクランプ辺（同値は垂直優先）
            arrow = travelY <= travelX
                ? (direction.y < 0f ? InteractionPromptArrow.Down : InteractionPromptArrow.Up)
                : (direction.x < 0f ? InteractionPromptArrow.Left : InteractionPromptArrow.Right);
            return new Vector3(edgePoint.x, edgePoint.y, 0f);
        }

        /// <summary>
        /// 矢印方向からプロンプト中心基準の配置と z 回転（度）を返す純関数。スプライトは上向きが基準。
        /// <see cref="InteractionPromptArrow.None"/> は原点・無回転を返す（呼び出し側で非表示にする想定）。
        /// </summary>
        internal static void GetArrowPlacement(InteractionPromptArrow arrow, float offset, out Vector2 anchoredPosition, out float zRotation)
        {
            switch (arrow)
            {
                case InteractionPromptArrow.Up:
                    anchoredPosition = new Vector2(0f, offset);
                    zRotation = 0f;
                    break;
                case InteractionPromptArrow.Down:
                    anchoredPosition = new Vector2(0f, -offset);
                    zRotation = 180f;
                    break;
                case InteractionPromptArrow.Left:
                    anchoredPosition = new Vector2(-offset, 0f);
                    zRotation = 90f;
                    break;
                case InteractionPromptArrow.Right:
                    anchoredPosition = new Vector2(offset, 0f);
                    zRotation = 270f;
                    break;
                default:
                    anchoredPosition = Vector2.zero;
                    zRotation = 0f;
                    break;
            }
        }
    }
}
