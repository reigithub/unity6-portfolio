using System;
using Mirror;
using Unity.Collections;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤーごとの状態同期 NetworkBehaviour。
    /// サーバーが SyncVar を更新 → Mirror が自動的にクライアントへ同期。
    /// クライアントは Command でサーバーへ入力を送信。
    /// </summary>
    public class SurvivorNetworkPlayerState : NetworkBehaviour
    {
        // --- 高頻度同期（サーバー → クライアント） ---

        [SyncVar(hook = nameof(OnStateChanged))]
        public SurvivorNetworkPlayerStateSnapshot State;

        [SyncVar]
        public FixedString64Bytes PlayerUserId;

        /// <summary>外部リスナー用イベント（SyncVar hook から発火）</summary>
        public event Action<SurvivorNetworkPlayerStateSnapshot, SurvivorNetworkPlayerStateSnapshot> OnStateUpdated;

        // --- サーバー側入力バッファ ---
        private float _pendingMoveX;
        private float _pendingMoveY;
        private bool _pendingIsSprinting;
        private bool _hasInput;

        /// <summary>
        /// サーバー側: バッファされた入力を消費する。
        /// SurvivorPlayerController.UpdateInput() から呼ばれる。
        /// </summary>
        public bool TryConsumeInput(out float moveX, out float moveY, out bool isSprinting)
        {
            if (!_hasInput)
            {
                moveX = 0;
                moveY = 0;
                isSprinting = false;
                return false;
            }
            moveX = _pendingMoveX;
            moveY = _pendingMoveY;
            isSprinting = _pendingIsSprinting;
            _hasInput = false;
            return true;
        }

        // --- クライアント → サーバー入力（Command） ---

        [Command]
        public void SendMoveInputServerRpc(float moveX, float moveY, bool isSprinting)
        {
            _pendingMoveX = moveX;
            _pendingMoveY = moveY;
            _pendingIsSprinting = isSprinting;
            _hasInput = true;
        }

        [Command]
        public void SendWeaponChoiceServerRpc(int weaponId, bool isNewWeapon)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] WeaponChoice from {connectionToClient?.connectionId}: weapon={weaponId} new={isNewWeapon}");
        }

        [Command]
        public void SendWeaponReplaceServerRpc(int removeWeaponId, int newWeaponId)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] WeaponReplace from {connectionToClient?.connectionId}: remove={removeWeaponId} new={newWeaponId}");
        }

        [Command]
        public void RequestPauseServerRpc()
        {
            Debug.Log($"[NetworkSurvivorPlayerState] PauseRequest from {connectionToClient?.connectionId}");
        }

        [Command]
        public void RequestResumeServerRpc()
        {
            Debug.Log($"[NetworkSurvivorPlayerState] ResumeRequest from {connectionToClient?.connectionId}");
        }

        [Command]
        public void ReportGameEndServerRpc(SurvivorNetworkGameResult result)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] GameEnd from {connectionToClient?.connectionId}: victory={result.IsVictory}");
            SurvivorNetworkGameManager.Instance?.NotifyGameEndedClientRpc(result);
        }

        [Command]
        public void NotifySceneReadyServerRpc()
        {
            Debug.Log($"[NetworkSurvivorPlayerState] SceneReady from conn={connectionToClient?.connectionId}");
            SurvivorNetworkGameManager.Instance?.OnClientSceneReady(connectionToClient);
        }

        // --- ライフサイクル ---

        public override void OnStartClient()
        {
            DontDestroyOnLoad(gameObject);
            if (isOwned)
            {
                foreach (var bindable in SurvivorNetworkPlayerStateBindableRegistry.Bindables)
                {
                    bindable.BindNetworkPlayerState(this);
                    Debug.Log($"[NetworkSurvivorPlayerState] Client bound to {bindable.GetType().Name}");
                    break;
                }
            }
            else
            {
                // リモートプレイヤー: 簡易ビジュアル + 補間表示
                var view = gameObject.AddComponent<SurvivorRemotePlayerView>();
                view.Initialize(this);
                Debug.Log($"[NetworkSurvivorPlayerState] Remote player view created for {PlayerUserId}");
            }
            Debug.Log($"[NetworkSurvivorPlayerState] Spawned on client (isOwned={isOwned})");
        }

        public override void OnStartServer()
        {
            foreach (var bindable in SurvivorNetworkPlayerStateBindableRegistry.Bindables)
            {
                bindable.BindNetworkPlayerState(this);
                Debug.Log($"[NetworkSurvivorPlayerState] Bound to {bindable.GetType().Name} for client {connectionToClient?.connectionId}");
                break;
            }
            Debug.Log("[NetworkSurvivorPlayerState] Spawned on server");
        }

        private void OnStateChanged(SurvivorNetworkPlayerStateSnapshot oldValue, SurvivorNetworkPlayerStateSnapshot newValue)
        {
            OnStateUpdated?.Invoke(oldValue, newValue);
        }

        // --- サーバー側ヘルパー ---

        /// <summary>サーバーから State を更新（Phase 4: SurvivorServerSimulation から呼ばれる）</summary>
        public void UpdateState(SurvivorNetworkPlayerStateSnapshot snapshot)
        {
            if (!isServer) return;
            State = snapshot;
        }
    }
}
