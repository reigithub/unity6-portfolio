#if UNITY_SERVER
using UnityEngine;
using UnityEngine.AI;

namespace Game.Shared.DedicatedServer
{
    /// <summary>
    /// Dedicated Server で PhysX / NavMesh が動作することを検証
    /// Phase 2 検証用 — Phase 3-4 以降で削除可
    /// </summary>
    public class DedicatedServerPhysicsVerifier : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[ServerVerify] ========================================");
            Debug.Log("[ServerVerify] Physics/NavMesh verification starting...");

            // PhysX 検証: OverlapSphere
            var colliders = Physics.OverlapSphere(transform.position, 10f);
            Debug.Log($"[ServerVerify] Physics.OverlapSphere(r=10): " +
                      $"{colliders.Length} colliders found");

            // NavMesh 検証: SamplePosition
            bool hasNavMesh = NavMesh.SamplePosition(
                transform.position, out var hit, 100f, NavMesh.AllAreas);
            Debug.Log($"[ServerVerify] NavMesh.SamplePosition: " +
                      $"found={hasNavMesh}, pos={hit.position}");

            // 動的物理検証: CheckSphere
            bool checkResult = Physics.CheckSphere(transform.position, 5f);
            Debug.Log($"[ServerVerify] Physics.CheckSphere(r=5): {checkResult}");

            Debug.Log("[ServerVerify] Verification completed.");
            Debug.Log("[ServerVerify] ========================================");
        }
    }
}
#endif
