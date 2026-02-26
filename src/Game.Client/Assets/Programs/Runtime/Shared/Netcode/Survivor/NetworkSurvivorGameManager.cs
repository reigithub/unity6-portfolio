using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// ゲーム全体のイベント配信 NetworkBehaviour（シングルトン）。
    /// IGameStageHubReceiver の 19 コールバックに対応する ClientRpc を定義。
    /// Phase 3 では全 ClientRpc 本体はスタブ。Phase 5 でクライアント側イベントハンドリング実装。
    /// </summary>
    public class NetworkSurvivorGameManager : NetworkBehaviour
    {
        public static NetworkSurvivorGameManager Instance { get; private set; }

        // --- セッション ---

        [ClientRpc]
        public void NotifyAllPlayersReadyClientRpc()
        {
            Debug.Log("[NetworkSurvivorGameManager] AllPlayersReady");
        }

        [ClientRpc]
        public void NotifyGameStartedClientRpc(float serverTime)
        {
            Debug.Log($"[NetworkSurvivorGameManager] GameStarted at serverTime={serverTime}");
        }

        // --- プレイヤーイベント ---

        [ClientRpc]
        public void NotifyPlayerDamagedClientRpc(FixedString64Bytes userId, int damage, int currentHp)
        {
            // Phase 5+: ダメージ演出
        }

        [ClientRpc]
        public void NotifyPlayerDiedClientRpc(FixedString64Bytes userId)
        {
            // Phase 5+: 死亡演出
        }

        [ClientRpc]
        public void NotifyItemCollectedClientRpc(FixedString64Bytes userId, int itemId, int effectValue)
        {
            // Phase 5+: アイテム取得演出
        }

        [ClientRpc]
        public void NotifyPlayerLevelUpClientRpc(FixedString64Bytes userId, int newLevel, NetworkSurvivorWeaponUpgradeOption[] options)
        {
            // Phase 5+: レベルアップ UI 表示
        }

        [ClientRpc]
        public void NotifyWeaponChangedClientRpc(FixedString64Bytes userId, int weaponId, int level, bool isNew)
        {
            // Phase 5+: 武器変更演出
        }

        // --- 敵・スコア ---

        [ClientRpc]
        public void NotifyEnemyKilledClientRpc(FixedString64Bytes killerUserId, int enemyId, int scoreGained, int totalKills)
        {
            // Phase 5+: キル演出・スコア更新
        }

        // --- ウェーブ ---

        [ClientRpc]
        public void NotifyWaveClearedClientRpc(int waveNumber, int nextWaveNumber, int waveClearScore)
        {
            // Phase 5+: ウェーブクリア演出
        }

        [ClientRpc]
        public void NotifyWaveStartedClientRpc(int waveNumber, int targetKills, int totalEnemies)
        {
            // Phase 5+: ウェーブ開始 UI
        }

        [ClientRpc]
        public void NotifyAllWavesClearedClientRpc()
        {
            // Phase 5+: 全ウェーブクリア演出
        }

        [ClientRpc]
        public void NotifyTimeUpClientRpc()
        {
            // Phase 5+: タイムアップ演出
        }

        // --- ゲーム終了 ---

        [ClientRpc]
        public void NotifyGameEndedClientRpc(NetworkSurvivorGameResult result)
        {
            // Phase 5+: リザルト画面表示
        }

        // --- ポーズ ---

        [ClientRpc]
        public void NotifyGamePausedClientRpc(FixedString64Bytes requestedByUserId)
        {
            // Phase 5+: ポーズ UI 表示
        }

        [ClientRpc]
        public void NotifyGameResumedClientRpc()
        {
            // Phase 5+: ポーズ解除
        }

        // --- 接続 ---

        [ClientRpc]
        public void NotifyPlayerConnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            // Phase 5+: 接続通知 UI
        }

        [ClientRpc]
        public void NotifyPlayerDisconnectedClientRpc(FixedString64Bytes userId, FixedString64Bytes playerName)
        {
            // Phase 5+: 切断通知 UI
        }

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Debug.Log($"[NetworkSurvivorGameManager] Spawned (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
