using System;
using Game.Shared.Constants;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// 地面設置型ダメージエリア
    /// 接触した敵に継続ダメージを与える
    /// </summary>
    public class SurvivorGroundDamageArea : MonoBehaviour, IPoolableWeaponItem
    {
        [SerializeField] private SphereCollider _damageCollider;
        [SerializeField] private ParticleSystem _vfx;

        // 状態
        private int _damage;
        private float _procInterval;
        private float _knockback;
        private float _remainingTime;
        private float _nextProcTime;
        private bool _isActive;

        // コールバック
        public event Action<SurvivorGroundDamageArea> OnExpired;
        public event Action<SurvivorGroundDamageArea, Collider> OnHit;

        public int Damage => _damage;
        public float Knockback => _knockback;

        /// <summary>
        /// ダメージエリアを有効化
        /// </summary>
        public void Activate(int damage, float duration, float procInterval,
                             float knockback, float hitboxRadius)
        {
            _damage = damage;
            _procInterval = procInterval;
            _knockback = knockback;
            _remainingTime = duration;
            _nextProcTime = 0f;
            _isActive = true;

            if (_damageCollider != null)
                _damageCollider.radius = hitboxRadius;

            if (_vfx != null)
            {
                _vfx.Clear();
                _vfx.Play();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            _remainingTime -= Time.deltaTime;
            _nextProcTime -= Time.deltaTime;

            // ProcInterval毎にダメージ判定
            if (_nextProcTime <= 0f)
            {
                _nextProcTime = _procInterval;

                // 現在接触中の敵にOnHitを発火
                if (_damageCollider != null)
                {
                    var colliders = Physics.OverlapSphere(
                        transform.position,
                        _damageCollider.radius,
                        LayerMaskConstants.Enemy);

                    foreach (var col in colliders)
                    {
                        OnHit?.Invoke(this, col);
                    }
                }
            }

            // 持続時間終了
            if (_remainingTime <= 0f)
            {
                _isActive = false;
                if (_vfx != null) _vfx.Stop();
                OnExpired?.Invoke(this);
            }
        }

        /// <summary>
        /// ダメージエリアを非活性化
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            _remainingTime = 0f;
            if (_vfx != null) _vfx.Stop();
        }

        /// <summary>
        /// イベントリスナーをクリア
        /// </summary>
        public void ClearListeners()
        {
            OnExpired = null;
            OnHit = null;
        }
    }
}
