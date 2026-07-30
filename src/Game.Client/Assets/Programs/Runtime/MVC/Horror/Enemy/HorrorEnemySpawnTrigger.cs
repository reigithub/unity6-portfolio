using Game.Core.Services;
using Game.Horror.Player;
using Game.Horror.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// エネミースポーントリガー。プレイヤーがトリガーボリュームへ進入したら通過をサービスへ通知し、自身を無効化する。
    /// 起動先グループの対応はマスタ（HorrorEnemySpawnTriggerMaster）が正本で、シーン側は Id のみを持つ
    /// （<see cref="HorrorEnemyStart"/> と同じ純粋マーカーの作法）。
    /// 発火済み（セーブデータからの復元含む）は Start で自己無効化する（HorrorItemInteractable の自己復元と同型）。
    /// </summary>
    public class HorrorEnemySpawnTrigger : MonoBehaviour
    {
        [Tooltip("トリガーの ID（HorrorEnemySpawnTriggerMasterTable の PrimaryKey）。0 は未設定")]
        [SerializeField] private int _triggerId;

        private IHorrorEnemyService _enemyService;

        private void Start() => HandleStart();

        private void OnTriggerEnter(Collider other) => HandleEnter(other);

        /// <summary>Start の実体（テストから直接呼ぶため分離）。サービス解決と発火済みの自己無効化を行う。</summary>
        internal void HandleStart()
        {
            _enemyService = GameServiceManager.Resolve<IHorrorEnemyService>();

            if (_triggerId == 0)
            {
                Debug.LogError($"[{nameof(HorrorEnemySpawnTrigger)}] {name} の TriggerId が未設定(0)です", this);
                return;
            }

            if (_enemyService.IsTriggerFired(_triggerId))
                gameObject.SetActive(false);
        }

        /// <summary>OnTriggerEnter の実体（テストから直接呼ぶため分離）。プレイヤーの進入なら通過を通知して自己無効化する。</summary>
        internal void HandleEnter(Collider other)
        {
            if (other.GetComponentInParent<HorrorPlayerController>() == null) return;

            _enemyService.NotifyTriggerPassed(_triggerId);

            // enabled=false ではコライダーが残り、無効な MonoBehaviour にも OnTriggerEnter が配送されるため GameObject ごと無効化する
            gameObject.SetActive(false);
        }
    }
}
