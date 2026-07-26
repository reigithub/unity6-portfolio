using Game.Horror.Constants;
using Game.Horror.Services.Interfaces;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror プレイヤー状態を扱うドメインサービス
    /// </summary>
    public class HorrorPlayerService : IHorrorPlayerService
    {
        private readonly IHorrorSaveRepository _repository;
        private readonly IScriptableDatabaseService _databaseService;

        private HorrorPlayerMaster _playerMaster;

        public HorrorPlayerService(IHorrorSaveRepository repository, IScriptableDatabaseService databaseService)
        {
            _repository = repository;
            _databaseService = databaseService;
        }

        public HorrorPlayerMaster PlayerMaster => _playerMaster;

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
