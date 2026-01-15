using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using R3;

namespace Game.MVP.Survivor.UI
{
    /// <summary>
    /// レベルアップダイアログ引数
    /// </summary>
    public record SurvivorPlayerLevelUpDialogArg(List<WeaponUpgradeOption> Options, int PlayerLevel);

    /// <summary>
    /// レベルアップダイアログ（Presenter）
    /// 武器の選択・アップグレードを行う
    /// </summary>
    public class SurvivorPlayerLevelUpDialog :
        GameDialogScene<SurvivorPlayerLevelUpDialog, SurvivorPlayerLevelUpDialogComponent, WeaponUpgradeOption>,
        IGameSceneArg<SurvivorPlayerLevelUpDialogArg>
    {
        protected override string AssetPathOrAddress => "SurvivorPlayerLevelUpDialog";

        private SurvivorPlayerLevelUpDialogArg _arg;

        public UniTask ArgHandle(SurvivorPlayerLevelUpDialogArg arg)
        {
            _arg = arg;
            return UniTask.CompletedTask;
        }

        public override UniTask Startup()
        {
            // Viewを初期化
            SceneComponent.Initialize(_arg.Options, _arg.PlayerLevel);

            // Viewのイベントを購読
            SceneComponent.OnOptionSelected
                .Subscribe(x => OnOptionSelected(x))
                .AddTo(Disposables);

            return base.Startup();
        }

        private void OnOptionSelected(WeaponUpgradeOption option)
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(option);
        }
    }
}