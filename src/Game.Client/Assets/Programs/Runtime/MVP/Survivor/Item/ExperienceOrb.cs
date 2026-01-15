using System;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// 経験値オーブ
    /// 敵を倒した時にドロップし、プレイヤーが拾うと経験値を得る
    /// </summary>
    public class ExperienceOrb : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _experienceValue = 5;
        [SerializeField] private float _attractDistance = 3f;
        [SerializeField] private float _attractSpeed = 10f;
        [SerializeField] private float _collectDistance = 0.5f;

        [Header("Visual")]
        [SerializeField] private float _floatAmplitude = 0.2f;
        [SerializeField] private float _floatSpeed = 2f;

        // State
        private Transform _target;
        private Vector3 _initialPosition;
        private float _floatTimer;
        private bool _isCollected;

        // Events
        public event Action<ExperienceOrb, int> OnCollected;

        public int ExperienceValue
        {
            get => _experienceValue;
            set => _experienceValue = value;
        }

        private void Start()
        {
            _initialPosition = transform.position;
        }

        private void Update()
        {
            if (_isCollected) return;

            // プレイヤーを探す
            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _target = player.transform;
                }
            }

            if (_target != null)
            {
                float distance = Vector3.Distance(transform.position, _target.position);

                // 吸引範囲内なら近づく
                if (distance <= _attractDistance)
                {
                    Vector3 direction = (_target.position - transform.position).normalized;
                    transform.position += direction * _attractSpeed * Time.deltaTime;

                    // 収集判定
                    if (distance <= _collectDistance)
                    {
                        Collect();
                    }
                }
                else
                {
                    // 浮遊アニメーション
                    _floatTimer += Time.deltaTime * _floatSpeed;
                    float yOffset = Mathf.Sin(_floatTimer) * _floatAmplitude;
                    transform.position = _initialPosition + Vector3.up * yOffset;
                }
            }
        }

        public void Collect()
        {
            if (_isCollected) return;

            _isCollected = true;
            OnCollected?.Invoke(this, _experienceValue);

            // 収集エフェクト（あれば）
            // Instantiate(collectEffect, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
        }

        public void Reset()
        {
            _isCollected = false;
            _target = null;
            _floatTimer = 0f;
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            _initialPosition = position;
        }
    }
}
