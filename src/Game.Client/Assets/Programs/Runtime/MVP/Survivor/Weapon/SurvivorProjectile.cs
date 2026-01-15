using System;
using UnityEngine;

namespace Game.MVP.Survivor.Weapon
{
    /// <summary>
    /// プロジェクタイル（弾）
    /// 貫通機能対応
    /// </summary>
    public class SurvivorProjectile : MonoBehaviour
    {
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private string _enemyTag = "Enemy";

        // State
        private Vector3 _direction;
        private float _speed;
        private float _lifetime;
        private int _damage;
        private int _pierce;
        private int _currentPierce;
        private bool _isActive;

        public int Damage => _damage;

        // Events
        public event Action<SurvivorProjectile, Collider> OnHit;
        public event Action<SurvivorProjectile> OnLifetimeExpired;

        public void Fire(Vector3 direction, float speed, int damage, float lifetime, int pierce = 0)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _pierce = pierce;
            _currentPierce = pierce;
            _isActive = true;

            // 向きを設定
            if (_direction.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(_direction);
            }

            // トレイルをリセット
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // 移動
            transform.position += _direction * _speed * Time.deltaTime;

            // 寿命チェック
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                _isActive = false;
                OnLifetimeExpired?.Invoke(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            if (other.CompareTag(_enemyTag))
            {
                OnHit?.Invoke(this, other);
            }
        }

        /// <summary>
        /// 貫通数をデクリメント
        /// </summary>
        /// <returns>true: 弾を消す, false: 貫通継続</returns>
        public bool DecrementPierce()
        {
            if (_currentPierce <= 0)
            {
                _isActive = false;
                return true;
            }

            _currentPierce--;
            return false;
        }

        public void Reset()
        {
            _isActive = false;
            _direction = Vector3.zero;
            _speed = 0f;
            _damage = 0;
            _lifetime = 0f;
            _pierce = 0;
            _currentPierce = 0;

            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }
    }
}
