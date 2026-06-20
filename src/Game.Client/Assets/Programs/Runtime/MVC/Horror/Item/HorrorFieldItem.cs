using Game.Core.Services;
using Game.Horror.Inventory;
using Game.Shared.Interaction;
using UnityEngine;

namespace Game.Horror.Item
{
    /// <summary>
    /// フィールドに配置されるアイテムオブジェクト。
    /// <see cref="IInteractable"/> を実装し、プレイヤーがインタラクトすると
    /// <see cref="HorrorInventoryService"/> に追加して自身を非表示にする。
    /// </summary>
    public class HorrorFieldItem : MonoBehaviour, IInteractable
    {
        [Header("アイテム設定")]
        [Tooltip("拾得する HorrorItemMaster の Id")]
        [SerializeField] private int _itemId;

        [Tooltip("拾得数量")]
        [SerializeField] private int _quantity = 1;

        [Header("インタラクション演出")]
        [Tooltip("中心位置の上書き。未指定なら自身の transform.position を使う")]
        [SerializeField] private Transform _centerOverride;

        [Tooltip("アウトライン表現を担うコンポーネント")]
        [SerializeField] private InteractionOutlineHighlighter _highlighter;

        [Tooltip("対象位置に出すプロンプト表示")]
        [SerializeField] private InteractionPromptView _promptView;

        /// <summary>インタラクション判定の中心座標。</summary>
        public Vector3 CenterPosition =>
            _centerOverride != null ? _centerOverride.position : transform.position;

        /// <summary>
        /// アイテムを拾得する。マスターデータが見つかればインベントリに追加し自身を非表示にする。
        /// マスターデータが見つからない場合もオブジェクトを非表示にして消費扱いにする。
        /// </summary>
        public void Interact()
        {
            var database = GameServiceManager.Get<ScriptableDatabaseService>().Database;
            if (database.HorrorItemMasterTable.TryFindById(_itemId, out var master))
            {
                GameServiceManager.Get<HorrorInventoryService>().Add(master, _quantity);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// インタラクション状態が変化したときに呼ばれる。
        /// アウトラインは Actionable 時のみ点灯し、プロンプトは状態をそのまま委譲する。
        /// </summary>
        /// <param name="state">新しいインタラクション状態。</param>
        /// <param name="viewCamera">UI プロジェクション基準カメラ。</param>
        public void SetInteractionState(InteractionState state, Camera viewCamera)
        {
            // アウトラインは実行可能時のみ点灯（「可能」を強調。発見可能はプロンプトのみで差別化する）
            if (_highlighter != null)
                _highlighter.SetHighlighted(state == InteractionState.Actionable);

            if (_promptView != null)
                _promptView.SetState(state, viewCamera);
        }

        // 無効化・破棄時に提示を確実に消す（検出器の Hidden 通知が届かないケースの保険）
        private void OnDisable()
        {
            if (_promptView != null)
                _promptView.SetState(InteractionState.Hidden, null);
        }
    }
}
