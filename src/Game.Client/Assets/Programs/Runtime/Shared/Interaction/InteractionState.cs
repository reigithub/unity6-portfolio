namespace Game.Shared.Interaction
{
    /// <summary>
    /// インタラクト対象の提示状態。検出器が距離・可視性から判定し、対象側のプロンプト表示へ反映する。
    /// </summary>
    public enum InteractionState
    {
        /// <summary>非提示（検出範囲外・視界外・遮蔽）。</summary>
        Hidden,

        /// <summary>発見可能。対象だと分かるが、まだインタラクトできない（距離が遠い）。複数同時に成立しうる。</summary>
        Discoverable,

        /// <summary>インタラクト可能。画面中心に最も近い単一対象のみが成立する。</summary>
        Actionable,
    }
}
