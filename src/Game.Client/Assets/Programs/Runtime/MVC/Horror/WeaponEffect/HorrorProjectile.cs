using Game.Shared.Scriptable.Database.Tables;
using UnityEngine;

namespace Game.Horror.WeaponEffect
{
    /// <summary>
    /// 武器から投射される実体の抽象基底。スポナーとの契約（<see cref="Launch"/>）と、
    /// 全投擲物で共通の射出処理（Rigidbody への初速設定・射手コライダーとの衝突無効化）のみを担う。
    /// 起爆条件・接触挙動・起爆効果は具象クラス（例: <see cref="HorrorSmokeGrenadeProjectile"/>）が実装する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class HorrorProjectile : MonoBehaviour
    {
        /// <summary>射出元武器のマスター（Launch で注入）</summary>
        protected HorrorWeaponMaster Master { get; private set; }

        /// <summary>生成元スポナー（起爆効果の生成は必ずこの追跡付き単一入口を通す）</summary>
        protected HorrorWeaponEffectSpawner Spawner { get; private set; }

        /// <summary>
        /// 投擲物を射出する。<see cref="HorrorWeaponEffectSpawner.SpawnProjectile"/> から生成直後に呼ばれる。
        /// </summary>
        /// <param name="velocity">初速ベクトル（方向 × 初速）</param>
        /// <param name="master">射出元武器のマスター</param>
        /// <param name="spawner">生成元スポナー</param>
        /// <param name="ignoreColliders">衝突を無効化する射手側コライダー（自己衝突防止）</param>
        public void Launch(Vector3 velocity, HorrorWeaponMaster master, HorrorWeaponEffectSpawner spawner, Collider[] ignoreColliders)
        {
            Master = master;
            Spawner = spawner;

            if (ignoreColliders != null)
            {
                foreach (var own in GetComponentsInChildren<Collider>())
                {
                    foreach (var ignore in ignoreColliders)
                    {
                        if (ignore != null)
                            Physics.IgnoreCollision(own, ignore);
                    }
                }
            }

            TryGetComponent(out Rigidbody rb);
            rb.linearVelocity = velocity;
        }
    }
}
