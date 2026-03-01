using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤーごとの状態同期 NetworkBehaviour。
    /// サーバーが NetworkVariable を更新 → NGO が自動的にクライアントへ同期。
    /// クライアントは ServerRpc で入力をサーバーへ送信。
    /// </summary>
    public class SurvivorNetworkPlayerState : NetworkBehaviour
    {
        // --- 高頻度同期（サーバー → クライアント） ---

        public NetworkVariable<SurvivorNetworkPlayerStateSnapshot> State = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString64Bytes> PlayerUserId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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

        // --- クライアント → サーバー入力（ServerRpc） ---
        [ServerRpc]
        public void SendMoveInputServerRpc(float moveX, float moveY, bool isSprinting)
        {
            _pendingMoveX = moveX;
            _pendingMoveY = moveY;
            _pendingIsSprinting = isSprinting;
            _hasInput = true;
        }

        [ServerRpc]
        public void SendWeaponChoiceServerRpc(int weaponId, bool isNewWeapon)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] WeaponChoice from {OwnerClientId}: weapon={weaponId} new={isNewWeapon}");
        }

        [ServerRpc]
        public void SendWeaponReplaceServerRpc(int removeWeaponId, int newWeaponId)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] WeaponReplace from {OwnerClientId}: remove={removeWeaponId} new={newWeaponId}");
        }

        [ServerRpc]
        public void RequestPauseServerRpc()
        {
            Debug.Log($"[NetworkSurvivorPlayerState] PauseRequest from {OwnerClientId}");
        }

        [ServerRpc]
        public void RequestResumeServerRpc()
        {
            Debug.Log($"[NetworkSurvivorPlayerState] ResumeRequest from {OwnerClientId}");
        }

        [ServerRpc]
        public void ReportGameEndServerRpc(SurvivorNetworkGameResult result)
        {
            Debug.Log($"[NetworkSurvivorPlayerState] GameEnd from {OwnerClientId}: victory={result.IsVictory}");
            SurvivorNetworkGameManager.Instance?.NotifyGameEndedClientRpc(result);
        }

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            if (IsClient)
            {
                State.OnValueChanged += OnStateChanged;

                // クライアント & Owner: レジストリからバインド（入力送信用）
                if (IsOwner)
                {
                    foreach (var bindable in SurvivorNetworkPlayerStateBindableRegistry.Bindables)
                    {
                        bindable.BindNetworkPlayerState(this);
                        Debug.Log($"[NetworkSurvivorPlayerState] Client bound to {bindable.GetType().Name}");
                        break;
                    }
                }
            }

            // サーバー: レジストリから INetworkPlayerStateBindable を検索してバインド
            if (IsServer)
            {
                foreach (var bindable in SurvivorNetworkPlayerStateBindableRegistry.Bindables)
                {
                    bindable.BindNetworkPlayerState(this);
                    Debug.Log($"[NetworkSurvivorPlayerState] Bound to {bindable.GetType().Name} for client {OwnerClientId}");
                    break;
                }
            }

            Debug.Log($"[NetworkSurvivorPlayerState] Spawned for client {OwnerClientId} (IsServer={IsServer})");
        }

        public override void OnNetworkDespawn()
        {
            if (IsClient)
            {
                State.OnValueChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(SurvivorNetworkPlayerStateSnapshot prev, SurvivorNetworkPlayerStateSnapshot current)
        {
            // Phase 5+: クライアント側 View 更新（PlayerView.UpdatePosition 等）
        }

        // --- サーバー側ヘルパー ---

        /// <summary>サーバーから State を更新（Phase 4: SurvivorServerSimulation から呼ばれる）</summary>
        public void UpdateState(SurvivorNetworkPlayerStateSnapshot snapshot)
        {
            if (!IsServer) return;
            State.Value = snapshot;
        }
    }
}
