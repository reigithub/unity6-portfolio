using System;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// 対象 Renderer の materials にアウトライン用 Material を一時的に追加してハイライトする。
    /// <see cref="Renderer.sharedMaterials"/> の割り当てのみを差し替えることで、
    /// マテリアルのインスタンス化（リーク）を避けつつ追加パス描画を実現する。
    /// </summary>
    public class InteractionOutlineHighlighter : MonoBehaviour
    {
        [Tooltip("背面押し出しアウトライン用 Material（Game/InteractionOutline シェーダー）")]
        [SerializeField] private Material _outlineMaterial;

        [Tooltip("対象 Renderer。未指定なら自身と子から自動取得する")]
        [SerializeField] private Renderer[] _renderers;

        // 各 Renderer の元の共有マテリアル割り当て（解除時に戻すための参照）
        private Material[][] _originalSharedMaterials;
        private bool _isHighlighted;

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            _originalSharedMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originalSharedMaterials[i] = _renderers[i] != null ? _renderers[i].sharedMaterials : Array.Empty<Material>();
            }
        }

        /// <summary>
        /// ハイライト表示を切り替える。同じ状態への再呼び出しは無視する（二重付与防止）。
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (_isHighlighted == highlighted) return;
            _isHighlighted = highlighted;

            if (_outlineMaterial == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var targetRenderer = _renderers[i];
                if (targetRenderer == null) continue;

                if (highlighted)
                {
                    var original = _originalSharedMaterials[i];
                    var extended = new Material[original.Length + 1];
                    Array.Copy(original, extended, original.Length);
                    extended[original.Length] = _outlineMaterial;
                    targetRenderer.sharedMaterials = extended;
                }
                else
                {
                    targetRenderer.sharedMaterials = _originalSharedMaterials[i];
                }
            }
        }

        // 無効化・破棄時にハイライトが残らないよう確実に元へ戻す
        private void OnDisable()
        {
            if (_isHighlighted)
            {
                SetHighlighted(false);
            }
        }
    }
}
