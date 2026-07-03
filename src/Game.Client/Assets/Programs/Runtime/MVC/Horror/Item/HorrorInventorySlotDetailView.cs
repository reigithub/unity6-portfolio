using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Item
{
    /// <summary>
    /// 選択中アイテムの詳細（拡大アイコン・名前・説明）を表示するパネル。
    /// 空スロット選択時は Clear で内容を消す。
    /// </summary>
    public class HorrorInventorySlotDetailView : MonoBehaviour
    {
        [SerializeField] private Image _largeIcon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        public void SetDetail(IHorrorInventorySlotInfo info)
        {
            LoadIconAsync(info).Forget();

            if (_nameText != null) _nameText.text = info?.Name;
            if (_descriptionText != null) _descriptionText.text = info?.Description;
        }

        private async UniTask LoadIconAsync(IHorrorInventorySlotInfo item)
        {
            if (item == null || string.IsNullOrEmpty(item.IconAssetName))
            {
                if (_largeIcon != null)
                {
                    _largeIcon.sprite = null;
                    _largeIcon.enabled = false;
                }
                return;
            }

            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var icon = await assetService.LoadAssetAsync<Sprite>(item.IconAssetName);

            if (_largeIcon != null)
            {
                _largeIcon.sprite = icon;
                _largeIcon.enabled = icon != null;
            }
        }

        public void Clear() => SetDetail(null);
    }
}
