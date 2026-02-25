using System.Threading.Tasks;
using Game.Library.Shared.Dto;
using MagicOnion;

namespace Game.Library.Shared.Realtime.Hubs
{
    /// <summary>
    /// ゲームステージHub クライアント受信インターフェース
    /// </summary>
    public interface IGameStageHubReceiver
    {
        /// <summary>
        /// 全プレイヤー準備完了通知
        /// </summary>
        void OnAllPlayersReady();

        /// <summary>
        /// ゲーム開始通知
        /// </summary>
        void OnGameStarted(float serverTime);

        /// <summary>
        /// プレイヤー状態同期（20Hz）
        /// </summary>
        void OnPlayersStateSync(PlayerStateSnapshot[] players, float serverTime);

        /// <summary>
        /// 敵状態差分同期
        /// </summary>
        void OnEnemiesStateSync(EnemyStateSnapshot[] enemies);

        /// <summary>
        /// プレイヤー被ダメージ通知
        /// </summary>
        void OnPlayerDamaged(string userId, int damage, int currentHp);

        /// <summary>
        /// プレイヤー死亡通知
        /// </summary>
        void OnPlayerDied(string userId);

        /// <summary>
        /// アイテム取得通知
        /// </summary>
        void OnItemCollected(string userId, int itemId, int effectValue);

        /// <summary>
        /// プレイヤーレベルアップ通知
        /// </summary>
        void OnPlayerLevelUp(string userId, int newLevel, WeaponUpgradeOptionSnapshot[] options);

        /// <summary>
        /// 武器変更通知
        /// </summary>
        void OnWeaponChanged(string userId, int weaponId, int level, bool isNew);

        /// <summary>
        /// 敵撃破通知
        /// </summary>
        void OnEnemyKilled(string killerUserId, int enemyId, int scoreGained, int totalKills);

        /// <summary>
        /// ウェーブクリア通知
        /// </summary>
        void OnWaveCleared(int waveNumber, int nextWaveNumber, int waveClearScore);

        /// <summary>
        /// ウェーブ開始通知
        /// </summary>
        void OnWaveStarted(int waveNumber, int targetKills, int totalEnemies);

        /// <summary>
        /// 全ウェーブクリア通知
        /// </summary>
        void OnAllWavesCleared();

        /// <summary>
        /// 時間切れ通知
        /// </summary>
        void OnTimeUp();

        /// <summary>
        /// ゲーム終了通知
        /// </summary>
        void OnGameEnded(GameResultSnapshot result);

        /// <summary>
        /// ゲームポーズ通知
        /// </summary>
        void OnGamePaused(string requestedByUserId);

        /// <summary>
        /// ゲームポーズ解除通知
        /// </summary>
        void OnGameResumed();

        /// <summary>
        /// プレイヤー接続通知
        /// </summary>
        void OnPlayerConnected(string userId, string playerName);

        /// <summary>
        /// プレイヤー切断通知
        /// </summary>
        void OnPlayerDisconnected(string userId, string playerName);
    }

    /// <summary>
    /// ゲームステージHub サーバー送信インターフェース（StreamingHub）
    /// ゲームプレイ中のリアルタイム通信を担当
    /// </summary>
    public interface IGameStageHub : IStreamingHub<IGameStageHub, IGameStageHubReceiver>
    {
        /// <summary>
        /// セッションに参加
        /// </summary>
        ValueTask JoinSessionAsync(string matchId, int stageId);

        /// <summary>
        /// セッションから離脱
        /// </summary>
        ValueTask LeaveSessionAsync();

        /// <summary>
        /// 準備完了通知
        /// </summary>
        ValueTask ReadyAsync();

        /// <summary>
        /// 移動入力送信
        /// </summary>
        ValueTask SendMoveInputAsync(float moveX, float moveY, bool isSprinting);

        /// <summary>
        /// 武器選択送信
        /// </summary>
        ValueTask SendWeaponChoiceAsync(int weaponId, bool isNewWeapon);

        /// <summary>
        /// 武器入替送信
        /// </summary>
        ValueTask SendWeaponReplaceAsync(int removeWeaponId, int newWeaponId);

        /// <summary>
        /// ポーズ要求
        /// </summary>
        ValueTask RequestPauseAsync();

        /// <summary>
        /// ポーズ解除要求
        /// </summary>
        ValueTask RequestResumeAsync();
    }
}
