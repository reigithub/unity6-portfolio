using R3;

namespace Game.ScoreTimeAttack.Base
{
    /// <summary>
    /// View基底インターフェース
    /// MVPパターンにおいてViewは受動的であり、Presenterからの指示で表示を更新する
    /// </summary>
    public interface IView
    {
        /// <summary>
        /// Viewの表示/非表示
        /// </summary>
        void SetVisible(bool visible);
    }

    /// <summary>
    /// ボタン等のUI要素を持つView
    /// </summary>
    public interface IInteractableView : IView
    {
        /// <summary>
        /// 全ボタンのインタラクティブ状態を設定
        /// </summary>
        void SetInteractable(bool interactable);
    }

    /// <summary>
    /// フェード機能を持つView
    /// </summary>
    public interface IFadeableView : IView
    {
        void FadeIn(float duration = 0.25f);
        void FadeOut(float duration = 0.25f);
    }
}