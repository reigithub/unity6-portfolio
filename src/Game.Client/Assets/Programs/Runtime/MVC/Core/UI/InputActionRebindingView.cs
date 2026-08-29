using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>
    /// 1アクション×1スキームのキーリバインド行。
    /// 現在のバインド表示と「変更」ボタンを持ち、押下を Observable で通知する。
    /// リバインド実行・重複判定などの入力ロジックは持たない。
    /// </summary>
    public class InputActionRebindingView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _controlScheme;     // コントロールスキーム（Keyboard&Mouse / Gamepad）
        [SerializeField] private string _actionMapName = InputActionMaps.Player;
        [SerializeField] private string _actionName;        // Player マップのアクション名（例: Jump）
        [SerializeField] private string _compositePartName; // コンポジットのパート名（up/down/left/right）。空＝非コンポジット

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI _actionLabel;
        [SerializeField] private TextMeshProUGUI _bindingLabel;
        [SerializeField] private Image _actionIcon;

        [Header("Buttons")]
        [SerializeField] private Button _rebindButton;
        [SerializeField] private bool _rebindable = true;

        [Header("Waiting")]
        [SerializeField] private GameObject _waitingOverlay;   // リバインド待機中の表示
        [SerializeField] private Image _timeoutFill;           // 自動キャンセルまでの残り時間バー/リング（fillAmount 1→0）

        private IInputSystemService _inputService;
        private IInputActionIconService _iconService;
        private bool _isWaiting;
        private bool _initialized;

        /// <summary>コントロールスキーム（Keyboard＆Mouse / Gamepad）。</summary>
        public string ControlScheme => _controlScheme;

        /// <summary>入力アクションマップ名</summary>
        public string ActionMapName => _actionMapName;

        /// <summary>入力アクション名</summary>
        public string ActionName => _actionName;

        /// <summary>コンポジットのパート名（up/down/left/right）。空＝非コンポジット（単体 binding）。</summary>
        public string CompositePartName => _compositePartName;

        /// <summary>リバインド操作終了後に再フォーカスするオブジェクト</summary>
        public Selectable Selectable => _rebindButton;

        /// <summary>「変更」ボタン押下。</summary>
        public Observable<Unit> OnRebindRequested
            => _rebindable ? _rebindButton.OnClickAsObservable() : Observable.Empty<Unit>();

        public void Initialize()
        {
            if (_initialized) return;
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _iconService = GameServiceManager.Resolve<IInputActionIconService>();
            var localizationService = GameServiceManager.Resolve<ILocalizationService>();

            if (_rebindButton.TryGetComponent<Image>(out var image))
            {
                image.color = _rebindable ? Color.white : Color.gray;
            }

            _inputService.OnBindingChanged.Subscribe(_ => Refresh()).AddTo(this);
            _inputService.OnDeviceChanged.Subscribe(_ => Refresh()).AddTo(this);
            localizationService.OnLocaleChanged.Subscribe(_ => Refresh()).AddTo(this);

            Refresh();
            _initialized = true;
        }

        private void Refresh()
        {
            if (_isWaiting) return; // 待機表示中は上書きしない
            var info = _inputService.GetBindingInfo(_controlScheme, _actionMapName, _actionName, _compositePartName);
            SetDisplay(info.DisplayName);
            SetIcon(_iconService.GetSprite(info));
        }

        /// <summary>アクション名ラベルを設定する。</summary>
        public void SetActionLabel(string text)
        {
            if (_actionLabel != null)
                _actionLabel.text = text;
        }

        /// <summary>現在のバインド表示文字列を設定する。</summary>
        public void SetDisplay(params string[] bindingTexts)
        {
            if (_bindingLabel != null)
                _bindingLabel.text = string.Join("/", bindingTexts);
        }

        public void SetIcon(Sprite icon)
        {
            if (_actionIcon != null)
                _actionIcon.sprite = icon;
        }

        /// <summary>タイムアウトの残り時間表示を更新する（1=満タン, 0=タイムアウト）。</summary>
        public void SetTimeoutProgress(float remaining01)
        {
            if (_timeoutFill != null)
                _timeoutFill.fillAmount = Mathf.Clamp01(remaining01);
        }

        /// <summary>リバインド待機状態の表示を切り替える（待機中はボタンを無効化）。</summary>
        public void SetWaiting(bool waiting)
        {
            _isWaiting = waiting;
            if (_waitingOverlay != null)
                _waitingOverlay.SetActive(waiting);
            if (_rebindButton != null)
                _rebindButton.interactable = !waiting;
            if (!waiting)
                Refresh(); // キャンセル復帰時の表示復元
        }
    }
}
