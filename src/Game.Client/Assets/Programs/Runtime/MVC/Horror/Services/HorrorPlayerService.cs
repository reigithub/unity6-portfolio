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

        // 今回のプレイで操作するプレイヤーのマスター。ResolvePlayerMaster で確定し、プレイ中は変わらない
        private HorrorPlayerMaster _playerMaster;

        public HorrorPlayerService(IHorrorSaveRepository repository, IScriptableDatabaseService databaseService)
        {
            _repository = repository;
            _databaseService = databaseService;
        }

        /// <summary>現在プレイ中のプレイヤーマスター（<see cref="ResolvePlayerMaster"/> 前・解決失敗時は null）。</summary>
        public HorrorPlayerMaster PlayerMaster => _playerMaster;

        /// <summary>
        /// セーブデータの PlayerId から操作対象のマスターを確定する。プレイヤーの生成に先立って呼ぶ。
        /// 要求 Id が引けなければ既定 Id へフォールバックし、記録と実体の乖離を残さないようセーブデータの
        /// PlayerId も既定 Id へ合わせる（LogWarning）。既定 Id も引けない場合はマスターデータ側の
        /// 不変条件違反として LogError の上 false を返す（プレイヤーを生成できない）。
        /// </summary>
        public bool ResolvePlayerMaster()
        {
            var data = _repository.Data?.Player;
            var requestedId = data?.PlayerId ?? HorrorSaveConstants.DefaultPlayerId;

            if (_databaseService.Database.HorrorPlayerMasterTable.TryFindById(requestedId, out _playerMaster))
                return true;

            if (!_databaseService.Database.HorrorPlayerMasterTable.TryFindById(HorrorSaveConstants.DefaultPlayerId, out _playerMaster))
            {
                Debug.LogError($"プレイヤーマスターが見つかりません Id={requestedId}（既定 Id={HorrorSaveConstants.DefaultPlayerId} も未登録）");
                return false;
            }

            Debug.LogWarning($"プレイヤーマスターが見つかりません Id={requestedId}。既定 Id={_playerMaster.Id} で代替します");

            if (data != null)
            {
                data.PlayerId = _playerMaster.Id;
                _repository.MarkDirty();
            }

            return true;
        }

        public int CurrentHealth => _repository.Data?.Player?.CurrentHealth ?? 0;

        public int MaxHealth => _playerMaster?.MaxHealth ?? 0;

        public bool IsHealthFull => MaxHealth > 0 && CurrentHealth >= MaxHealth;

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
    }
}
