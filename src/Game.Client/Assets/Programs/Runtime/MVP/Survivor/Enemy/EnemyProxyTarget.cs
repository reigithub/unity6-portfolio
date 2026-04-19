using Game.Shared.Combat;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// クライアント敵プロキシ用ターゲットコンポーネント。
    /// LockOnServiceがOverlapSphereで検出し、CenterPositionを取得する。
    /// ICombatTarget実装: TakeDamage/ApplyKnockbackはno-op（ダメージはRPC経由でサーバーが処理）。
    /// </summary>
    public class EnemyProxyTarget : MonoBehaviour, ICombatTarget
    {
        public IEnemyDeathQuery DeathQuery { get; set; }
        public int NetworkId { get; set; }
        public Vector3 CenterPosition => transform.position + Vector3.up;
        public bool IsDead => DeathQuery != null && DeathQuery.IsProxyDead(NetworkId);
        public void TakeDamage(int damage) { }
        public void ApplyKnockback(Vector3 knockback) { }
    }
}
