using Game.Core.Services;
using Game.Horror.Database;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// クラフトの詳細ペインに並ぶ素材 1 件。アイコンと「必要数 / 所持数」を表示する。
    /// </summary>
    public class HorrorCraftMaterialView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _countText;

        private IHorrorIconService _iconService;

        public void Initialize()
        {
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();
        }

        public void SetMaterial(ObjectCategory category, int objectId, int requiredCount, int possessedCount)
        {
            Sprite sprite = null;
            if (HorrorDatabaseHelper.TryGetInfo(category, objectId, out var info)
                && !string.IsNullOrEmpty(info.IconAssetName))
            {
                sprite = _iconService.GetSprite(info.IconAssetName);
            }

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }

            if (_countText != null)
                _countText.text = $"{requiredCount} / {possessedCount}";
        }
    }
}
