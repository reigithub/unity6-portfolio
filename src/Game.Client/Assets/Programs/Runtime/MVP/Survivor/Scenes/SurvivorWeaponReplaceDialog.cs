using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// 武器入れ替えダイアログ引数
    /// </summary>
    public record SurvivorWeaponReplaceDialogArg(
        SurvivorWeaponUpgradeOption NewWeapon,
        IReadOnlyList<SurvivorWeaponBase> CurrentWeapons
    );

    /// <summary>
    /// 武器入れ替えダイアログ（Presenter）
    /// 満杯状態で新規武器を選択した際に表示
    /// 戻り値: 削除する武器ID（キャンセル時はnull）
    /// </summary>
    public class SurvivorWeaponReplaceDialog :
        GameDialogScene<SurvivorWeaponReplaceDialog, SurvivorWeaponReplaceDialogComponent, int?>,
        IGameSceneArg<SurvivorWeaponReplaceDialogArg>
    {
        protected override string AssetPathOrAddress => "SurvivorWeaponReplaceDialog";

        [Inject] private readonly IInputService _inputService;

        private SurvivorWeaponReplaceDialogArg _arg;

        /// <summary>
        /// ダイアログを表示して結果を取得
        /// </summary>
        public static UniTask<int?> RunAsync(IGameSceneService sceneService, SurvivorWeaponReplaceDialogArg arg)
        {
            return sceneService.TransitionDialogAsync<
                SurvivorWeaponReplaceDialog,
                SurvivorWeaponReplaceDialogComponent,
                SurvivorWeaponReplaceDialogArg,
                int?>(arg);
        }

        public UniTask ArgHandle(SurvivorWeaponReplaceDialogArg arg)
        {
            _arg = arg;
            return UniTask.CompletedTask;
        }

        public override UniTask Startup()
        {
            // Viewを初期化
            SceneComponent.Initialize(_arg.NewWeapon, _arg.CurrentWeapons);

            // Viewのイベントを購読
            SceneComponent.OnWeaponSelected
                .Subscribe(weaponId => OnWeaponSelected(weaponId))
                .AddTo(Disposables);

            SceneComponent.OnCancelClicked
                .Subscribe(_ => OnCancel())
                .AddTo(Disposables);

            return base.Startup();
        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            // Escapeキーでキャンセル
            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnCancel();
                })
                .AddTo(Disposables);
        }

        private void OnWeaponSelected(int weaponId)
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(weaponId);
        }

        private void OnCancel()
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(null);
        }
    }
}
