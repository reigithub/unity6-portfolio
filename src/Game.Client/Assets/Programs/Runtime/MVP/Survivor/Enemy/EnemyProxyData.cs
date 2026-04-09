using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// クライアント敵プロキシの状態データ。
    /// Animator 駆動・Collider 制御をメソッドとして提供し、
    /// SurvivorEnemyView のオーケストレーションを簡素化する。
    /// </summary>
    internal class EnemyProxyData
    {
        public GameObject GameObject;
        public Animator Animator;
        public Collider[] Colliders;
        public int EnemyMasterId;
        public bool IsDead;
        public float DeathAnimDuration;
        public EnemyProxyInterpolation Interpolation;
        public int LodUpdateInterval = 1;
        public int FrameOffset;

        /// <summary>攻撃アニメーションを再生する</summary>
        public void PlayAttack()
        {
            if (Animator == null) return;
            Animator.SetFloat(EnemyAnimatorHashes.Speed, 0f);
            Animator.SetTrigger(EnemyAnimatorHashes.Attack);
        }

        /// <summary>死亡アニメーションを再生し、コライダーを無効化する</summary>
        public void PlayDeath()
        {
            IsDead = true;
            if (Animator != null)
            {
                Animator.SetFloat(EnemyAnimatorHashes.Speed, 0f);
                Animator.SetTrigger(EnemyAnimatorHashes.Death);
            }
            DisableColliders();
        }

        /// <summary>速度に応じてアニメーターの Speed パラメータを更新する</summary>
        public void UpdateAnimatorSpeed(float velocityMagnitude)
        {
            if (Animator != null)
                Animator.SetFloat(EnemyAnimatorHashes.Speed, velocityMagnitude > 0.1f ? 1f : 0f);
        }

        private void DisableColliders()
        {
            if (Colliders == null) return;
            foreach (var col in Colliders)
                if (col != null) col.enabled = false;
        }
    }
}
