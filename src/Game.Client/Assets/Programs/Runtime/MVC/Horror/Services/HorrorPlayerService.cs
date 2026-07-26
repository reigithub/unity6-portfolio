using Game.Horror.Constants;
using Game.Horror.Services.Interfaces;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror プレイヤー状態を扱うドメインサービス。操作対象のプレイヤーマスターの解決・保持と、残 HP の永続化を担う。
    /// </summary>
    public class HorrorPlayerService : IHorrorPlayerService
    {
        private readonly IHorrorSaveRepository _repository;
        private readonly IScriptableDatabaseService _databaseService;

        // プレイヤーマスターの解決キャッシュ（要求 Id が変わった時のみ再解決。解決結果が null でも保持してログの連打を防ぐ）
        private int _resolvedPlayerId;
        private HorrorPlayerMaster _resolvedPlayerMaster;

        public HorrorPlayerService(IHorrorSaveRepository repository, IScriptableDatabaseService databaseService)
        {
            _repository = repository;
            _databaseService = databaseService;
        }

        /// <summary>操作するプレイヤーの Id（未ロード時は既定 Id）。</summary>
        public int PlayerId => _repository.Data?.Player?.PlayerId ?? HorrorSaveConstants.DefaultPlayerId;

        /// <summary>
        /// 現在プレイ中のプレイヤーマスター（解決失敗時は null）。
        /// 遅延解決＋要求 Id キーのキャッシュ。Id 比較が毎読みで真実源（セーブデータ）に追従するため、
        /// セーブ差し替え（ロード・新規作成）にもイベント購読なしで自動追従する。
        /// </summary>
        public HorrorPlayerMaster PlayerMaster
        {
            get
            {
                var id = PlayerId;
                if (id == _resolvedPlayerId)
                    return _resolvedPlayerMaster;

                _resolvedPlayerId = id;
                _resolvedPlayerMaster = ResolvePlayerMaster(id);
                return _resolvedPlayerMaster;
            }
        }

        /// <summary>
        /// 要求 Id → 既定 Id の順にマスターを解決する。フォールバックの発動はセーブデータ側の不整合として LogWarning、
        /// 既定 Id も引けない場合はマスターデータ側の不変条件違反として LogError の上 null を返す。
        /// </summary>
        private HorrorPlayerMaster ResolvePlayerMaster(int id)
        {
            var table = _databaseService.Database.HorrorPlayerMasterTable;
            if (table.TryFindById(id, out var master))
                return master;

            if (table.TryFindById(HorrorSaveConstants.DefaultPlayerId, out var defaultMaster))
            {
                Debug.LogWarning($"プレイヤーマスターが見つかりません Id={id}。既定 Id={HorrorSaveConstants.DefaultPlayerId} で代替します");
                return defaultMaster;
            }

            Debug.LogError($"プレイヤーマスターが見つかりません Id={id}（既定 Id={HorrorSaveConstants.DefaultPlayerId} も未登録）");
            return null;
        }

        /// <summary>残 HP（0 = 未記録・未ロード。復元側で最大 HP へ正規化する）。</summary>
        public int CurrentHealth => _repository.Data?.Player?.CurrentHealth ?? 0;

        /// <summary>
        /// 残 HP を記録する。未ロード時は LogError の上で何もしない。同値の場合も何もしない（同値で Dirty にしない）。
        /// 0 も有効値として記録する（死亡時。ゲームオーバー後は Continue/Load でデータごと置き換わる）。
        /// </summary>
        public void SetCurrentHealth(int health)
        {
            var data = _repository.Data?.Player;
            if (data == null)
            {
                Debug.LogError($"セーブデータ未ロードのため {nameof(SetCurrentHealth)}({health}) を無視しました");
                return;
            }

            if (data.CurrentHealth == health)
                return;

            data.CurrentHealth = health;
            _repository.MarkDirty();
        }

        /// <summary>
        /// 最大 HP（0 = マスター未解決）。他メンバーと異なりマスタ由来のランタイム値であり、
        /// セーブリポジトリを経由しない（Dirty 化もしない）。
        /// </summary>
        public int MaxHealth => PlayerMaster?.MaxHealth ?? 0;

        /// <summary>HP が満タンで回復アイテムを使用できないか。MaxHealth 未解決（0 以下）は満タン扱いにしない。</summary>
        public bool IsHealthFull => MaxHealth > 0 && CurrentHealth >= MaxHealth;
    }
}
