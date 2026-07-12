using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Interaction
{
    /// <summary>
    /// セーブポイントのインタラクト。<see cref="HorrorSaveDataDialog"/> でスロットを選択させ、
    /// 選択されたスロットへ進行データの Dirty 分の書き込みを <see cref="HorrorSaveRepository"/>
    /// に委譲する。キャンセル時は何も書き込まず、何度でも再インタラクトできる。
    /// </summary>
    public class HorrorSavepointInteractable : InteractableBase
    {
        [Tooltip("セーブ後の復帰時にプレイヤーを開始させる位置・向き（Yaw のみ使用）")]
        [SerializeField] private Transform _respawnPoint;

        private IHorrorSaveRepository _saveRepository;
        private IHorrorPlayerService _playerService;

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
            _saveRepository = GameServiceManager.Resolve<IHorrorSaveRepository>();
            _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();
        }

        public override void Interact()
        {
            InteractAsync().Forget();
        }

        private async UniTask InteractAsync()
        {
            var slots = await _saveRepository.LoadSlotInfosAsync();
            var selected = await HorrorSaveDataDialog.RunAsync(slots);
            if (selected == 0) return;

            // 自身のインタラクト記録と復帰地点を選択後に Dirty 化し、今回の保存に含める
            base.Interact();
            _playerService.SetLastSavepoint(InteractionId);
            await _saveRepository.SaveToSlotAsync(selected);
        }
    }
}
