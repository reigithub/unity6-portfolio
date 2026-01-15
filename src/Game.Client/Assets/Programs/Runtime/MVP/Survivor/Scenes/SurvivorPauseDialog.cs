using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using R3;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ポーズダイアログの結果
    /// </summary>
    public enum SurvivorPauseResult
    {
        Resume,
        Retry,
        Quit
    }

    /// <summary>
    /// Survivorポーズダイアログ（Presenter）
    /// MVPパターンでViewとの仲介を行う
    /// </summary>
    public class SurvivorPauseDialog : GameDialogScene<SurvivorPauseDialog, SurvivorPauseDialogComponent, SurvivorPauseResult>
    {
        protected override string AssetPathOrAddress => "SurvivorPauseDialog";

        public override UniTask Startup()
        {
            // Viewのイベントを購読
            SceneComponent.OnResultSelected
                .Subscribe(x => OnResultSelected(x))
                .AddTo(Disposables);

            return base.Startup();
        }

        private void OnResultSelected(SurvivorPauseResult result)
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(result);
        }
    }
}