namespace Game.Shared.Enums
{
    /// <summary>
    /// インタラクトの起動方式。入力ハンドラがこの値を見て「いつ <see cref="IInteractable.Interact"/> を呼ぶか」を決める。
    /// 対象側はこの値を宣言するだけで、入力の検知方法そのものには関与しない。
    /// </summary>
    public enum InteractionInputType
    {
        /// <summary>押下した瞬間に1回実行（拾う・話す等）。</summary>
        Instant,

        /// <summary>一定時間（<see cref="IInteractable.HoldSeconds"/>）押し続けて実行（こじ開ける等）。</summary>
        Hold,

        /// <summary>押下のたびに状態を交互に切り替える（扉の開閉等）。</summary>
        Toggle,
    }

    /// <summary>
    /// インタラクト対象の提示状態。検出器が距離・可視性から判定し、対象側のプロンプト表示へ反映する。
    /// </summary>
    public enum InteractionState
    {
        /// <summary>非提示（検出範囲外・視界外・遮蔽）。</summary>
        Hidden,

        /// <summary>発見可能。対象だと分かるが、まだインタラクトできない（距離が遠い）。複数同時に成立しうる。</summary>
        Discoverable,

        /// <summary>
        /// インタラクト可能。画面中心に最も近い単一対象のみが成立する。
        /// エイムコーン内に候補が無い場合は、視界外かつプレイヤー前方半面（水平 180 度）かつ近接距離内の対象が
        /// フォールバックで成立しうる（このとき対象は Discoverable を経ず、プロンプトは画面端クランプ+方向矢印で表示される）。
        /// </summary>
        Actionable,
    }

    /// <summary>
    /// 画面端クランプ時の方向矢印。<see cref="None"/> はクランプ不要（実位置表示）を意味する。
    /// </summary>
    public enum InteractionPromptArrow
    {
        /// <summary>クランプなし（対象は画面内。矢印非表示）。</summary>
        None,

        /// <summary>上辺にクランプ（対象は画面上方）。</summary>
        Up,

        /// <summary>下辺にクランプ（対象は画面下方・足元）。</summary>
        Down,

        /// <summary>左辺にクランプ（対象は画面左方）。</summary>
        Left,

        /// <summary>右辺にクランプ（対象は画面右方）。</summary>
        Right,
    }
}
