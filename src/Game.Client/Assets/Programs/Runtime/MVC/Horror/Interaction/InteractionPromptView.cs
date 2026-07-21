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
    /// カメラ背後（投影 z&lt;=0）では CanvasGroup の alpha を 0 にして非表示にする。
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

        private RectTransform _rectTransform;

        private HorrorInteractionMaster _master;
        private IInputSystemService _inputService;
        private IInputActionIconService _inputActionIconService;
        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;

        private Transform _anchor;
        private Camera _viewCamera;
        private bool _interactionToggle;
        private InteractionTargetInfo _targetInfo;

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

            gameObject.SetActive(false);
        }

        public void SetTargetInfo(InteractionTargetInfo info)
        {
            _targetInfo = info;

            bool active = !string.IsNullOrEmpty(info.Type) && info.Id > 0;
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
                ? _localizationService.GetStringByContextActions(_master.InteractionVerbLocalizeKey)
                : _localizationService.GetStringByContextActions(_master.ReinteractionVerbLocalizeKey);
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
        // カメラ・アンカーが未設定（未 Bind）の間は何もしない。
        private void UpdatePosition()
        {
            if (_viewCamera == null || _anchor == null) return;

            var screenPoint = _viewCamera.WorldToScreenPoint(_anchor.position);
            bool inFront = IsInFrontOfCamera(screenPoint);

            if (_canvasGroup != null) _canvasGroup.alpha = inFront ? 1f : 0f;

            if (inFront) _rectTransform.position = screenPoint;
        }

        /// <summary>
        /// スクリーン座標変換結果がカメラ前方（表示可能）かを判定する純関数。<see cref="Camera.WorldToScreenPoint(Vector3)"/> の
        /// z 成分はカメラ前方への射影深度で、0 以下はカメラ背後（背面に回り込んだ）ことを意味する。
        /// </summary>
        internal static bool IsInFrontOfCamera(Vector3 screenPoint) => screenPoint.z > 0f;
    }
}
