using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト対象の位置に出すワールド空間プロンプト。対象（Interactable）が所有し、対象プレハブの子として配置する。
    /// 検出器から渡される <see cref="InteractionState"/> に応じて発見可能/実行可能の見た目を出し分け、視点カメラへビルボードする。
    /// 見た目の意味（発見/実行可能）は検出器が決め、ここは表示と正対だけを担う。
    /// 既定は非アクティブ運用で、Hidden では自身を無効化し描画もビルボードも止める。
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("発見可能（対象だと分かる）状態の見た目")]
        [SerializeField] private GameObject _discoverableView;

        [Tooltip("実行可能（インタラクトできる）状態の見た目")]
        [SerializeField] private GameObject _actionableView;

        private Camera _viewCamera;

        /// <summary>
        /// 提示状態を反映する。Hidden で自身を無効化し、Discoverable/Actionable で対応する見た目だけを出す。
        /// <paramref name="viewCamera"/> はビルボードの正対先（Hidden 時は未使用）。
        /// </summary>
        public void SetState(InteractionState state, Camera viewCamera)
        {
            _viewCamera = viewCamera;

            bool discoverable = state == InteractionState.Discoverable;
            bool actionable = state == InteractionState.Actionable;

            if (_discoverableView != null) _discoverableView.SetActive(discoverable);
            if (_actionableView != null) _actionableView.SetActive(actionable);

            gameObject.SetActive(discoverable || actionable);
        }

        private void LateUpdate()
        {
            if (_viewCamera != null)
            {
                transform.rotation = _viewCamera.transform.rotation;
            }
        }
    }
}
