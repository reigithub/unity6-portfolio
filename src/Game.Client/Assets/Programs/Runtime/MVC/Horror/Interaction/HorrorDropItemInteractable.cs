using System;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// エネミードロップ品の拾得インタラクト。アイテム・数量は <see cref="Setup"/> で実行時注入され、
    /// マスター共有行（_interactionId）はプロンプト表示・入力方式の供給のみに使う。
    /// シーン静的配置の <see cref="HorrorItemInteractable"/> と異なりランタイム生成されるため、
    /// インタラクション永続化（IHorrorInteractionService）には一切書き込まない
    /// （共有 Id を記録すると同 Id の全ドロップ品が取得済み扱いになる）。
    /// アイテム種を問わない共通構造で、見た目と当たり判定は <see cref="AttachModel"/> で
    /// ModelHolder 配下へ装着するモデルアセットが供給する。
    /// </summary>
    public class HorrorDropItemInteractable : InteractableBase
    {
        [Tooltip("具体モデルの装着先")]
        [SerializeField] private Transform _modelHolder;

        private HorrorItemMaster _itemMaster;
        private int _count;
        private Action<HorrorDropItemInteractable> _onCollected;

        /// <summary>
        /// 具体モデルを ModelHolder 配下へ装着する。スポナーがプール個体の生成時に一度だけ呼ぶ。
        /// 呼び出しは個体が非アクティブな間に行われるため、初回貸出時の Awake での収集
        /// （<see cref="InteractableBase"/> のコライダー、<see cref="InteractionOutlineHighlighter"/> の Renderer）は
        /// このモデルを含んだ状態で走る。
        /// </summary>
        public void AttachModel(GameObject modelPrefab)
        {
            if (_modelHolder == null)
            {
                // プレハブ側の結線漏れ。無音だと「モデルの無いドロップ品が拾えない」としか観測できない
                Debug.LogError($"[{nameof(HorrorDropItemInteractable)}] {nameof(_modelHolder)} が未設定です", this);
                return;
            }

            var model = Instantiate(modelPrefab, _modelHolder);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.SetLayerRecursively(gameObject.layer);

            // モデル同梱のコライダーは非トリガーでプレイヤーを押し返し、他のドロップ品の遮蔽物にもなる。
            // 形状はモデル側のものをそのまま拾得判定に使い、トリガー化だけ行う
            foreach (var col in model.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                col.isTrigger = true;
            }
        }

        /// <summary>スポーナーがプール貸出時に呼ぶ。プール再利用個体の状態上書きを兼ねる。</summary>
        public void Setup(HorrorItemMaster itemMaster, int count, Action<HorrorDropItemInteractable> onCollected)
        {
            _itemMaster = itemMaster;
            _count = count;
            _onCollected = onCollected;
        }

        // 拾得系のため、視界外でも画面端クランプのプロンプトで拾えるようにする
        public override bool AllowOutOfView => true;

        // ドロップ品に再拾得の概念はなく、取得状態を永続化もしない
        public override bool WasInteracted() => false;

        public override bool CanInteract() => _itemMaster != null;

        public override void Interact()
        {
            // キーアイテムはマスターデータ検証（HorrorEnemyDropItemValidator）で配布禁止のため通常アイテム経路のみ。
            // インベントリ満杯（TryAdd 失敗）時はその場に残置する
            var inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();
            if (!inventoryService.TryAdd(_itemMaster.ObjectCategory, _itemMaster.Id, _count, _itemMaster.MaxCount))
                return;

            // base.Interact()（= 共有 _interactionId の永続化）は呼ばず、スポーナーへプール返却を通知する
            _onCollected?.Invoke(this);
        }

        public override InteractionTargetInfo GetTargetInfo()
        {
            if (_itemMaster == null)
                return base.GetTargetInfo();

            return new InteractionTargetInfo
            {
                ObjectCategory = _itemMaster.ObjectCategory,
                Id = _itemMaster.Id,
                Name = _itemMaster.Name,
                Description = _itemMaster.Description,
                Count = _count,
                IconAssetName = _itemMaster.IconAssetName,
            };
        }
    }
}
