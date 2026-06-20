using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Scriptable.Database.Tables;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// グリッド内の1スロット。アイコンと個数を表示し、選択時に枠を点灯して詳細表示へ通知する。
    /// 選択されるのは Selectable（子 Button）自身なので、このコンポーネントは Button と同じ GameObject に付与する。
    /// マウスホバー（PointerEventReceiver）・パッド/キー操作のどちらも EventSystem の選択に集約され OnSelect に届く。
    /// </summary>
    public class HorrorItemSlotView : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image _iconImage;            // アイコン表示（自身の Image を流用）
        [SerializeField] private TextMeshProUGUI _countText;  // 個数（スタック > 1 のときのみ表示）

        private readonly Subject<HorrorItemSlotView> _onSelected = new();

        /// <summary>このスロットが選択された通知。Component が購読して詳細表示を更新する。</summary>
        public Observable<HorrorItemSlotView> OnSelected => _onSelected;

        /// <summary>保持アイテム。空スロットは null。</summary>
        public HorrorItemMaster Item { get; private set; }

        public void SetItem(HorrorItemMaster item, int count)
        {
            Item = item;
            ApplyIcon(item);
            ApplyCount(item, count);
        }

        public void SetEmpty()
        {
            Item = null;
            ApplyIcon(null);
            if (_countText != null) _countText.gameObject.SetActive(false);
        }

        private void ApplyIcon(HorrorItemMaster item)
        {
            LoadIconAsync(item).Forget();
        }

        private async UniTask LoadIconAsync(HorrorItemMaster item)
        {
            if (item == null || string.IsNullOrEmpty(item.IconAssetName))
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = null;
                    _iconImage.enabled = false;
                }
                return;
            }

            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var icon = await assetService.LoadAssetAsync<Sprite>(item.IconAssetName);

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
        }

        private void ApplyCount(HorrorItemMaster item, int count)
        {
            if (_countText == null) return;
            var show = item != null && count > 1;
            _countText.gameObject.SetActive(show);
            if (show) _countText.text = count.ToString();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _onSelected.OnNext(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
        }

        private void OnDestroy()
        {
            _onSelected.Dispose();
        }
    }
}
