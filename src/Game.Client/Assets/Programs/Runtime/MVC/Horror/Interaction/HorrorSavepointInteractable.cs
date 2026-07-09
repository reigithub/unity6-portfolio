using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// セーブポイントのインタラクト。進行データの Dirty 分の書き込みを
    /// <see cref="HorrorCheckpointSaveService"/> に委譲する。保存の発火はマスターデータの
    /// フラグに依存せず本クラス自身が担い、何度でも再インタラクトできる。
    /// </summary>
    public class HorrorSavepointInteractable : InteractableBase
    {
        [Tooltip("セーブ後の復帰時にプレイヤーを開始させる位置・向き（Yaw のみ使用）")]
        [SerializeField] private Transform _respawnPoint;

        private HorrorCheckpointSaveService _checkpointSaveService;
        private HorrorRespawnSaveService _respawnSaveService;

        /// <summary>
        /// 復帰時のプレイヤー開始 Transform。未設定はシーン配線漏れとして LogError で顕在化し null を返す
        /// （呼び出し側は初期位置フォールバック）。
        /// </summary>
        public Transform RespawnPoint
        {
            get
            {
                if (_respawnPoint == null)
                    Debug.LogError($"[{nameof(HorrorSavepointInteractable)}] _respawnPoint 未設定 (InteractionId={InteractionId})", this);
                return _respawnPoint;
            }
        }

        protected override void Start()
        {
            base.Start();
            _checkpointSaveService = GameServiceManager.Resolve<HorrorCheckpointSaveService>();
            _respawnSaveService = GameServiceManager.Resolve<HorrorRespawnSaveService>();
        }

        public override void Interact()
        {
            // 自身のインタラクト記録と復帰地点を先に Dirty 化し、今回の保存に含める
            base.Interact();
            _respawnSaveService.SetLastSavepoint(InteractionId);
            _checkpointSaveService.SaveIfDirtyAsync().Forget();
        }
    }
}
