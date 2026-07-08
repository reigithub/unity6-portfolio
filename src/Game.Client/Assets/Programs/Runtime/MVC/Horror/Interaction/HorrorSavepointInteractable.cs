using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// セーブポイントのインタラクト。進行データの Dirty 分の書き込みを
    /// <see cref="HorrorCheckpointSaveService"/> に委譲する。保存の発火はマスターデータの
    /// フラグに依存せず本クラス自身が担い、何度でも再インタラクトできる。
    /// </summary>
    public class HorrorSavepointInteractable : InteractableBase
    {
        private HorrorCheckpointSaveService _checkpointSaveService;

        protected override void Start()
        {
            base.Start();
            _checkpointSaveService = GameServiceManager.Resolve<HorrorCheckpointSaveService>();
        }

        public override void Interact()
        {
            // 自身のインタラクト記録を先に Dirty 化し、今回の保存に含める
            base.Interact();
            _checkpointSaveService.SaveIfDirtyAsync().Forget();
        }
    }
}
