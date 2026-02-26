using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// プレイヤーごとの状態同期 NetworkBehaviour。
    /// サーバーが NetworkVariable を更新 → NGO が自動的にクライアントへ同期。
    /// クライアントは ServerRpc で入力をサーバーへ送信。
    /// </summary>
    public class NetworkSurvivorPlayerState : NetworkBehaviour
    {
        // --- 高頻度同期（サーバー → クライアント） ---

        public NetworkVariable<NetworkSurvivorPlayerStateSnapshot> State = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString64Bytes> PlayerUserId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // --- クライアント → サーバー入力（ServerRpc） ---

        [ServerRpc]
        public void SendMoveInputServerRpc(float moveX, float moveY, bool isSprinting)
        {
            // Phase 4+: SurvivorPlayerController に入力を転送
            Debug.Log($"[NetworkSurvivorPlayerState] MoveInput from {OwnerClientId}: ({moveX}, {moveY}) sprint={isSprinting}");
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

        // --- ライフサイクル ---

        public override void OnNetworkSpawn()
        {
            if (IsClient)
            {
                State.OnValueChanged += OnStateChanged;
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

        private void OnStateChanged(NetworkSurvivorPlayerStateSnapshot prev, NetworkSurvivorPlayerStateSnapshot current)
        {
            // Phase 5+: クライアント側 View 更新（PlayerView.UpdatePosition 等）
        }

        // --- サーバー側ヘルパー ---

        /// <summary>サーバーから State を更新（Phase 4: ServerGameSimulation から呼ばれる）</summary>
        public void UpdateState(NetworkSurvivorPlayerStateSnapshot snapshot)
        {
            if (!IsServer) return;
            State.Value = snapshot;
        }
    }
}
