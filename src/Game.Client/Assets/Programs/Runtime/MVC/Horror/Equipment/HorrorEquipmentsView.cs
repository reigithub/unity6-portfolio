using DG.Tweening;
using Game.Core.Services;
using Game.Horror.Inventory;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Equipment
{
    /// <summary>
    /// 装備切替ショートカット HUD（D-Pad 4スロット常時表示）。切替成功時に Show を呼ぶと
    /// 現在の登録内容と装備中スロットの枠ハイライトを表示し、一定時間後に自動でフェードアウトする。
    /// スロット並びはショートカット登録ダイアログと一致させる: 1=左 / 2=上 / 3=右 / 4=下（index 0-3）。
    /// </summary>
    public class HorrorEquipmentsView : MonoBehaviour
    {
        [SerializeField] private HorrorEquipmentSlotView[] _slots;
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private float _fadeInSeconds = 0.2f;
        [SerializeField] private float _holdSeconds = 2f;
        [SerializeField] private float _fadeOutSeconds = 0.5f;

        [SerializeField] private Color _equippedFrameColor = Color.darkCyan;
        [SerializeField] private Color _normalFrameColor = Color.white;

        private IScriptableDatabaseService _databaseService;
        private IHorrorEquipmentService _equipmentService;
        private Sequence _sequence;

        private void Awake()
        {
            // Instantiate 時点（フレーム0）から非表示にする。prefab 側の Alpha はレイアウト作業性のため 1 のまま変更しない
            _canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// 依存解決を行う。プレイヤーコントローラーの初期化処理から呼ぶこと。
        /// </summary>
        public void Initialize()
        {
            _databaseService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _equipmentService = GameServiceManager.Resolve<IHorrorEquipmentService>();
        }

        /// <summary>
        /// 装備切替成功時に呼ぶ。4スロットの登録内容を表示し、装備中スロットの枠だけハイライトしてフェード表示する。
        /// フェード中の再呼び出しは現在の透明度から滑らかに再フェードインし、保持タイマーをリセットする。
        /// </summary>
        /// <param name="equippedType">装備中アイテムのスロット種別</param>
        /// <param name="equippedId">装備中アイテムの Id</param>
        public void Show(ObjectCategory equippedType, int equippedId)
        {
            // 1. 登録内容の更新（ダイアログの RefreshSlot と同イディオム）
            for (int i = 0; i < _slots.Length; i++)
                RefreshSlot(i);

            // 2. 枠色の明示適用（装備中スロットのみハイライト、それ以外は毎回通常色に戻す）
            for (int i = 0; i < _slots.Length; i++)
            {
                var isEquipped = _equipmentService.TryGetSlot(i, out var slot) && slot.ObjectCategory == equippedType && slot.Id == equippedId;
                _slots[i].SetFrameColor(isEquipped ? _equippedFrameColor : _normalFrameColor);
            }

            // 3. フェード演出：再 Show なら Kill して作り直す（Kill(complete:false) は現在 alpha を保持するため滑らかに繋がる）
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();

            _sequence = DOTween.Sequence()
                .Append(_canvasGroup.DOFade(1f, _fadeInSeconds))
                .AppendInterval(_holdSeconds)
                .Append(_canvasGroup.DOFade(0f, _fadeOutSeconds))
                .SetUpdate(true); // ポーズ中（timeScale=0）でもフェードを止めない
        }

        // 保存済み binding を master 解決してスロット表示を更新する（空なら空表示）。
        private void RefreshSlot(int index)
        {
            if (_equipmentService.TryGetSlot(index, out var slot) && HorrorInventoryHelper.TryGetSlotInfo(_databaseService.Database, slot.ObjectCategory, slot.Id, out var info))
                _slots[index].SetSlot(info);
            else
                _slots[index].SetEmpty();
        }

        private void OnDestroy()
        {
            // フェード中のステージ遷移（プレイヤー破棄）で死んだ CanvasGroup へ tween が走るのを防ぐ
            if (_sequence != null && _sequence.IsActive())
                _sequence.Kill();
        }
    }
}
