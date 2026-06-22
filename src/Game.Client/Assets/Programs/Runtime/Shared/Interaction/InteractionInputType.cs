namespace Game.Shared.Interaction
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
}
