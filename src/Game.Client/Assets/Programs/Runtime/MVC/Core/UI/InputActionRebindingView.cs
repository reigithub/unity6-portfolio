using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>
    /// 1アクション×1スキームのキーリバインド行（純粋View）。
    /// 現在のバインド表示と「変更」ボタンを持ち、押下を Observable で通知する。
    /// リバインド実行・重複判定などの入力ロジックは持たず、表示更新のみを担う。
    /// </summary>
    public class InputActionRebindingView : MonoBehaviour
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
        [SerializeField] private bool _rebindable = true;

        [Header("Waiting")]
        [SerializeField] private GameObject _waitingOverlay;   // 任意: リバインド待機中の表示
        [SerializeField] private Image _timeoutFill;           // 任意: 自動キャンセルまでの残り時間バー/リング（fillAmount 1→0）

        /// <summary>コントロールスキーム（Keyboard＆Mouse / Gamepad）。</summary>
        public string Scheme => _scheme;

        /// <summary>Player マップのアクション名。</summary>
        public string ActionName => _actionName;

        /// <summary>コンポジットのパート名（up/down/left/right）。空＝非コンポジット（単体 binding）。</summary>
        public string CompositePartName => _compositePartName;

        /// <summary>リバインド操作終了後に再フォーカスするオブジェクト</summary>
        public Selectable Selectable => _rebindButton;

        /// <summary>「変更」ボタン押下。</summary>
        public Observable<Unit> OnRebindRequested
            => _rebindable ? _rebindButton.OnClickAsObservable() : Observable.Empty<Unit>();

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

        /// <summary>タイムアウトの残り時間表示を更新する（1=満タン, 0=タイムアウト）。</summary>
        public void SetTimeoutProgress(float remaining01)
        {
            if (_timeoutFill != null)
                _timeoutFill.fillAmount = Mathf.Clamp01(remaining01);
        }

        /// <summary>リバインド待機状態の表示を切り替える（待機中はボタンを無効化）。</summary>
        public void SetWaiting(bool waiting)
        {
            if (_waitingOverlay != null)
                _waitingOverlay.SetActive(waiting);
            if (_rebindButton != null)
                _rebindButton.interactable = !waiting;
        }
    }
}
