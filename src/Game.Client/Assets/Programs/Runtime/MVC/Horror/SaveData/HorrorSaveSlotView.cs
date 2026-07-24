using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.SaveData
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
        public Observable<BaseEventData> OnSelect => _button.OnSelectAsObservable();

        public void SetInfo(int slotNo, string savepointNameText, string dateTimeText)
        {
            _slotNoText.text = slotNo.ToString();
            _savepointNameText.text = savepointNameText;
            _dateTimeText.text = dateTimeText;
        }
    }
}
