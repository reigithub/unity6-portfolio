using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// セーブスロット一覧のスロット行 View。表示テキストの反映のみを担い、
    /// マスター参照・ローカライズ解決は呼び出し側（Dialog）が行う。
    /// </summary>
    public class HorrorSaveSlotView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _slotNoText;

        [SerializeField]
        private TMP_Text _savepointNameText;

        [SerializeField]
        private TMP_Text _dateTimeText;

        [SerializeField]
        private Button _button;

        public Observable<Unit> OnClick => _button.OnClickAsObservable();

        /// <summary>スロット行の表示テキストを反映する。</summary>
        /// <param name="slotNoText">スロット番号の表示テキスト。</param>
        /// <param name="savepointNameText">セーブポイント名称の表示テキスト（空きスロットは空文字）。</param>
        /// <param name="dateTimeText">保存日時の表示テキスト（空きスロットは空文字）。</param>
        public void SetInfo(string slotNoText, string savepointNameText, string dateTimeText)
        {
            _slotNoText.text = slotNoText;
            _savepointNameText.text = savepointNameText;
            _dateTimeText.text = dateTimeText;
        }
    }
}
