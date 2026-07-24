namespace Game.Horror.Interaction
{
    /// <summary>
    /// インタラクトプロンプト View の貸出インターフェース。対象（<see cref="InteractableBase"/>）が
    /// 表示中だけ View を借り受けるための Rent/Return のみを公開し、プールのライフサイクル
    /// （<see cref="InteractionPromptPool.Initialize"/>）は所有者（コンテナ）専有に留める。
    /// </summary>
    public interface IInteractionPromptPool
    {
        /// <summary>
        /// View を1つ貸し出す。空きが無ければ追加生成して動作を継続する。
        /// </summary>
        InteractionPromptView Rent();

        /// <summary>
        /// View を返却する。返却された View は Unbind され再貸出可能になる。
        /// </summary>
        void Return(InteractionPromptView view);
    }
}
