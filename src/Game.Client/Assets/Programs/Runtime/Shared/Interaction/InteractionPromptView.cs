using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト対象の位置に出すワールド空間プロンプト。対象（Interactable）が所有し、対象プレハブの子として配置する。
    /// 検出器から渡される <see cref="InteractionState"/> に応じて発見可能/実行可能の見た目を出し分け、視点カメラへビルボードする。
    /// 見た目の意味（発見/実行可能）は検出器が決め、ここは表示と正対だけを担う。
    /// 既定は非アクティブ運用で、Hidden では自身を無効化し描画もビルボードも止める。
    /// 表示サイズはカメラ距離に依らず一定（<see cref="_screenSizeFactor"/> で全対象を統一）に保つ。
    /// 注意: スケールは自身の localScale に一様適用するため、親に「回転＋非一様スケール」を混在させないこと
    /// （<see cref="Transform.lossyScale"/> が歪み、親スケールの打ち消しが不正確になる）。
    /// </summary>
    public class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("発見可能（対象だと分かる）状態の見た目")]
        [SerializeField] private GameObject _discoverableView;

        [Tooltip("実行可能（インタラクトできる）状態の見た目")]
        [SerializeField] private GameObject _actionableView;

        [Tooltip("画面に対するプロンプトの目標サイズ係数。全対象で同一値にすると画面上のサイズが距離に依らず統一される")]
        [SerializeField] private float _screenSizeFactor = 0.05f;

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
            if (_viewCamera == null) return;

            var camTf = _viewCamera.transform;

            // ビルボード（視点カメラへ正対）
            transform.rotation = camTf.rotation;

            // 平行投影は見かけサイズが距離非依存のためスケール補正不要
            if (_viewCamera.orthographic) return;

            // 距離はカメラ前方への射影深度を使う（直線距離だと画面端で過大評価する）
            float depth = Vector3.Dot(transform.position - camTf.position, camTf.forward);
            if (depth <= 0f) return; // カメラ背後は補正しない

            float parentLossy = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            float scale = CalculateUniformLocalScale(depth, _viewCamera.fieldOfView, _screenSizeFactor, parentLossy);
            transform.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>
        /// 距離に依らず画面上一定サイズになる localScale を算出する。
        /// 透視投影では見かけサイズ ∝ 1/深度 なので、ワールドスケールを深度に比例させて相殺する。
        /// 親スケールは <paramref name="parentLossyScale"/> で打ち消し、最終ワールドスケールを目標値に合わせる。
        /// </summary>
        /// <param name="depth">カメラ前方への射影深度（m, 正の値）</param>
        /// <param name="fovDegrees">カメラの垂直 FOV（度）</param>
        /// <param name="screenSizeFactor">画面に対する目標サイズ係数</param>
        /// <param name="parentLossyScale">親の lossyScale（一様前提の代表軸）。0 以下は下限でガード</param>
        public static float CalculateUniformLocalScale(float depth, float fovDegrees, float screenSizeFactor, float parentLossyScale)
        {
            float worldHeightAtDepth = 2f * depth * Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            float desiredWorldScale = screenSizeFactor * worldHeightAtDepth;
            return desiredWorldScale / Mathf.Max(parentLossyScale, 1e-5f);
        }
    }
}
