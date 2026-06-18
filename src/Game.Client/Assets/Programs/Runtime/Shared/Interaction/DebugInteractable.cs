using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト時に Debug.Log を出すだけの最小 <see cref="IInteractable"/> 実装（検証・サンプル用）。
    /// <see cref="InteractionDetector"/> の OverlapSphere に検出されるため Collider が必要。
    /// 視覚表現はアウトライン（<see cref="InteractionOutlineHighlighter"/>）と
    /// 対象側プロンプト（<see cref="InteractionPromptView"/>）へ委譲する。
    /// </summary>
    public class DebugInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("ログに出す識別ラベル")]
        [SerializeField] private string _label = "Object";

        [Tooltip("中心位置の上書き。未指定なら自身の transform.position を使う")]
        [SerializeField] private Transform _centerOverride;

        [Tooltip("アウトライン表現を担うコンポーネント")]
        [SerializeField] private InteractionOutlineHighlighter _highlighter;

        [Tooltip("対象位置に出すプロンプト表示")]
        [SerializeField] private InteractionPromptView _promptView;

        public Vector3 CenterPosition =>
            _centerOverride != null ? _centerOverride.position : transform.position;

        public void Interact()
        {
            Debug.Log($"[Interact] {_label}");
        }

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
