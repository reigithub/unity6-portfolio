using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>
    /// 1アクション×1スキームのキーリバインド行（純粋View）。
    /// 現在のバインド表示と「変更」「リセット」ボタンを持ち、押下を Observable で通知する。
    /// リバインド実行・重複判定などの入力ロジックは持たず、表示更新のみを担う。
    /// </summary>
    public class InputActionRebindView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _scheme;            // コントロールスキーム（Keyboard&Mouse / Gamepad）
        [SerializeField] private string _actionName;        // Player マップのアクション名（例: Jump）
        [SerializeField] private string _compositePartName; // コンポジットのパート名（up/down/left/right）。空＝非コンポジット

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI _actionLabel;
        [SerializeField] private TextMeshProUGUI _bindingLabel;

        [Header("Buttons")]
        [SerializeField] private Button _rebindButton;
        [SerializeField] private Button _resetButton;

        [Header("Waiting")]
        [SerializeField] private GameObject _waitingOverlay;   // 任意: リバインド待機中の表示
        [SerializeField] private Button _cancelButton;         // 任意: 待機中のキャンセル（InputAction 非依存）

        /// <summary>Player マップのアクション名。</summary>
        public string ActionName => _actionName;

        /// <summary>コントロールスキーム（Keyboard&amp;Mouse / Gamepad）。</summary>
        public string Scheme => _scheme;

        /// <summary>コンポジットのパート名（up/down/left/right）。空＝非コンポジット（単体 binding）。</summary>
        public string CompositePartName => _compositePartName;

        /// <summary>「変更」ボタン押下。</summary>
        public Observable<Unit> OnRebindRequested => _rebindButton.OnClickAsObservable();

        /// <summary>「リセット」ボタン押下。</summary>
        public Observable<Unit> OnResetRequested => _resetButton.OnClickAsObservable();

        /// <summary>待機中キャンセルボタン押下（未設定なら何も流さない）。</summary>
        public Observable<Unit> OnCancelRequested =>
            _cancelButton != null ? _cancelButton.OnClickAsObservable() : Observable.Empty<Unit>();

        /// <summary>アクション名ラベルを設定する。</summary>
        public void SetActionLabel(string text)
        {
            if (_actionLabel != null)
                _actionLabel.text = text;
        }

        /// <summary>現在のバインド表示文字列を設定する。</summary>
        public void SetDisplay(string bindingText)
        {
            if (_bindingLabel != null)
                _bindingLabel.text = bindingText;
        }

        /// <summary>リバインド待機状態の表示を切り替える（待機中はボタンを無効化）。</summary>
        public void SetWaiting(bool waiting)
        {
            if (_waitingOverlay != null)
                _waitingOverlay.SetActive(waiting);
            if (_rebindButton != null)
                _rebindButton.interactable = !waiting;
            if (_resetButton != null)
                _resetButton.interactable = !waiting;
        }
    }
}
