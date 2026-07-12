using Game.Core.Services;
using Game.Shared.Enums;
using Game.Shared.Localization;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// コンテキストサブメニューの1エントリ（ラベル＋ボタン）。決定でアクション種別を通知する。
    /// </summary>
    public class HorrorInventoryContextActionView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _button;

        private readonly Subject<InventoryContextActionType> _onClicked = new();
        public Observable<InventoryContextActionType> OnClicked => _onClicked;

        /// <summary>フォーカス設定に用いるボタン。</summary>
        public Selectable Selectable => _button;

        public void Initialize(InventoryContextActionType contextAction)
        {
            if (_label != null)
            {
                var localization = GameServiceManager.Resolve<ILocalizationService>();
                _label.text = localization.GetStringByContextActions(contextAction.ToString());
            }

            _button.OnClickAsObservable()
                .Subscribe(_ => _onClicked.OnNext(contextAction))
                .AddTo(this);
        }

        private void OnDestroy()
        {
            _onClicked.Dispose();
        }
    }
}
