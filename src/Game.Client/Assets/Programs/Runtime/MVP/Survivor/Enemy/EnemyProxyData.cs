using Cysharp.Threading.Tasks;
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
        public EnemyVisualEffectController VfxController;
        public int PreviousHp;

        /// <summary>攻撃アニメーションを再生する</summary>
        public void PlayAttack()
        {
            if (Animator == null) return;
            Animator.SetFloat(EnemyAnimatorHashes.Speed, 0f);
            Animator.SetTrigger(EnemyAnimatorHashes.Attack);
        }

        /// <summary>死亡アニメーションを再生し、コライダーを無効化する。ディゾルブエフェクトも起動する</summary>
        public void PlayDeath()
        {
            IsDead = true;
            if (Animator != null)
            {
                Animator.SetFloat(EnemyAnimatorHashes.Speed, 0f);
                Animator.SetTrigger(EnemyAnimatorHashes.Death);
            }
            DisableColliders();

            // ディゾルブエフェクト再生（CancellationToken は MonoBehaviour.OnDestroy で自動キャンセル）
            VfxController?.PlayDeathDissolveAsync().Forget();
        }

        /// <summary>HP 減少検知時にヒットフラッシュを再生</summary>
        public void PlayHitFlash()
        {
            VfxController?.PlayHitFlash();
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
