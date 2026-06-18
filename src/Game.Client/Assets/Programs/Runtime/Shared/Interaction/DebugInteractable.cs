using UnityEngine;

namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト時に Debug.Log を出すだけの最小 <see cref="IInteractable"/> 実装（検証・サンプル用）。
    /// <see cref="InteractionDetector"/> の OverlapSphere に検出されるため Collider が必要。
    /// 視覚表現は <see cref="InteractionOutlineHighlighter"/> へ委譲する。
    /// </summary>
    public class DebugInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("ログに出す識別ラベル")]
        [SerializeField] private string _label = "Object";

        [Tooltip("中心位置の上書き。未指定なら自身の transform.position を使う")]
        [SerializeField] private Transform _centerOverride;

        [Tooltip("ハイライト表現を担うコンポーネント")]
        [SerializeField] private InteractionOutlineHighlighter _highlighter;

        public Vector3 CenterPosition =>
            _centerOverride != null ? _centerOverride.position : transform.position;

        public void Interact()
        {
            Debug.Log($"[Interact] {_label}");
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlighter != null)
            {
                _highlighter.SetHighlighted(highlighted);
            }
        }
    }
}
